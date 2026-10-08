// Rendererごとに深度を分離して強調色を蓄積する。対象同士で奥を隠さない。
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering;

namespace Echo.Echolocation
{
    /// <summary>壁裏の対象もRegistryから明示的に描くRender Graphパス。</summary>
    public sealed class EchoUrpRenderPass : ScriptableRenderPass
    {
        private readonly EchoUrpRenderBackend _backend;
        private readonly EchoShadowTexture _shadowTexture = new EchoShadowTexture();
        public void Dispose() => _shadowTexture.Dispose();
        private static readonly int _colorId = Shader.PropertyToID("_EchoTint");
        private static readonly int _modeId = Shader.PropertyToID("_EchoOcclusion");
        /// <summary>パイプライン固有リソースを受け取る。</summary>
        public EchoUrpRenderPass(EchoUrpRenderBackend backend)
        {
            this._backend = backend;
        }

        /// <summary>各カメラのリソース依存を明示し、Render Graphに寿命を任せる。</summary>
        public override void RecordRenderGraph(RenderGraph graph, ContextContainer context)
        {
            var camera = context.Get<UniversalCameraData>();
            var controller = EchoController.GetForCamera(camera.camera);
            if (!controller || controller.RenderItems.Count == 0)
                return;
            var resources = context.Get<UniversalResourceData>();
            var desc = graph.GetTextureDesc(resources.activeColorTexture);
            desc.name = "Echo accumulated highlights";
            desc.colorFormat = GraphicsFormat.R16G16B16A16_SFloat;
            desc.depthBufferBits = DepthBits.None;
            desc.msaaSamples = MSAASamples.None;
            desc.clearBuffer = true;
            desc.clearColor = Color.clear;
            var color = graph.CreateTexture(desc);
            var paths = _shadowTexture.Import(graph, controller);
            foreach (var item in controller.RenderItems)
            {
                // 各Rendererで深度を初期化する。他対象との重なりは後段の色合成に任せる。
                var objectDesc = desc;
                objectDesc.name = "Echo object highlight";
                var objectColor = graph.CreateTexture(objectDesc);
                objectDesc.name = "Echo object depth";
                objectDesc.colorFormat = GraphicsFormat.None;
                objectDesc.depthBufferBits = DepthBits.Depth32;
                var depth = graph.CreateTexture(objectDesc);
                using (var builder = graph.AddRasterRenderPass<EchoHighlightDrawData>("Echo.Draw", out EchoHighlightDrawData data))
                {
                    data.Waves.Capture(controller);
                    data.Occlusion = controller.Settings.OcclusionMode;
                    data.Item = item;
                    data.Material = _backend.Highlight;
                    data.Waves.Shadows = paths;
                    if (paths.IsValid())
                        builder.UseTexture(paths, AccessFlags.Read);
                    builder.SetRenderAttachment(objectColor, 0, AccessFlags.Write);
                    builder.SetRenderAttachmentDepth(depth, AccessFlags.Write);
                    if (resources.cameraDepthTexture.IsValid())
                        builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc((EchoHighlightDrawData d, RasterGraphContext ctx) =>
                    {
                        d.Waves.Bind(ctx.cmd);
                        ctx.cmd.SetGlobalFloat(_modeId, d.Occlusion == EchoOcclusionMode.VisibleOnly ? 1 : 0);
                        var renderItem = d.Item;
                        if (renderItem.Renderer)
                        {
                            Color c = QualitySettings.activeColorSpace == ColorSpace.Linear ? renderItem.Color.linear : renderItem.Color;
                            ctx.cmd.SetGlobalVector(_colorId, new Vector4(c.r, c.g, c.b, renderItem.Color.a));
                            for (int s = 0; s < renderItem.SubMeshCount; s++)
                                ctx.cmd.DrawRenderer(renderItem.Renderer, d.Material, s, 0);
                        }
                    });
                }

                using (var builder = graph.AddRasterRenderPass<EchoCompositeData>("Echo.Accumulate", out EchoCompositeData data))
                {
                    data.Source = objectColor;
                    data.Material = _backend.Composite;
                    builder.UseTexture(objectColor, AccessFlags.Read);
                    builder.SetRenderAttachment(color, 0, AccessFlags.ReadWrite);
                    builder.SetRenderFunc((EchoCompositeData d, RasterGraphContext ctx) => Blitter.BlitTexture(ctx.cmd, d.Source, new Vector4(1, 1, 0, 0), d.Material, 1));
                }
            }

            using (var builder = graph.AddRasterRenderPass<EchoCompositeData>("Echo.Composite", out EchoCompositeData data))
            {
                data.Source = color;
                data.Material = _backend.Composite;
                builder.UseTexture(color, AccessFlags.Read);
                builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                builder.SetRenderFunc((EchoCompositeData d, RasterGraphContext ctx) => Blitter.BlitTexture(ctx.cmd, d.Source, new Vector4(1, 1, 0, 0), d.Material, 0));
            }
        }
    }
}
