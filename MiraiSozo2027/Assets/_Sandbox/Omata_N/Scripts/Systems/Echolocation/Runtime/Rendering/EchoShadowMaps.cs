// エコー中心を点光源と同様に扱い、6方向の最近接面までの距離をGPUへ描く。
// UnityのLightやCameraをHierarchyへ追加せず、実際の照明設定から独立して更新する。
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Echo.Echolocation
{
    public sealed class EchoShadowMaps
    {
        private const int FaceCount = 6;
        // 立方体の1面ずつ描くため、画角は90度で隙間なく全方向を覆う。
        private const float CubeFaceFieldOfView = 90;
        private const int FaceDepthBits = 24;
        // 近クリップは半径に比例させつつ、大きな半径でも手前の遮断物を切らない上限を設ける。
        private const float NearPlaneRatio = .01f;
        private const float MaximumNearPlane = .01f;
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

        /// <summary>前フレームと比べて遮断物が動いたかを判定するための記録。</summary>
        private struct BlockerState
        {
            public Matrix4x4 Matrix;
            public Bounds Bounds;
            public Mesh Mesh;
        }

        private readonly Dictionary<Renderer, Mesh> _meshes = new Dictionary<Renderer, Mesh>();
        private readonly Dictionary<Renderer, EchoTarget> _owners = new Dictionary<Renderer, EchoTarget>();
        public void Refresh()
        {
            _meshes.Clear(); _owners.Clear();
            foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
            {
                if (renderer.GetComponent<EchoRevealProxy>()) continue;
                Mesh mesh = renderer is SkinnedMeshRenderer skin ? skin.sharedMesh :
                    renderer.TryGetComponent<MeshFilter>(out var filter) ? filter.sharedMesh : null;
                if (!mesh) continue;
                _meshes.Add(renderer, mesh);
                _owners.Add(renderer, renderer.GetComponentInParent<EchoTarget>(true));
            }
        }
        private readonly List<Renderer> _blockers = new List<Renderer>();
        private readonly List<Vector3> _origins = new List<Vector3>();
        private readonly Plane[] _planes = new Plane[6];
        private CommandBuffer _commands;
        private readonly System.Diagnostics.Stopwatch _timer = new System.Diagnostics.Stopwatch();
        public Vector4[] Slots { get; } = new Vector4[EchoPulseTimeline.MaxPulses];
        public Matrix4x4[] Matrices { get; } = new Matrix4x4[EchoPulseTimeline.MaxPulses * FaceCount];

        // 静止した発生地点と遮断物の影は変わらないため、描画済みの面を使い回す。
        // ソナーは波ごとに発生地点が固定なので、毎フレーム全波×6面を描き直すと非常に重い。
        private Dictionary<Renderer, BlockerState> _blockerStates = new Dictionary<Renderer, BlockerState>();
        private Dictionary<Renderer, BlockerState> _nextBlockerStates = new Dictionary<Renderer, BlockerState>();
        private readonly List<Bounds> _changedBounds = new List<Bounds>();
        private readonly Vector3[] _slotOrigins = new Vector3[EchoPulseTimeline.MaxPulses];
        private readonly bool[] _isSlotAssigned = new bool[EchoPulseTimeline.MaxPulses];
        private readonly bool[] _isSlotUsed = new bool[EchoPulseTimeline.MaxPulses];
        private readonly int[] _originSlots = new int[EchoPulseTimeline.MaxPulses];
        private readonly bool[] _isFaceValid = new bool[EchoPulseTimeline.MaxPulses * FaceCount];
        private readonly int[] _faceDrawCalls = new int[EchoPulseTimeline.MaxPulses * FaceCount];
        private float _cachedFar = -1;

        private Material _material;
        private RenderTexture _atlas;
        private RenderTexture _faceTexture;
        private int _capacity;
        public RenderTexture Texture => _atlas;
        public bool IsReady { get; private set; }
        public int OriginCount => _origins.Count;
        public int BlockerCount => _blockers.Count;
        /// <summary>現在の影マップ全体を構成する描画回数。再利用した面の分も含む。</summary>
        public int DrawCalls { get; private set; }
        /// <summary>当フレームで描き直した面の数。静止時は0になる。</summary>
        public int UpdatedFaceCount { get; private set; }
        public double CpuMilliseconds { get; private set; }

        static void Destroy(Object target)
        {
            if (!target)
                return;
            if (Application.isPlaying)
                Object.Destroy(target);
            else
                Object.DestroyImmediate(target);
        }

        public void Clear()
        {
            IsReady = false;
            _origins.Clear();
            _blockers.Clear();
            DrawCalls = 0;
            UpdatedFaceCount = 0;
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
            InvalidateCache();
            _blockerStates.Clear();
        }

        void InvalidateCache()
        {
            System.Array.Clear(_isSlotAssigned, 0, _isSlotAssigned.Length);
            System.Array.Clear(_isFaceValid, 0, _isFaceValid.Length);
            System.Array.Clear(_faceDrawCalls, 0, _faceDrawCalls.Length);
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
            // 配列を作り直すと以前の面は失われる。
            InvalidateCache();
            _capacity = Mathf.Min(EchoPulseTimeline.MaxPulses, Mathf.NextPowerOfTwo(count));
            _atlas = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear)
            {
                name = "Echo Source Shadow Array",
                dimension = TextureDimension.Tex2DArray,
                volumeDepth = _capacity * FaceCount,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            _atlas.Create();
            _faceTexture = new RenderTexture(resolution, resolution, FaceDepthBits, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear)
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
            UpdatedFaceCount = 0;
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
                int index = _origins.IndexOf(frames[i].Origin);
                if (index < 0)
                {
                    index = _origins.Count;
                    _origins.Add(frames[i].Origin);
                }

                Slots[i] = new Vector4(index, 0, 0, 0);
            }

            Allocate(_origins.Count, settings.Shadow.Resolution);
            float near = Mathf.Min(MaximumNearPlane, settings.Radius * NearPlaneRatio), far = Mathf.Max(near + MaximumNearPlane, settings.Radius);
            if (far != _cachedFar)
            {
                InvalidateCache();
                _cachedFar = far;
            }

            AssignSlots();
            for (int i = 0; i < frames.Count; i++)
                Slots[i] = new Vector4(_originSlots[(int)Slots[i].x], 0, 0, 0);

            _blockers.Clear();
            _changedBounds.Clear();
            _nextBlockerStates.Clear();
            foreach (var pair in _meshes)
            {
                var renderer = pair.Key;
                if (!renderer) continue;
                if (!renderer.enabled || !renderer.gameObject.activeInHierarchy || (excluded && renderer.transform.IsChildOf(excluded)))
                    continue;
                var owner = _owners[renderer];
                var definition = owner ? owner.Definition : null;
                bool isBlocking = !owner || !owner.isActiveAndEnabled || !definition || definition.IsBlocking;
                if (!isBlocking)
                    continue;
                _blockers.Add(renderer);
                var state = new BlockerState { Matrix = renderer.localToWorldMatrix, Bounds = renderer.bounds, Mesh = pair.Value };
                _nextBlockerStates[renderer] = state;
                // スキンメッシュは変形するため、範囲内にある限り毎フレーム描き直す。
                if (!_blockerStates.TryGetValue(renderer, out var previous))
                    _changedBounds.Add(state.Bounds);
                else if (renderer is SkinnedMeshRenderer || previous.Mesh != state.Mesh || previous.Matrix != state.Matrix)
                {
                    _changedBounds.Add(previous.Bounds);
                    _changedBounds.Add(state.Bounds);
                }
            }

            // 消えた・遮断しなくなった物の影も、元の位置の面から取り除く。
            foreach (var pair in _blockerStates)
                if (!_nextBlockerStates.ContainsKey(pair.Key))
                    _changedBounds.Add(pair.Value.Bounds);
            (_blockerStates, _nextBlockerStates) = (_nextBlockerStates, _blockerStates);

            // Unityのネイティブ資源はMonoBehaviourのフィールド初期化中に作らない。
            _commands ??= new CommandBuffer
            {
                name = "Echo source shadows"
            };
            _commands.Clear();
            var projection = Matrix4x4.Perspective(CubeFaceFieldOfView, 1, near, far);
            for (int index = 0; index < _origins.Count; index++)
            {
                int slot = _originSlots[index];
                var origin = _origins[index];
                for (int face = 0; face < FaceCount; face++)
                {
                    int slice = slot * FaceCount + face;
                    var view = Matrix4x4.Scale(new Vector3(1, 1, -1)) * Matrix4x4.TRS(origin, Quaternion.LookRotation(_directions[face], _upDirections[face]), Vector3.one).inverse;
                    var vp = GL.GetGPUProjectionMatrix(projection, false) * view;
                    Matrices[slice] = vp;
                    GeometryUtility.CalculateFrustumPlanes(projection * view, _planes);
                    if (_isFaceValid[slice] && !HasChangeInside(_planes))
                        continue;
                    _commands.SetRenderTarget(_faceTexture);
                    _commands.SetViewport(new Rect(0, 0, settings.Shadow.Resolution, settings.Shadow.Resolution));
                    // ClearRenderTargetがReversed Zを処理するため、手動で深度を0へ反転しない。
                    _commands.ClearRenderTarget(true, true, new Color(far + 1, 0, 0, 0));
                    _commands.SetGlobalMatrix("_EchoShadowCaptureVP", vp);
                    _commands.SetGlobalVector("_EchoShadowCaptureOrigin", origin);
                    int drawCalls = 0;
                    foreach (var renderer in _blockers)
                    {
                        if (!GeometryUtility.TestPlanesAABB(_planes, renderer.bounds))
                            continue;
                        Mesh mesh = _meshes[renderer];
                        for (int sub = 0; sub < mesh.subMeshCount; sub++)
                        {
                            _commands.DrawRenderer(renderer, _material, sub, 0);
                            drawCalls++;
                        }
                    }

                    _commands.CopyTexture(_faceTexture, 0, 0, _atlas, slice, 0);
                    _faceDrawCalls[slice] = drawCalls;
                    _isFaceValid[slice] = true;
                    UpdatedFaceCount++;
                }
            }

            DrawCalls = 0;
            for (int index = 0; index < _origins.Count; index++)
                for (int face = 0; face < FaceCount; face++)
                    DrawCalls += _faceDrawCalls[_originSlots[index] * FaceCount + face];
            // 実際の描画はGPUに順序付きで送る。後続のURP描画は同じ距離マップを読む。
            if (UpdatedFaceCount > 0)
                Graphics.ExecuteCommandBuffer(_commands);
            IsReady = true;
            _timer.Stop();
            CpuMilliseconds = _timer.Elapsed.TotalMilliseconds;
        }

        /// <summary>前フレームと同じ発生地点には同じスライスを割り当て、描画済みの面を再利用する。</summary>
        void AssignSlots()
        {
            System.Array.Clear(_isSlotUsed, 0, _isSlotUsed.Length);
            for (int index = 0; index < _origins.Count; index++)
            {
                _originSlots[index] = -1;
                for (int slot = 0; slot < _capacity; slot++)
                    if (_isSlotAssigned[slot] && !_isSlotUsed[slot] && _slotOrigins[slot].Equals(_origins[index]))
                    {
                        _originSlots[index] = slot;
                        _isSlotUsed[slot] = true;
                        break;
                    }
            }

            for (int index = 0; index < _origins.Count; index++)
            {
                if (_originSlots[index] >= 0)
                    continue;
                int free = 0;
                while (_isSlotUsed[free])
                    free++;
                _originSlots[index] = free;
                _isSlotUsed[free] = true;
                _isSlotAssigned[free] = true;
                _slotOrigins[free] = _origins[index];
                for (int face = 0; face < FaceCount; face++)
                    _isFaceValid[free * FaceCount + face] = false;
            }
        }

        bool HasChangeInside(Plane[] planes)
        {
            foreach (var bounds in _changedBounds)
                if (GeometryUtility.TestPlanesAABB(planes, bounds))
                    return true;
            return false;
        }
    }
}
