// エコー中心を点光源と同様に扱い、6方向の最近接面までの距離をGPUへ描く。
// UnityのLightやCameraをHierarchyへ追加せず、実際の照明設定から独立して更新する。
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Echo.Echolocation
{
    public sealed class EchoShadowMaps
    {
        private static readonly Vector3[] _directions =
        {
            Vector3.right,
            Vector3.left,
            Vector3.up,
            Vector3.down,
            Vector3.forward,
            Vector3.back
        };
        private static readonly Vector3[] _upDirections =
        {
            Vector3.up,
            Vector3.up,
            Vector3.forward,
            Vector3.back,
            Vector3.up,
            Vector3.up
        };
        private readonly List<Renderer> _blockers = new List<Renderer>();
        private readonly List<Vector3> _origins = new List<Vector3>();
        private readonly Plane[] _planes = new Plane[6];
        private CommandBuffer _commands;
        private readonly System.Diagnostics.Stopwatch _timer = new System.Diagnostics.Stopwatch();
        public Vector4[] Slots { get; } = new Vector4[EchoPulseTimeline.MaxPulses];
        public Matrix4x4[] Matrices { get; } = new Matrix4x4[EchoPulseTimeline.MaxPulses * 6];

        private Material _material;
        private RenderTexture _atlas;
        private RenderTexture _faceTexture;
        private int _capacity;
        public RenderTexture Texture => _atlas;
        public bool IsReady { get; private set; }
        public int OriginCount => _origins.Count;
        public int BlockerCount => _blockers.Count;
        public int DrawCalls { get; private set; }
        public double CpuMilliseconds { get; private set; }

        static void Destroy(Object o)
        {
            if (!o)
                return;
            if (Application.isPlaying)
                Object.Destroy(o);
            else
                Object.DestroyImmediate(o);
        }

        public void Clear()
        {
            IsReady = false;
            _origins.Clear();
            _blockers.Clear();
            DrawCalls = 0;
            CpuMilliseconds = 0;
            _commands?.Release();
            _commands = null;
            if (_atlas)
                _atlas.Release();
            if (_faceTexture)
                _faceTexture.Release();
            Destroy(_atlas);
            Destroy(_faceTexture);
            Destroy(_material);
            _atlas = _faceTexture = null;
            _material = null;
            _capacity = 0;
        }

        void Allocate(int count, int resolution)
        {
            if (_atlas && _atlas.width == resolution && _capacity >= count)
                return;
            if (_atlas)
                _atlas.Release();
            if (_faceTexture)
                _faceTexture.Release();
            Destroy(_atlas);
            Destroy(_faceTexture);
            _capacity = Mathf.Min(EchoPulseTimeline.MaxPulses, Mathf.NextPowerOfTwo(count));
            _atlas = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear)
            {
                name = "Echo Source Shadow Array",
                dimension = TextureDimension.Tex2DArray,
                volumeDepth = _capacity * 6,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            _atlas.Create();
            _faceTexture = new RenderTexture(resolution, resolution, 24, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear)
            {
                name = "Echo Shadow Face",
                filterMode = FilterMode.Point,
                hideFlags = HideFlags.HideAndDontSave
            };
            _faceTexture.Create();
        }

        /// <summary>位置の異なる生存波ごとに更新する。遮断物の移動やタグ変更は同じフレームに反映する。</summary>
        public void Update(IReadOnlyList<EchoFrameData> frames, EchoSettings settings, Transform excluded)
        {
            _timer.Restart();
            IsReady = false;
            DrawCalls = 0;
            if (!settings.Shadow.IsEnabled || frames.Count == 0 || settings.Radius <= 0)
            {
                if (_atlas)
                    Clear();
                return;
            }

            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                return;
            if (!_material)
            {
                var shader = Resources.Load<Shader>("EchoShadowDepth");
                if (!shader)
                {
                    Debug.LogError("エコー：影用シェーダー EchoShadowDepth がありません。");
                    return;
                }

                _material = new Material(shader)
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
            }

            _origins.Clear();
            for (int i = 0; i < frames.Count; i++)
            {
                int slot = _origins.IndexOf(frames[i].Origin);
                if (slot < 0)
                {
                    slot = _origins.Count;
                    _origins.Add(frames[i].Origin);
                }

                Slots[i] = new Vector4(slot, 0, 0, 0);
            }

            Allocate(_origins.Count, settings.Shadow.Resolution);
            _blockers.Clear();
            foreach (var r in Object.FindObjectsByType<Renderer>())
            {
                if (!r.enabled || !r.gameObject.activeInHierarchy || (excluded && r.transform.IsChildOf(excluded)))
                    continue;
                if ((settings.Shadow.Layers.value & (1 << r.gameObject.layer)) == 0 || !settings.Shadow.IsBlockingTag(r.tag))
                    continue;
                if (r is SkinnedMeshRenderer skin && skin.sharedMesh)
                    _blockers.Add(r);
                else if (r is MeshRenderer && r.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh)
                    _blockers.Add(r);
            }

            // Unityのネイティブ資源はMonoBehaviourのフィールド初期化中に作らない。
            _commands ??= new CommandBuffer
            {
                name = "Echo source shadows"
            };
            _commands.Clear();
            float near = Mathf.Min(.01f, settings.Radius * .01f), far = Mathf.Max(near + .01f, settings.Radius);
            var projection = Matrix4x4.Perspective(90, 1, near, far);
            for (int slot = 0; slot < _origins.Count; slot++)
                for (int face = 0; face < 6; face++)
                {
                    var origin = _origins[slot];
                    var view = Matrix4x4.Scale(new Vector3(1, 1, -1)) * Matrix4x4.TRS(origin, Quaternion.LookRotation(_directions[face], _upDirections[face]), Vector3.one).inverse;
                    var vp = GL.GetGPUProjectionMatrix(projection, false) * view;
                    Matrices[slot * 6 + face] = vp;
                    GeometryUtility.CalculateFrustumPlanes(projection * view, _planes);
                    _commands.SetRenderTarget(_faceTexture);
                    _commands.SetViewport(new Rect(0, 0, settings.Shadow.Resolution, settings.Shadow.Resolution));
                    // ClearRenderTargetがReversed Zを処理するため、手動で深度を0へ反転しない。
                    _commands.ClearRenderTarget(true, true, new Color(far + 1, 0, 0, 0));
                    _commands.SetGlobalMatrix("_EchoShadowCaptureVP", vp);
                    _commands.SetGlobalVector("_EchoShadowCaptureOrigin", origin);
                    foreach (var r in _blockers)
                    {
                        if (!GeometryUtility.TestPlanesAABB(_planes, r.bounds))
                            continue;
                        Mesh mesh = r is SkinnedMeshRenderer s ? s.sharedMesh : r.GetComponent<MeshFilter>().sharedMesh;
                        for (int sub = 0; sub < mesh.subMeshCount; sub++)
                        {
                            _commands.DrawRenderer(r, _material, sub, 0);
                            DrawCalls++;
                        }
                    }

                    _commands.CopyTexture(_faceTexture, 0, 0, _atlas, slot * 6 + face, 0);
                }

            // 実際の描画はGPUに順序付きで送る。後続のURP描画は同じ距離マップを読む。
            Graphics.ExecuteCommandBuffer(_commands);
            IsReady = true;
            _timer.Stop();
            CpuMilliseconds = _timer.Elapsed.TotalMilliseconds;
        }
    }
}
