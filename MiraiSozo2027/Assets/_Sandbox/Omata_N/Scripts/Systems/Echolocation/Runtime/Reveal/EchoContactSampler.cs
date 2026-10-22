using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace Echo.Echolocation
{
    /// <summary>カメラに依存しないGPU接触判定。1要求ずつ処理し、古い結果で上書きしない。</summary>
    internal sealed class EchoContactSampler : IDisposable
    {
        private static readonly int ContactMatrixId = Shader.PropertyToID("_EchoContactMatrix");
        private static readonly int ContactSourceId = Shader.PropertyToID("_EchoContactSource");
        private static readonly int ForwardsId = Shader.PropertyToID("_EchoForwards");
        private static readonly int OriginsId = Shader.PropertyToID("_EchoOrigins");
        private static readonly int PatchProjectionId = Shader.PropertyToID("_EchoPatchProjection");
        private static readonly int PulseCountId = Shader.PropertyToID("_EchoPulseCount");
        private static readonly int ShadowBiasId = Shader.PropertyToID("_EchoShadowBias");
        private static readonly int ShadowMapsId = Shader.PropertyToID("_EchoShadowMaps");
        private static readonly int ShadowMatricesId = Shader.PropertyToID("_EchoShadowMatrices");
        private static readonly int ShadowSlotsId = Shader.PropertyToID("_EchoShadowSlots");
        private static readonly int ShadowsEnabledId = Shader.PropertyToID("_EchoShadowsEnabled");
        private static readonly int VolumesId = Shader.PropertyToID("_EchoVolumes");
        private const int PatchResolution = 16;
        private readonly Vector4[] _origins = new Vector4[EchoPulseTimeline.MaxPulses];
        private readonly Vector4[] _forwards = new Vector4[EchoPulseTimeline.MaxPulses];
        private readonly Vector4[] _volumes = new Vector4[EchoPulseTimeline.MaxPulses];
        private readonly List<RenderTexture> _textures = new List<RenderTexture>();
        private Material _material;
        private CommandBuffer _commands;
        private AsyncGPUReadbackRequest _request;
        private bool _hasRequest;
        private int _grid;
        private int _count;
        public double SubmittedAt { get; private set; }
        public float SampleTime { get; private set; }
        public bool IsPending => _hasRequest;
        public bool IsSupported => SystemInfo.supportsAsyncGPUReadback &&
            SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.RFloat);
        public bool TryReceive(bool[] contacts, out bool hasError)
        {
            hasError = false;
            if (!_hasRequest || !_request.done) return false;
            _hasRequest = false;
            hasError = _request.hasError;
            if (!hasError)
            {
                var values = _request.GetData<float>();
                for (int i = 0; i < _count; i++) contacts[i] = values[i] > .5f;
            }
            return true;
        }
        private void Allocate(int count, int resolution)
        {
            int grid = Mathf.CeilToInt(Mathf.Sqrt(count));
            if (_textures.Count > 0 && _grid == grid && _textures[0].width == grid * resolution) { _count = count; return; }
            ReleaseTextures();
            _grid = grid; _count = count;
            for (int size = resolution; size >= 1; size /= 2)
            {
                var texture = new RenderTexture(grid * size, grid * size, 0, RenderTextureFormat.RFloat)
                { name = "Echo reveal contact", filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
                texture.Create(); _textures.Add(texture);
            }
        }
        public void Submit(EchoController controller, IReadOnlyList<Renderer> sources, IReadOnlyList<MeshRenderer> patches,
            bool isPartial, int resolution)
        {
            if (_hasRequest || !IsSupported) return;
            int count = isPartial ? patches.Count : 1;
            if (count == 0) return;
            Allocate(count, isPartial ? PatchResolution : Mathf.ClosestPowerOfTwo(Mathf.Clamp(resolution, 64, 1024)));
            if (!_material) _material = new Material(Resources.Load<Shader>("EchoRevealContact")) { hideFlags = HideFlags.HideAndDontSave };
            _commands ??= new CommandBuffer { name = "Echo reveal region contact" };
            _commands.Clear();
            BindWaves(controller);
            _commands.SetRenderTarget(_textures[0]);
            _commands.ClearRenderTarget(false, true, Color.clear);
            _commands.SetGlobalFloat(PatchProjectionId, isPartial ? 1 : 0);
            if (isPartial)
            {
                for (int i = 0; i < count; i++)
                {
                    _commands.SetViewport(new Rect(i % _grid * PatchResolution, i / _grid * PatchResolution, PatchResolution, PatchResolution));
                    _commands.DrawRenderer(patches[i], _material, 0, 0);
                }
            }
            else if (sources.Count > 0)
            {
                var bounds = sources[0].bounds;
                foreach (var source in sources) bounds.Encapsulate(source.bounds);
                float radius = Mathf.Max(.1f, bounds.extents.magnitude) * 1.01f;
                var projection = GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(-radius, radius, -radius, radius, .01f, radius * 4), true);
                _commands.SetViewport(new Rect(0, 0, _textures[0].width, _textures[0].height));
                for (int axis = 0; axis < 3; axis++)
                {
                    var forward = axis == 0 ? Vector3.forward : axis == 1 ? Vector3.right : Vector3.up;
                    var up = axis == 2 ? Vector3.forward : Vector3.up;
                    var view = Matrix4x4.Scale(new Vector3(1, 1, -1)) * Matrix4x4.TRS(bounds.center - forward * radius * 2,
                        Quaternion.LookRotation(forward, up), Vector3.one).inverse;
                    _commands.SetGlobalMatrix(ContactMatrixId, projection * view);
                    foreach (var source in sources)
                    {
                        var materials = EchoTargetRegistry.GetOriginalMaterials(source);
                        for (int s = 0; s < materials.Length; s++) _commands.DrawRenderer(source, _material, s, 0);
                    }
                }
            }
            for (int i = 1; i < _textures.Count; i++)
            {
                _commands.SetGlobalTexture(ContactSourceId, _textures[i - 1]);
                _commands.SetRenderTarget(_textures[i]);
                _commands.SetViewport(new Rect(0, 0, _textures[i].width, _textures[i].height));
                _commands.DrawProcedural(Matrix4x4.identity, _material, 1, MeshTopology.Triangles, 3);
            }
            Graphics.ExecuteCommandBuffer(_commands);
            _request = AsyncGPUReadback.Request(_textures[_textures.Count - 1], 0);
            _hasRequest = true;
            SubmittedAt = Time.realtimeSinceStartupAsDouble;
            SampleTime = controller.Settings.TimeMode == EchoTimeMode.Scaled ? Time.time : Time.unscaledTime;
        }
        private void BindWaves(EchoController controller)
        {
            var frames = controller.Frames;
            for (int i = 0; i < frames.Count; i++)
            {
                var frame = frames[i];
                _origins[i] = new Vector4(frame.Origin.x, frame.Origin.y, frame.Origin.z, frame.Angle);
                _forwards[i] = new Vector4(frame.Forward.x, frame.Forward.y, frame.Forward.z, Mathf.Cos(frame.Angle * .5f * Mathf.Deg2Rad));
                _volumes[i] = new Vector4(frame.OuterRadius, frame.EraseRadius, frame.State == EchoState.Hiding ? 1 : 0, frame.State == EchoState.Revealing ? 1 : 0);
            }
            _commands.SetGlobalInteger(PulseCountId, frames.Count);
            _commands.SetGlobalVectorArray(OriginsId, _origins);
            _commands.SetGlobalVectorArray(ForwardsId, _forwards);
            _commands.SetGlobalVectorArray(VolumesId, _volumes);
            var shadows = controller.ShadowMaps;
            _commands.SetGlobalInteger(ShadowsEnabledId, shadows.IsReady ? 1 : 0);
            _commands.SetGlobalFloat(ShadowBiasId, controller.Settings.Shadow.Bias);
            if (shadows.IsReady)
            {
                _commands.SetGlobalTexture(ShadowMapsId, shadows.Texture);
                _commands.SetGlobalVectorArray(ShadowSlotsId, shadows.Slots);
                _commands.SetGlobalMatrixArray(ShadowMatricesId, shadows.Matrices);
            }
        }
        private void ReleaseTextures()
        {
            foreach (var texture in _textures) { texture.Release(); UnityEngine.Object.Destroy(texture); }
            _textures.Clear();
        }
        public void Dispose()
        {
            // 終了時だけ待機し、GPUが参照中のTextureを解放しない。通常更新は非同期。
            if (_hasRequest) _request.WaitForCompletion();
            _hasRequest = false;
            _commands?.Release(); _commands = null;
            ReleaseTextures();
            if (_material) UnityEngine.Object.Destroy(_material);
        }
    }
}

