using System;
using System.Collections.Generic;
using UnityEngine.Experimental.Rendering;
using UnityEngine;
using UnityEngine.Rendering;
namespace Echo.Echolocation
{
    internal sealed class EchoSurfaceMemory : IDisposable
    {
        private const int AxisCount = 3;
        private const float MinimumExtent = .01f;
        private const float BoundsTolerance = .0001f;
        private static readonly int MatrixId = Shader.PropertyToID("_EchoMemoryAxis");
        private static readonly int WorldToMemoryId = Shader.PropertyToID("_EchoWorldToMemory");
        private static readonly int StampId = Shader.PropertyToID("_EchoContactStamp");
        private static readonly int ResolutionId = Shader.PropertyToID("_EchoMemoryResolution");
        private readonly CommandBuffer _commands = new CommandBuffer { name = "Echo local surface memory" };
        private readonly EchoVolumeShaderData _waves = new EchoVolumeShaderData();
        private Material _material;
        private RenderTexture _target;
        private Bounds _bounds;
        public RenderTexture Texture { get; private set; }
        public RTHandle Handle { get; private set; }
        public Matrix4x4 WorldToMemory { get; private set; }
        public void Capture(EchoController controller, List<EchoRenderItem> items, int resolution, float now, bool hasReset, bool hasPossibleContact)
        {
            var root = items[0].EffectGroup is Component component ? component.transform : items[0].Renderer.transform;
            var bounds = GetLocalBounds(items, root);
            hasReset |= !Texture || Texture.width != resolution || (bounds.center - _bounds.center).sqrMagnitude > BoundsTolerance ||
                (bounds.size - _bounds.size).sqrMagnitude > BoundsTolerance;
            if (hasReset) Allocate(resolution);
            _bounds = bounds;
            WorldToMemory = Matrix4x4.Scale(new Vector3(1 / bounds.size.x, 1 / bounds.size.y, 1 / bounds.size.z)) *
                Matrix4x4.Translate(-bounds.min) * root.worldToLocalMatrix;
            // 残存だけの期間は時刻を読み出すだけでよく、表面の再描画は不要。
            if (hasPossibleContact) RecordCapture(controller, items, now);
        }
        private static Bounds GetLocalBounds(List<EchoRenderItem> items, Transform root)
        {
            var bounds = new Bounds(); bool hasPoint = false;
            foreach (var item in items)
            {
                var local = item.Renderer.localBounds;
                var matrix = root.worldToLocalMatrix * item.Renderer.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var corner = local.center + Vector3.Scale(local.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var point = matrix.MultiplyPoint3x4(corner);
                    if (!hasPoint) { bounds = new Bounds(point, Vector3.zero); hasPoint = true; } else bounds.Encapsulate(point);
                }
            }
            bounds.Expand(MinimumExtent);
            return bounds;
        }
        private void Allocate(int resolution)
        {
            ReleaseTextures();
            Texture = new RenderTexture(resolution, resolution, 0, RenderTextureFormat.RFloat, RenderTextureReadWrite.Linear)
            { name = "Echo contact timestamps", dimension = TextureDimension.Tex3D, volumeDepth = resolution,
                enableRandomWrite = true, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            Texture.Create();
            Handle = RTHandles.Alloc(Texture);
            var format = SystemInfo.IsFormatSupported(GraphicsFormat.R8_UNorm, GraphicsFormatUsage.Render)
                ? GraphicsFormat.R8_UNorm : GraphicsFormat.R8G8B8A8_UNorm;
            // 色を保存しない補助ターゲット。色空間の自動変換でR8_SRGBを要求しない。
            _target = new RenderTexture(new RenderTextureDescriptor(resolution, resolution, format, 0))
                { hideFlags = HideFlags.HideAndDontSave };
            _target.Create();
            _commands.Clear();
            // 明示的に全スライスを消し、未初期化の時刻を残存判定に使用しない。
            for (int slice = 0; slice < resolution; slice++)
            { _commands.SetRenderTarget(Texture, 0, CubemapFace.Unknown, slice); _commands.ClearRenderTarget(false, true, Color.clear); }
            Graphics.ExecuteCommandBuffer(_commands);
        }
        private void RecordCapture(EchoController controller, List<EchoRenderItem> items, float now)
        {
            if (!_material) _material = new Material(Resources.Load<Shader>("EchoSurfaceMemory")) { hideFlags = HideFlags.HideAndDontSave };
            _commands.Clear(); _waves.Capture(controller); _waves.Bind(_commands, controller.ShadowMaps.Texture);
            _commands.SetGlobalMatrix(WorldToMemoryId, WorldToMemory);
            _commands.SetGlobalFloat(StampId, now + 1);
            _commands.SetGlobalInteger(ResolutionId, Texture.width);
            _commands.SetRenderTarget(_target);
            _commands.SetRandomWriteTarget(1, Texture);
            for (int axis = 0; axis < AxisCount; axis++)
            {
                _commands.SetGlobalInteger(MatrixId, axis);
                foreach (var item in items)
                    for (int sub = 0; sub < item.SubMeshCount; sub++) _commands.DrawRenderer(item.Renderer, _material, sub);
            }
            _commands.ClearRandomWriteTargets();
            Graphics.ExecuteCommandBuffer(_commands);
        }
        private void ReleaseTextures()
        {
            Handle?.Release(); Handle = null;
            if (Texture) { Texture.Release(); UnityEngine.Object.Destroy(Texture); }
            if (_target) { _target.Release(); UnityEngine.Object.Destroy(_target); }
        }
        public void Dispose()
        { ReleaseTextures(); _commands.Release(); if (_material) UnityEngine.Object.Destroy(_material); }
    }
}
