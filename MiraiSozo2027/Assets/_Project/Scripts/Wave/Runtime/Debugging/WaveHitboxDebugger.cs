using System.Collections.Generic;
using Aura.Inspector;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace Aura.Wave
{
    /// @brief 確かめる用。キーを押すと、シーンにあるすべての WaveEmitter の波の実体（当たり判定）を、半透明で出す・消す
    /// GameManager など、シーンをまたいで残る GameObject に 1 つ付ける（どの GameObject に付けてもよい）
    /// ・ビーム: 当たり判定の球（WaveEmitter の「ビームの判定の半径（m）」）が通る道を、実体の線ごとのカプセルで出す
    /// ・扇: 撃った 1 発の当たる範囲（円すい）を薄く、波面（今どこまで届いたか）を濃く出す。波面が届く距離に着くと消える
    /// ・当たった所: 波を渡した位置に、少しの間だけ小さな球を出す
    /// 見た目の部品（IBeamWaveView、IFanWaveView）とは別に、実体の値をそのまま描く。WaveEmitter の後に描く（同じフレームの値を使う）
    [DefaultExecutionOrder(1000)]
    [DisallowMultipleComponent]
    public sealed class WaveHitboxDebugger : MonoBehaviour
    {
        private const int ConeSegments = 32;            ///< 扇の円すいの、まわりの区切りの数
        private const int CapRings = 8;                 ///< 扇の円すいのふた（球の一部）の、縦の区切りの数
        private const float MinBeamDrawRadius = 0.02f;  ///< 判定の半径が 0（太さのない線）のビームを出すときの半径（m）
        private const float MinSegmentLength = 0.0001f; ///< これより短い線は出さない（m）
        private const float AngleRebuildEpsilon = 0.01f; ///< 扇の半角がこれ（度）より変わったら、円すいを作り直す
        private const string ShaderName = "Hidden/Internal-Colored";  ///< Unity の組み込みの、色だけで描くシェーダー（GL の線などに使われている）
        private const float PrimitiveRadius = 0.5f;     ///< Unity の組み込みの球・円柱の半径（スケール 1 のとき）
        private const float CylinderHeight = 2.0f;      ///< Unity の組み込みの円柱の高さ（スケール 1 のとき。y が -1〜1）
        private const float MinDirectionSqrMagnitude = 0.5f;   ///< 扇の向きの長さの 2 乗がこれ未満なら、向きが入っていないとみなして出さない（向きは長さ 1 のはず）
        private const float MinUpCrossSqrMagnitude = 0.0001f;  ///< 扇の向きと上向きの外積の長さの 2 乗がこれ未満なら、上向きが向きと平行とみなして別の上向きを使う
        private static readonly int ColorId = Shader.PropertyToID("_Color");        ///< 組み込みシェーダーの色
        private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");  ///< 組み込みシェーダーの、半透明の混ぜ方（描く側）
        private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");  ///< 組み込みシェーダーの、半透明の混ぜ方（描かれる側）
        private static readonly int CullId = Shader.PropertyToID("_Cull");          ///< 組み込みシェーダーの、裏面を描くか
        private static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");      ///< 組み込みシェーダーの、奥行きを書くか

        [InspectorLabel("切り替えるキー")]
        [Tooltip("押すたびに、当たり判定を出す・消すを切り替えるキー（Input System のキーボード）")]
        [SerializeField] private Key _toggleKey = Key.F3;  ///< 出す・消すを切り替えるキー

        [InspectorLabel("始めから出す")]
        [Tooltip("オンにすると、再生を始めたときから当たり判定を出す")]
        [SerializeField] private bool _isVisibleOnStart;  ///< 再生を始めたときから出すか

        [InspectorLabel("ビームの色")]
        [Tooltip("ビームの当たり判定（カプセル）の色。A（不透明度）で透け具合を決める")]
        [SerializeField] private Color _beamColor = new Color(1.0f, 0.2f, 0.2f, 0.25f);  ///< ビームの当たり判定の色

        [InspectorLabel("扇の範囲の色")]
        [Tooltip("扇の 1 発の、当たる範囲（円すい）の色。A（不透明度）で透け具合を決める")]
        [SerializeField] private Color _fanRangeColor = new Color(1.0f, 0.8f, 0.2f, 0.1f);  ///< 扇の当たる範囲の色

        [InspectorLabel("扇の波面の色")]
        [Tooltip("扇の 1 発の、波面（今どこまで届いたか）の色。波面が届いた物に波が渡る")]
        [SerializeField] private Color _fanFrontColor = new Color(1.0f, 0.45f, 0.1f, 0.35f);  ///< 扇の波面の色

        [InspectorLabel("当たった所の色")]
        [Tooltip("波を渡した位置に出す球の色")]
        [SerializeField] private Color _hitColor = new Color(1.0f, 0.0f, 0.4f, 0.7f);  ///< 当たった所の球の色

        [InspectorLabel("当たった所を出す時間（秒）")]
        [Tooltip("波を渡した位置の球を出しておく時間（秒）。だんだん薄くなって消える")]
        [SerializeField, Min(0.0f)] private float _hitMarkSeconds = 0.3f;  ///< 当たった所の球を出しておく時間（秒）

        [InspectorLabel("当たった所の球の半径（m）")]
        [Tooltip("波を渡した位置に出す球の半径（m）")]
        [SerializeField, Min(0.01f)] private float _hitMarkRadius = 0.12f;  ///< 当たった所の球の半径（m）

        private readonly List<FanShotInfo> _fanShots = new List<FanShotInfo>();
        private readonly List<HitMark> _hitMarks = new List<HitMark>();
        private Mesh _sphereMesh;
        private Mesh _cylinderMesh;
        private Mesh _coneMesh;       ///< 扇の当たる範囲（半径 1 の円すいと、球の一部のふた）
        private Mesh _capMesh;        ///< 扇の波面（半径 1 の球の一部）
        private float _meshHalfAngle = float.NaN;  ///< _coneMesh と _capMesh を作ったときの半角（度）
        private Material _beamMaterial;
        private Material _fanRangeMaterial;
        private Material _fanFrontMaterial;
        private Material _hitMaterial;
        private MaterialPropertyBlock _hitPropertyBlock;  ///< 当たった所の球の色を 1 つずつ変えるための入れ物（使い回す）

        /// @brief 当たり判定を出しているか
        public bool IsVisible { get; set; }

        /// @brief 出す・消すを切り替える
        [ShowInEventMenu]
        public void ToggleVisible()
        {
            IsVisible = !IsVisible;
        }

        //============================================================
        // Unity のメッセージ
        //============================================================

        private void Awake()
        {
            IsVisible = _isVisibleOnStart;
        }

        private void OnDestroy()
        {
            DestroyCreated(_sphereMesh);
            DestroyCreated(_cylinderMesh);
            DestroyCreated(_coneMesh);
            DestroyCreated(_capMesh);
            DestroyCreated(_beamMaterial);
            DestroyCreated(_fanRangeMaterial);
            DestroyCreated(_fanFrontMaterial);
            DestroyCreated(_hitMaterial);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && _toggleKey != Key.None && keyboard[_toggleKey].wasPressedThisFrame)
            {
                ToggleVisible();
            }
        }

        private void LateUpdate()
        {
            if (!IsVisible)
            {
                _hitMarks.Clear();
                return;
            }
            if (!EnsureResources())
            {
                return;
            }

            float now = Time.time;
            // 有効な WaveEmitter は、WaveEmitter が OnEnable / OnDisable で出し入れする一覧から取る（毎フレーム探さない）
            IReadOnlyList<WaveEmitter> emitters = WaveEmitter.ActiveEmitters;
            for (int emitterIndex = 0; emitterIndex < emitters.Count; ++emitterIndex)
            {
                WaveEmitter emitter = emitters[emitterIndex];
                DrawBeam(emitter);
                DrawFanShots(emitter, now);
                foreach (InfoBeamHit hit in emitter.LastHits)
                {
                    _hitMarks.Add(new HitMark(hit.Point, now));
                }
            }
            DrawHitMarks(now);
        }

        //============================================================
        // 内部の処理
        //============================================================

        /// @brief ビームの当たり判定（線ごとのカプセル）を描く
        private void DrawBeam(WaveEmitter emitter)
        {
            IReadOnlyList<Vector3> corners = emitter.BeamCorners;
            if (corners.Count < 2)
            {
                return;
            }
            float radius = Mathf.Max(MinBeamDrawRadius, emitter.BeamHitRadius);
            var renderParams = new RenderParams(_beamMaterial);
            float diameterScale = radius / PrimitiveRadius;  // 組み込みの球・円柱を、判定の半径に合わせる倍率
            Vector3 sphereScale = Vector3.one * diameterScale;
            for (int i = 0; i < corners.Count; ++i)
            {
                Graphics.RenderMesh(renderParams, _sphereMesh, 0, Matrix4x4.TRS(corners[i], Quaternion.identity, sphereScale));
                if (i + 1 >= corners.Count)
                {
                    break;
                }
                Vector3 delta = corners[i + 1] - corners[i];
                float length = delta.magnitude;
                if (length < MinSegmentLength)
                {
                    continue;
                }
                // 組み込みの円柱（高さ CylinderHeight、半径 PrimitiveRadius）を、線の真ん中に置いて、線の長さと判定の半径に合わせる
                Vector3 center = (corners[i] + corners[i + 1]) / 2.0f;
                Matrix4x4 matrix = Matrix4x4.TRS(center, Quaternion.FromToRotation(Vector3.up, delta / length),
                    new Vector3(diameterScale, length / CylinderHeight, diameterScale));
                Graphics.RenderMesh(renderParams, _cylinderMesh, 0, matrix);
            }
        }

        /// @brief 扇の 1 発の、当たる範囲（円すい）と波面（球の一部）を描く
        private void DrawFanShots(WaveEmitter emitter, float now)
        {
            emitter.GetFlyingFanShots(_fanShots);
            foreach (FanShotInfo shot in _fanShots)
            {
                if (shot.Range <= 0.0f || shot.Direction.sqrMagnitude < MinDirectionSqrMagnitude)
                {
                    continue;
                }
                EnsureFanMeshes(shot.HalfAngle);
                Vector3 up = Vector3.Cross(shot.Direction, shot.Up).sqrMagnitude > MinUpCrossSqrMagnitude ? shot.Up : Vector3.right;
                Quaternion rotation = Quaternion.LookRotation(shot.Direction, up);
                Graphics.RenderMesh(new RenderParams(_fanRangeMaterial), _coneMesh, 0,
                    Matrix4x4.TRS(shot.Origin, rotation, Vector3.one * shot.Range));
                float front = Mathf.Min(shot.GetFrontDistance(now), shot.Range);
                if (front > 0.0f)
                {
                    Graphics.RenderMesh(new RenderParams(_fanFrontMaterial), _capMesh, 0,
                        Matrix4x4.TRS(shot.Origin, rotation, Vector3.one * front));
                }
            }
        }

        /// @brief 当たった所の球を、だんだん薄くしながら描く
        private void DrawHitMarks(float now)
        {
            _hitMarks.RemoveAll(mark => now - mark.HitTime > _hitMarkSeconds);
            Vector3 scale = Vector3.one * (_hitMarkRadius / PrimitiveRadius);
            foreach (HitMark mark in _hitMarks)
            {
                float fade = _hitMarkSeconds > 0.0f ? 1.0f - (now - mark.HitTime) / _hitMarkSeconds : 1.0f;
                Color color = _hitColor;
                color.a *= fade;
                // 入れ物は 1 つを使い回す（RenderMesh は呼んだ時点の値を写すので、次の球で色を変えても前の球には効かない）
                _hitPropertyBlock.SetColor(ColorId, color);
                var renderParams = new RenderParams(_hitMaterial) { matProps = _hitPropertyBlock };
                Graphics.RenderMesh(renderParams, _sphereMesh, 0, Matrix4x4.TRS(mark.Point, Quaternion.identity, scale));
            }
        }

        /// @brief 描くのに使うメッシュとマテリアルを用意する。色は毎フレーム Inspector の値に合わせる
        /// @return 用意できたら true（シェーダーが見つからないときは false）
        private bool EnsureResources()
        {
            if (_beamMaterial == null)
            {
                Shader shader = Shader.Find(ShaderName);
                if (shader == null)
                {
                    return false;
                }
                _beamMaterial = CreateMaterial(shader, "WaveHitboxBeam");
                _fanRangeMaterial = CreateMaterial(shader, "WaveHitboxFanRange");
                _fanFrontMaterial = CreateMaterial(shader, "WaveHitboxFanFront");
                _hitMaterial = CreateMaterial(shader, "WaveHitboxHit");
                _sphereMesh = CopyPrimitiveMesh(PrimitiveType.Sphere);
                _cylinderMesh = CopyPrimitiveMesh(PrimitiveType.Cylinder);
                _hitPropertyBlock = new MaterialPropertyBlock();
            }
            _beamMaterial.color = _beamColor;
            _fanRangeMaterial.color = _fanRangeColor;
            _fanFrontMaterial.color = _fanFrontColor;
            _hitMaterial.color = _hitColor;
            return true;
        }

        /// @brief 頂点カラー × 色で描く、半透明・両面・奥行きを書かないマテリアルを作る（物に隠れた所は描かない）
        private static Material CreateMaterial(Shader shader, string name)
        {
            var material = new Material(shader) { name = name, hideFlags = HideFlags.HideAndDontSave };
            material.SetInt(SrcBlendId, (int)BlendMode.SrcAlpha);
            material.SetInt(DstBlendId, (int)BlendMode.OneMinusSrcAlpha);
            material.SetInt(CullId, (int)CullMode.Off);
            material.SetInt(ZWriteId, 0);
            material.renderQueue = (int)RenderQueue.Transparent;
            return material;
        }

        /// @brief Unity の組み込みの形のメッシュを写し、頂点カラーを白にする（シェーダーが頂点カラーに色を掛けるため）
        private static Mesh CopyPrimitiveMesh(PrimitiveType type)
        {
            GameObject primitive = GameObject.CreatePrimitive(type);
            Mesh mesh = Instantiate(primitive.GetComponent<MeshFilter>().sharedMesh);
            Destroy(primitive);
            mesh.name = "WaveHitbox" + type;
            mesh.hideFlags = HideFlags.HideAndDontSave;
            mesh.colors32 = CreateWhiteColors(mesh.vertexCount);
            return mesh;
        }

        /// @brief 扇の当たる範囲（円すいとふた）と、波面（ふただけ）のメッシュを、半角に合わせて作る（半径 1、+Z が出す向き）
        private void EnsureFanMeshes(float halfAngle)
        {
            if (_coneMesh != null && Mathf.Abs(halfAngle - _meshHalfAngle) < AngleRebuildEpsilon)
            {
                return;
            }
            DestroyCreated(_coneMesh);
            DestroyCreated(_capMesh);
            _coneMesh = BuildSectorMesh(halfAngle, true);
            _capMesh = BuildSectorMesh(halfAngle, false);
            _meshHalfAngle = halfAngle;
        }

        /// @brief 半径 1 の球の、+Z から半角以内の部分（ふた）を作る。withSide なら、出る位置（原点）から縁までの円すいの側面も足す
        private static Mesh BuildSectorMesh(float halfAngle, bool withSide)
        {
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            float maxPolar = halfAngle * Mathf.Deg2Rad;

            // ふた: 極（+Z）から縁まで、縦に CapRings 段、まわりに ConeSegments 区切り
            vertices.Add(Vector3.forward);
            for (int ring = 1; ring <= CapRings; ++ring)
            {
                float polar = maxPolar * ring / CapRings;
                for (int segment = 0; segment < ConeSegments; ++segment)
                {
                    vertices.Add(GetSpherePoint(polar, 2.0f * Mathf.PI * segment / ConeSegments));
                }
            }
            for (int segment = 0; segment < ConeSegments; ++segment)
            {
                int next = (segment + 1) % ConeSegments;
                triangles.Add(0);
                triangles.Add(1 + segment);
                triangles.Add(1 + next);
            }
            for (int ring = 1; ring < CapRings; ++ring)
            {
                int inner = 1 + (ring - 1) * ConeSegments;
                int outer = 1 + ring * ConeSegments;
                for (int segment = 0; segment < ConeSegments; ++segment)
                {
                    int next = (segment + 1) % ConeSegments;
                    triangles.Add(inner + segment);
                    triangles.Add(outer + segment);
                    triangles.Add(outer + next);
                    triangles.Add(inner + segment);
                    triangles.Add(outer + next);
                    triangles.Add(inner + next);
                }
            }

            // 側面: 原点から、ふたの縁まで
            if (withSide)
            {
                int apex = vertices.Count;
                vertices.Add(Vector3.zero);
                int rim = 1 + (CapRings - 1) * ConeSegments;
                for (int segment = 0; segment < ConeSegments; ++segment)
                {
                    int next = (segment + 1) % ConeSegments;
                    triangles.Add(apex);
                    triangles.Add(rim + next);
                    triangles.Add(rim + segment);
                }
            }

            var mesh = new Mesh { name = withSide ? "WaveHitboxFanCone" : "WaveHitboxFanFront", hideFlags = HideFlags.HideAndDontSave };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.colors32 = CreateWhiteColors(vertices.Count);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// @brief 半径 1 の球の上の点。polar は +Z からの角度、azimuth はまわりの角度（どちらもラジアン）
        private static Vector3 GetSpherePoint(float polar, float azimuth)
        {
            float sin = Mathf.Sin(polar);
            return new Vector3(sin * Mathf.Cos(azimuth), sin * Mathf.Sin(azimuth), Mathf.Cos(polar));
        }

        private static Color32[] CreateWhiteColors(int count)
        {
            var colors = new Color32[count];
            for (int i = 0; i < count; ++i)
            {
                colors[i] = new Color32(255, 255, 255, 255);
            }
            return colors;
        }

        private static void DestroyCreated(Object created)
        {
            if (created != null)
            {
                Destroy(created);
            }
        }

        /// @brief 当たった所の印
        private readonly struct HitMark
        {
            public HitMark(Vector3 point, float hitTime)
            {
                Point = point;
                HitTime = hitTime;
            }

            public Vector3 Point { get; }   ///< 当たった位置
            public float HitTime { get; }   ///< 当たった時刻
        }
    }
}
