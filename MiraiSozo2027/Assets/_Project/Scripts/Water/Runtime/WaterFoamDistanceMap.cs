using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Aura.Water
{
    /// <summary>
    /// 水面シェーダー(ToonWater)用に、「真上から見た、一番近い物体までの水平距離(m)の地図」を作る。
    /// 視線方向の奥行きで泡を決めると、柱に重なって見える所にしか泡が出ず、柱の左右には出ない。
    /// 地図は水面の XZ 位置から直接読めるので、物体の周り全方向に同じ幅の泡を出せる。
    /// 作り方: 物体を水面の高さで輪切りにして「物体の中」の絵を描き、
    /// ジャンプフラッディング法(GPU で距離を求める方法)で各画素の距離にする。
    /// 地図はゲーム開始時と、エディターで値を変えたときに作り直す。物体が動く場合は Bake を呼び直す。
    /// </summary>
    [ExecuteAlways]
    public sealed class WaterFoamDistanceMap : MonoBehaviour
    {
        private const int MinResolution = 256;
        private const int MaxResolution = 4096;
        private const int ThreadGroupSize = 8;
        private const int MaskDepthBits = 24;
        private const int NoDepthBits = 0;
        // 範囲を 0 や負にすると 1 / 範囲 が壊れるため、この値で下限を決める
        private const float MinAreaSize = 1f;
        private const float MinHeightAboveWater = 1f;
#if UNITY_EDITOR
        private const string MaskShaderPath = "Assets/_Project/Scripts/Water/Shaders/WaterFoamMask.shader";
        private const string JumpFloodShaderPath = "Assets/_Project/Scripts/Water/Shaders/WaterFoamJumpFlood.compute";
#endif

        // 計算シェーダー(WaterFoamJumpFlood.compute)の処理名。シェーダー側の #pragma kernel と合わせる
        private const string InitKernelName = "Init";
        private const string FloodKernelName = "Flood";
        private const string ResolveKernelName = "Resolve";
        private static readonly string[] JumpFloodKernelNames = { InitKernelName, FloodKernelName, ResolveKernelName };

        private static readonly int FoamDistanceMapId = Shader.PropertyToID("_WaterFoamDistanceMap");
        private static readonly int FoamMapAreaId = Shader.PropertyToID("_WaterFoamMapArea");
        private static readonly int MaskAreaId = Shader.PropertyToID("_FoamMaskArea");
        private static readonly int MaskHeightId = Shader.PropertyToID("_FoamMaskHeight");
        private static readonly int MaskTextureId = Shader.PropertyToID("_Mask");
        private static readonly int SeedReadId = Shader.PropertyToID("_SeedRead");
        private static readonly int SeedWriteId = Shader.PropertyToID("_SeedWrite");
        private static readonly int DistanceId = Shader.PropertyToID("_Distance");
        private static readonly int ResolutionId = Shader.PropertyToID("_Resolution");
        private static readonly int StepId = Shader.PropertyToID("_Step");
        private static readonly int MetersPerPixelId = Shader.PropertyToID("_MetersPerPixel");

        [SerializeField, Tooltip("泡を出す対象の物体のレイヤー。水面を貫いている物体だけが対象になる")]
        private LayerMask _targetLayers = ~0;
        [SerializeField, Min(MinAreaSize), Tooltip("地図の範囲の一辺の長さ(m)。この水面の位置(XZ)を中心にする。ステージが収まる広さにする")]
        private float _areaSize = 100f;
        [SerializeField, Tooltip("地図の一辺の画素数。大きいほど細かいが重い。2のべき乗に丸め、256〜4096 にする")]
        private int _resolution = 1024;
        [SerializeField, Min(MinHeightAboveWater), Tooltip("水面から上を何 m まで調べるか。物体の一番高い所より大きくする")]
        private float _maxHeightAboveWater = 500f;
        [SerializeField, Tooltip("物体の中を描くシェーダー。エディターでは空のとき自動で入る(ビルドに含めるため参照を残す)")]
        private Shader _maskShader;
        [SerializeField, Tooltip("距離を求める ComputeShader。エディターでは空のとき自動で入る(ビルドに含めるため参照を残す)")]
        private ComputeShader _jumpFloodShader;

        // 水面シェーダーが毎フレーム読むので、作り直すか無効になるまで持ち続ける
        private RenderTexture _distanceTexture;

        private void Reset()
        {
            AssignDefaultAssets();
        }

        private void OnEnable()
        {
            AssignDefaultAssets();
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

        private void OnDisable()
        {
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

            int resolution = Mathf.Clamp(Mathf.ClosestPowerOfTwo(_resolution), MinResolution, MaxResolution);
            float areaSize = Mathf.Max(_areaSize, MinAreaSize);
            float maxHeight = Mathf.Max(_maxHeightAboveWater, MinHeightAboveWater);
            Vector3 center = transform.position;
            float waterY = center.y;
            var areaMin = new Vector2(center.x - areaSize * 0.5f, center.z - areaSize * 0.5f);

            RenderTexture mask = null;
            RenderTexture seedA = null;
            RenderTexture seedB = null;
            RenderTexture distance = null;
            Material maskMaterial = null;
            var cmd = new CommandBuffer { name = nameof(WaterFoamDistanceMap) };
            try
            {
                mask = CreateRenderTexture("WaterFoamMask", resolution, RenderTextureFormat.R8, MaskDepthBits, false, FilterMode.Point);
                // 種には画素の座標を入れるので、小数の誤差が出ない 32bit 浮動小数にする
                seedA = CreateRenderTexture("WaterFoamSeedA", resolution, RenderTextureFormat.RGFloat, NoDepthBits, true, FilterMode.Point);
                seedB = CreateRenderTexture("WaterFoamSeedB", resolution, RenderTextureFormat.RGFloat, NoDepthBits, true, FilterMode.Point);
                // 水面からは画素の間も読むので、なめらかにつなぐ
                distance = CreateRenderTexture("WaterFoamDistance", resolution, RenderTextureFormat.RHalf, NoDepthBits, true, FilterMode.Bilinear);

                // 計算シェーダーに書き込ませる画像は、使う前に GPU 側に作っておく必要がある。
                // 作らずに渡すと、エラーも出ずに書き込みが飛ばされ、地図が 0m のまま(範囲全体が泡)になる
                if (!mask.Create() || !seedA.Create() || !seedB.Create() || !distance.Create())
                {
                    LogSkipped("計算用の画像を作れなかった", "解像度(Resolution)を下げてください。");
                    return;
                }

                maskMaterial = CreateMaskMaterial(areaMin, areaSize, waterY, maxHeight);
                RecordMaskPass(cmd, maskMaterial, mask, waterY, areaMin, areaSize);
                RecordJumpFloodPass(cmd, mask, seedA, seedB, distance, resolution, areaSize);
                Graphics.ExecuteCommandBuffer(cmd);

                // 地図は持ち続けるので、下の後片付けで消されないよう手元から外す
                _distanceTexture = distance;
                distance = null;
                PublishDistanceMap(areaMin, areaSize);
            }
            finally
            {
                // マスクと計算途中の画像は、距離を作り終えたら使わないのですぐ解放する
                cmd.Release();
                DestroyResource(mask);
                DestroyResource(seedA);
                DestroyResource(seedB);
                DestroyResource(distance);
                DestroyResource(maskMaterial);
            }
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
            foreach (string kernelName in JumpFloodKernelNames)
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

        private static RenderTexture CreateRenderTexture(
            string textureName, int resolution, RenderTextureFormat format, int depthBits, bool enableRandomWrite, FilterMode filterMode)
        {
            return new RenderTexture(resolution, resolution, depthBits, format, RenderTextureReadWrite.Linear)
            {
                name = textureName,
                hideFlags = HideFlags.HideAndDontSave,
                // 計算シェーダーから書き込む画像だけ有効にする。GPU 側に作る前に決める必要がある
                enableRandomWrite = enableRandomWrite,
                filterMode = filterMode,
                wrapMode = TextureWrapMode.Clamp,
            };
        }

        private Material CreateMaskMaterial(Vector2 areaMin, float areaSize, float waterY, float maxHeight)
        {
            var material = new Material(_maskShader) { hideFlags = HideFlags.HideAndDontSave };
            material.SetVector(MaskAreaId, new Vector4(areaMin.x, areaMin.y, 1f / areaSize, 0f));
            material.SetVector(MaskHeightId, new Vector4(waterY, 1f / maxHeight, 0f, 0f));
            return material;
        }

        /// <summary>対象の物体を水面の高さで輪切りにして、「物体の中」の絵を描く命令を並べる。</summary>
        private void RecordMaskPass(
            CommandBuffer cmd, Material maskMaterial, RenderTexture mask, float waterY, Vector2 areaMin, float areaSize)
        {
            cmd.SetRenderTarget(mask);
            cmd.ClearRenderTarget(true, true, Color.clear);

            List<MeshRenderer> targets = CollectTargets(waterY, areaMin, areaSize);
            foreach (MeshRenderer meshRenderer in targets)
            {
                Mesh mesh = meshRenderer.GetComponent<MeshFilter>().sharedMesh;
                Matrix4x4 matrix = meshRenderer.localToWorldMatrix;
                for (int submesh = 0; submesh < mesh.subMeshCount; submesh++)
                {
                    cmd.DrawMesh(mesh, matrix, maskMaterial, submesh);
                }
            }
        }

        private List<MeshRenderer> CollectTargets(float waterY, Vector2 areaMin, float areaSize)
        {
            var targets = new List<MeshRenderer>();
            Vector2 areaMax = areaMin + Vector2.one * areaSize;
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
                if (bounds.min.y >= waterY || bounds.max.y <= waterY)
                {
                    continue;
                }
                if (bounds.max.x < areaMin.x || bounds.min.x > areaMax.x ||
                    bounds.max.z < areaMin.y || bounds.min.z > areaMax.y)
                {
                    continue;
                }
                targets.Add(meshRenderer);
            }
            return targets;
        }

        /// <summary>
        /// 距離を求める命令を並べる。
        /// 段階ごとに「飛ばす幅」を 解像度の半分 → 1 まで半分ずつ小さくし、最後に幅 1 をもう一度行って精度を上げる。
        /// </summary>
        private void RecordJumpFloodPass(
            CommandBuffer cmd,
            RenderTexture mask,
            RenderTexture seedA,
            RenderTexture seedB,
            RenderTexture distance,
            int resolution,
            float areaSize)
        {
            ComputeShader shader = _jumpFloodShader;
            int initKernel = shader.FindKernel(InitKernelName);
            int floodKernel = shader.FindKernel(FloodKernelName);
            int resolveKernel = shader.FindKernel(ResolveKernelName);
            int groupCount = Mathf.CeilToInt(resolution / (float)ThreadGroupSize);

            cmd.SetComputeIntParam(shader, ResolutionId, resolution);
            cmd.SetComputeFloatParam(shader, MetersPerPixelId, areaSize / resolution);

            cmd.SetComputeTextureParam(shader, initKernel, MaskTextureId, mask);
            cmd.SetComputeTextureParam(shader, initKernel, SeedWriteId, seedA);
            cmd.DispatchCompute(shader, initKernel, groupCount, groupCount, 1);

            RenderTexture read = seedA;
            RenderTexture write = seedB;
            for (int step = resolution / 2; step >= 1; step /= 2)
            {
                RecordFloodStep(cmd, shader, floodKernel, read, write, step, groupCount);
                (read, write) = (write, read);
            }
            RecordFloodStep(cmd, shader, floodKernel, read, write, 1, groupCount);
            (read, write) = (write, read);

            cmd.SetComputeTextureParam(shader, resolveKernel, SeedReadId, read);
            cmd.SetComputeTextureParam(shader, resolveKernel, DistanceId, distance);
            cmd.DispatchCompute(shader, resolveKernel, groupCount, groupCount, 1);
        }

        private static void RecordFloodStep(
            CommandBuffer cmd, ComputeShader shader, int kernel, RenderTexture read, RenderTexture write, int step, int groupCount)
        {
            cmd.SetComputeIntParam(shader, StepId, step);
            cmd.SetComputeTextureParam(shader, kernel, SeedReadId, read);
            cmd.SetComputeTextureParam(shader, kernel, SeedWriteId, write);
            cmd.DispatchCompute(shader, kernel, groupCount, groupCount, 1);
        }

        /// <summary>出来上がった地図を、水面シェーダーが読める共有の値として渡す。</summary>
        private void PublishDistanceMap(Vector2 areaMin, float areaSize)
        {
            Shader.SetGlobalTexture(FoamDistanceMapId, _distanceTexture);
            // w = 1 が「地図あり」。水面シェーダーはこれを見て従来方式との切り替えを決める
            Shader.SetGlobalVector(FoamMapAreaId, new Vector4(areaMin.x, areaMin.y, 1f / areaSize, 1f));
        }

        private void ReleaseResources()
        {
            // 地図なしに戻す。w = 0 だと水面シェーダーは従来の方式を使う
            Shader.SetGlobalVector(FoamMapAreaId, Vector4.zero);
            DestroyResource(_distanceTexture);
            _distanceTexture = null;
        }

        private static void DestroyResource(Object target)
        {
            if (target == null)
            {
                return;
            }
            if (target is RenderTexture renderTexture)
            {
                renderTexture.Release();
            }
            if (Application.isPlaying)
            {
                Destroy(target);
            }
            else
            {
                DestroyImmediate(target);
            }
        }
    }
}
