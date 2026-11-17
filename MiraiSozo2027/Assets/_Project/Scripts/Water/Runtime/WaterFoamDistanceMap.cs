using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace Aura.Water
{
    /// <summary>
    /// 水面シェーダー(ToonWater)用に、「真上から見た、一番近い物体までの水平距離(m)の地図」を作る。
    /// 視線方向の奥行きで泡を決めると、柱に重なって見える所にしか泡が出ず、柱の左右には出ない。
    /// 地図は水面の XZ 位置から直接読めるので、物体の周り全方向に同じ幅の泡を出せる。
    /// 作り方: 物体を水面の高さで輪切りにして「物体の中」の絵を描き、
    /// ジャンプフラッディング法(GPU で距離を求める方法)で各画素の距離にする。
    /// 地図は 2 枚持つ。ステージ全体の「遠い地図」は、ゲーム開始時と、エディターで値を変えたときに作り直す。
    /// カメラの周りだけ細かい「近い地図」は、カメラが一定距離動くたびに作り直す。
    /// 1 枚で広い範囲を細かくすると、一時的に使うメモリが大きすぎるため分けている。
    /// 物体が動く場合は Bake を呼び直す。
    /// </summary>
    [ExecuteAlways]
    public sealed class WaterFoamDistanceMap : MonoBehaviour
    {
        private const int MinResolution = 256;
        private const int MaxResolution = 4096;
        // 範囲を 0 や負にすると 1 / 範囲 が壊れるため、この値で下限を決める
        private const float MinAreaSize = 1f;
        private const float MinHeightAboveWater = 1f;
        private const float MinSnapDistance = 1f;
        private const float MinFoamRange = 1f;
        private const float HalfRatio = 0.5f;
#if UNITY_EDITOR
        private const string MaskShaderPath = "Assets/_Project/Scripts/Water/Shaders/WaterFoamMask.shader";
        private const string JumpFloodShaderPath = "Assets/_Project/Scripts/Water/Shaders/WaterFoamJumpFlood.compute";
#endif

        private static readonly int FarMapId = Shader.PropertyToID("_WaterFoamFarMap");
        private static readonly int FarAreaId = Shader.PropertyToID("_WaterFoamFarArea");
        private static readonly int NearMapId = Shader.PropertyToID("_WaterFoamNearMap");
        private static readonly int NearAreaId = Shader.PropertyToID("_WaterFoamNearArea");

        [SerializeField, Tooltip("泡を出す対象の物体のレイヤー。水面を貫いている物体だけが対象になる")]
        private LayerMask _targetLayers = ~0;
        [SerializeField, Min(MinHeightAboveWater), Tooltip("水面から上を何 m まで調べるか。物体の一番高い所より大きくする")]
        private float _maxHeightAboveWater = 500f;
        [SerializeField, Min(MinFoamRange), Tooltip("物体からこの距離(m)までを正確に求める。水の材質の泡の距離と、光の模様を出す近い距離より大きくする。これより遠い所は「遠い」として扱う")]
        private float _maxFoamRange = 8f;

        [Header("遠い地図(ステージ全体)")]
        [FormerlySerializedAs("_areaSize")]
        [SerializeField, Min(MinAreaSize), Tooltip("遠い地図の範囲の一辺の長さ(m)。この水面の位置(XZ)を中心にする。ステージが収まる広さにする")]
        private float _farAreaSize = 1000f;
        [FormerlySerializedAs("_resolution")]
        [SerializeField, Tooltip("遠い地図の一辺の画素数。大きいほど細かいが重い。2のべき乗に丸め、256〜4096 にする")]
        private int _farResolution = 2048;

        [Header("近い地図(カメラの周り)")]
        [SerializeField, Min(MinAreaSize), Tooltip("近い地図の範囲の一辺の長さ(m)。カメラの位置(XZ)を中心にする")]
        private float _nearAreaSize = 128f;
        [SerializeField, Tooltip("近い地図の一辺の画素数。大きいほど細かいが重い。2のべき乗に丸め、256〜4096 にする")]
        private int _nearResolution = 1024;
        [SerializeField, Min(MinSnapDistance), Tooltip("近い地図の中心を、この間隔(m)の格子に合わせる。カメラがこの距離だけ動くまで作り直さない。大きいほど作り直しが減る")]
        private float _nearSnapDistance = 16f;
        [SerializeField, Tooltip("近い地図が追いかけるカメラ。空のときは Camera.main。エディターで止まっているときは Scene ビューを追いかける")]
        private Camera _targetCamera;

        [Header("シェーダー")]
        [SerializeField, Tooltip("物体の中を描くシェーダー。エディターでは空のとき自動で入る(ビルドに含めるため参照を残す)")]
        private Shader _maskShader;
        [SerializeField, Tooltip("距離を求める ComputeShader。エディターでは空のとき自動で入る(ビルドに含めるため参照を残す)")]
        private ComputeShader _jumpFloodShader;

        // 水面シェーダーが毎フレーム読むので、作り直すか無効になるまで持ち続ける
        private WaterFoamDistanceMapLayer _farLayer;
        private WaterFoamDistanceMapLayer _nearLayer;
        // 近い地図は何度も作り直すので、マスク用の材質と命令の入れ物は無効になるまで使い回す
        private Material _maskMaterial;
        private CommandBuffer _commandBuffer;
        // 物体の探索は重いので、作り直しのたびに 1 回だけ行い、近い地図の更新では使い回す
        private readonly List<MeshRenderer> _targets = new List<MeshRenderer>();
        private float _waterY;
        private float _maxHeight;
        private Vector2 _nearCenter;
        private bool _hasNearCenter;
        private Camera _cachedMainCamera;

        private void Reset()
        {
            AssignDefaultAssets();
        }

        private void OnEnable()
        {
            AssignDefaultAssets();
#if UNITY_EDITOR
            // 再生中は LateUpdate で追いかけるので、エディターの更新はゲームを動かしていない間だけ使う
            UnityEditor.EditorApplication.update -= HandleEditorUpdate;
            UnityEditor.EditorApplication.update += HandleEditorUpdate;
#endif
            Bake();
        }

        private void OnValidate()
        {
            AssignDefaultAssets();
            // OnValidate 中は描画の準備を触れないので、次の更新で作り直す。
            // 値を続けて変えると予約が重なり何度も作り直すため、先に外してから 1 つだけ登録する
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall -= HandleDelayedRebuild;
            UnityEditor.EditorApplication.delayCall += HandleDelayedRebuild;
#endif
        }

        private void LateUpdate()
        {
            if (!Application.isPlaying)
            {
                return;
            }
            TrackCamera(GetPlayModeCamera());
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= HandleEditorUpdate;
#endif
            ReleaseResources();
        }

        private void OnDestroy()
        {
            ReleaseResources();
        }

#if UNITY_EDITOR
        private void HandleDelayedRebuild()
        {
            if (this == null || !isActiveAndEnabled)
            {
                return;
            }
            Bake();
        }

        private void HandleEditorUpdate()
        {
            if (Application.isPlaying)
            {
                return;
            }
            TrackCamera(GetCurrentCamera());
        }
#endif

        /// <summary>
        /// 空の参照に、既定のシェーダーを入れる。エディター専用。
        /// 実行時に名前で探すと、ビルドにシェーダーが含まれず動かなくなるため、
        /// シリアライズした参照として持たせる
        /// </summary>
        private void AssignDefaultAssets()
        {
#if UNITY_EDITOR
            if (_maskShader == null)
            {
                _maskShader = UnityEditor.AssetDatabase.LoadAssetAtPath<Shader>(MaskShaderPath);
            }
            if (_jumpFloodShader == null)
            {
                _jumpFloodShader = UnityEditor.AssetDatabase.LoadAssetAtPath<ComputeShader>(JumpFloodShaderPath);
            }
#endif
        }

        /// <summary>距離の地図を作り直す。物体を動かしたあとに呼ぶ。</summary>
        [ContextMenu("距離の地図を作り直す")]
        public void Bake()
        {
            ReleaseResources();
            if (!CanBake())
            {
                return;
            }

            Vector3 center = transform.position;
            _waterY = center.y;
            _maxHeight = Mathf.Max(_maxHeightAboveWater, MinHeightAboveWater);
            CollectTargets();
            _maskMaterial = new Material(_maskShader) { hideFlags = HideFlags.HideAndDontSave };
            _commandBuffer = new CommandBuffer { name = nameof(WaterFoamDistanceMap) };

            if (!BuildFarLayer(new Vector2(center.x, center.z)))
            {
                ReleaseResources();
                return;
            }
            PrepareNearLayer();
            // 近い地図は、カメラの位置が分かった時点で作る
            TrackCamera(GetCurrentCamera());
        }

        /// <summary>この環境と設定で地図を作れるかを調べる。作れないときは理由を警告に出す。</summary>
        private bool CanBake()
        {
            if (!SystemInfo.supportsComputeShaders)
            {
                LogSkipped("この環境は ComputeShader に対応していない", string.Empty);
                return false;
            }
            if (_maskShader == null || _jumpFloodShader == null)
            {
                LogSkipped("シェーダーの参照が空だった", "Mask Shader と Jump Flood Shader を設定してください。");
                return false;
            }
            // 種の画像は 2 成分の整数で、計算シェーダーから読み書きする。対応していない環境では作れない
            if (!SystemInfo.IsFormatSupported(WaterFoamDistanceMapLayer.SeedFormat, GraphicsFormatUsage.LoadStore))
            {
                LogSkipped("この環境は距離計算用の画像の形式(R16G16_UInt)に対応していない", string.Empty);
                return false;
            }
            // 計算シェーダーが読み込みに失敗していると計算が実行されず、地図が 0m のまま残って
            // 範囲全体が泡になる。それを防ぐため、使えないときは地図を作らず従来の方式に任せる
            if (!CanUseJumpFloodShader())
            {
                LogSkipped($"{_jumpFloodShader.name} がこの環境で使えない", "シェーダーのエラーを確認してください。");
                return false;
            }
            return true;
        }

        /// <summary>計算シェーダーの 3 つの処理が、すべてこの環境で動くかを調べる。</summary>
        private bool CanUseJumpFloodShader()
        {
            foreach (string kernelName in WaterFoamDistanceMapLayer.JumpFloodKernelNames)
            {
                // 読み込みに失敗した処理は見つからないことがあるので、探す前に有無を確かめる
                if (!_jumpFloodShader.HasKernel(kernelName))
                {
                    return false;
                }
                if (!_jumpFloodShader.IsSupported(_jumpFloodShader.FindKernel(kernelName)))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>地図を作らなかったことを、理由と対処とともに警告する。泡は従来の方式になることも伝える。</summary>
        private void LogSkipped(string reason, string remedy)
        {
            Debug.LogWarning(
                $"{nameof(WaterFoamDistanceMap)}: {reason}ため、泡の距離の地図を作りませんでした。" +
                $"{remedy}泡は従来の方式(視線方向の奥行き)になります。", this);
        }

        /// <summary>
        /// 対象の物体を集める。探索は重いので作り直しのたびに 1 回だけ行う。
        /// 範囲での絞り込みは、地図ごとに描くときに行う。
        /// </summary>
        private void CollectTargets()
        {
            _targets.Clear();
            MeshRenderer[] renderers = FindObjectsByType<MeshRenderer>();
            foreach (MeshRenderer meshRenderer in renderers)
            {
                GameObject target = meshRenderer.gameObject;
                if (!meshRenderer.enabled || !target.activeInHierarchy || target == gameObject)
                {
                    continue;
                }
                if ((_targetLayers.value & (1 << target.layer)) == 0)
                {
                    continue;
                }
                if (!target.TryGetComponent(out MeshFilter meshFilter) || meshFilter.sharedMesh == null)
                {
                    continue;
                }

                // 水面を貫いている物体だけを対象にする。橋の床のように水面より上だけにある部品は、
                // 水面から見上げると物体の中に見えてしまい、その下の水面に泡が出てしまうため
                Bounds bounds = meshRenderer.bounds;
                if (bounds.min.y >= _waterY || bounds.max.y <= _waterY)
                {
                    continue;
                }
                _targets.Add(meshRenderer);
            }
        }

        /// <summary>ステージ全体の遠い地図を作る。作れなかったら false。</summary>
        private bool BuildFarLayer(Vector2 centerXZ)
        {
            var layer = new WaterFoamDistanceMapLayer();
            if (!layer.Allocate(ClampResolution(_farResolution), Mathf.Max(_farAreaSize, MinAreaSize)))
            {
                layer.Release();
                LogSkipped("計算用の画像を作れなかった", "遠い地図の解像度(Far Resolution)を下げてください。");
                return false;
            }
            layer.SetAreaCenter(centerXZ);
            RecordAndExecute(layer);
            // 遠い地図は作り終えたら二度と描かないので、マスクと計算途中の画像はすぐ解放する
            layer.ReleaseWorkTextures();

            _farLayer = layer;
            Shader.SetGlobalTexture(FarMapId, layer.Distance);
            // w = 1 が「地図あり」。水面シェーダーはこれを見て従来方式との切り替えを決める
            Shader.SetGlobalVector(FarAreaId, layer.AreaVector);
            return true;
        }

        /// <summary>
        /// 近い地図の画像を用意する。作れなくても遠い地図だけで動くので、警告だけ出して続ける。
        /// 作り直しのたびに作ると重いので、計算用の画像も持ち続ける(ReleaseResources で解放する)
        /// </summary>
        private void PrepareNearLayer()
        {
            var layer = new WaterFoamDistanceMapLayer();
            if (!layer.Allocate(ClampResolution(_nearResolution), Mathf.Max(_nearAreaSize, MinAreaSize)))
            {
                layer.Release();
                LogSkipped("近い地図の画像を作れなかった", "近い地図の解像度(Near Resolution)を下げてください。");
                return;
            }
            _nearLayer = layer;
            _hasNearCenter = false;
            Shader.SetGlobalTexture(NearMapId, layer.Distance);
        }

        /// <summary>カメラの位置を格子に合わせ、前回と違う場所になったときだけ近い地図を作り直す。</summary>
        private void TrackCamera(Camera followCamera)
        {
            if (_nearLayer == null || followCamera == null)
            {
                return;
            }

            Vector3 cameraPosition = followCamera.transform.position;
            Vector2 snappedCenter = new Vector2(
                SnapToGrid(cameraPosition.x, _nearSnapDistance),
                SnapToGrid(cameraPosition.z, _nearSnapDistance));
            if (_hasNearCenter && snappedCenter == _nearCenter)
            {
                return;
            }

            _nearCenter = snappedCenter;
            _hasNearCenter = true;
            _nearLayer.SetAreaCenter(snappedCenter);
            RecordAndExecute(_nearLayer);
            Shader.SetGlobalVector(NearAreaId, _nearLayer.AreaVector);
        }

        /// <summary>
        /// 地図 1 枚分の命令を並べてすぐ実行する。
        /// マスク用の材質を 2 枚の地図で共有していて、実行までに値を書き換えられないようにするため、並べたらすぐ流す
        /// </summary>
        private void RecordAndExecute(WaterFoamDistanceMapLayer layer)
        {
            _commandBuffer.Clear();
            layer.Record(_commandBuffer, _maskMaterial, _jumpFloodShader, _targets, _waterY, _maxHeight, _maxFoamRange);
            Graphics.ExecuteCommandBuffer(_commandBuffer);
        }

        private static float SnapToGrid(float value, float gridSize)
        {
            float safeGridSize = Mathf.Max(gridSize, MinSnapDistance);
            return Mathf.Floor(value / safeGridSize + HalfRatio) * safeGridSize;
        }

        private static int ClampResolution(int resolution)
        {
            return Mathf.Clamp(Mathf.ClosestPowerOfTwo(resolution), MinResolution, MaxResolution);
        }

        /// <summary>再生中に近い地図が追いかけるカメラ。指定が無ければ Camera.main。</summary>
        private Camera GetPlayModeCamera()
        {
            if (_targetCamera != null)
            {
                return _targetCamera;
            }
            // 毎フレーム探さないよう、見つけたものを持っておく
            if (_cachedMainCamera == null)
            {
                _cachedMainCamera = Camera.main;
            }
            return _cachedMainCamera;
        }

        /// <summary>作り直した直後に、近い地図が追いかけるカメラ。エディターで止まっているときは Scene ビュー。</summary>
        private Camera GetCurrentCamera()
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                UnityEditor.SceneView sceneView = UnityEditor.SceneView.lastActiveSceneView;
                if (sceneView != null && sceneView.camera != null)
                {
                    return sceneView.camera;
                }
            }
#endif
            return GetPlayModeCamera();
        }

        private void ReleaseResources()
        {
            // 地図なしに戻す。w = 0 だと水面シェーダーは従来の方式を使う
            Shader.SetGlobalVector(FarAreaId, Vector4.zero);
            Shader.SetGlobalVector(NearAreaId, Vector4.zero);

            if (_farLayer != null)
            {
                _farLayer.Release();
                _farLayer = null;
            }
            if (_nearLayer != null)
            {
                _nearLayer.Release();
                _nearLayer = null;
            }
            _hasNearCenter = false;
            _targets.Clear();

            WaterFoamDistanceMapLayer.DestroyResource(_maskMaterial);
            _maskMaterial = null;
            if (_commandBuffer != null)
            {
                _commandBuffer.Release();
                _commandBuffer = null;
            }
        }
    }
}
