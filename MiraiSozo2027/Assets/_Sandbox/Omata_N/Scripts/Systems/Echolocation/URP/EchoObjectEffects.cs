using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Echo.Echolocation
{
    // 波との接触をGPUで1画素へ集約する。CPU readbackを使わず同じフレームで全体を表示。
    internal sealed class EchoObjectEffects : IDisposable
    {
        sealed class History : IDisposable
        {
            public readonly RTHandle[] Textures = new RTHandle[2];
            public int Index;
            public float Time;
            public int Frame = -1;
            public EchoTimeMode TimeMode;
            public int EnableVersion;
            public EchoTagColorRule Rule;
            public float LatestPossibleContact;
            public History()
            {
                for (int i = 0; i < 2; i++)
                    Textures[i] = RTHandles.Alloc(1, 1, colorFormat: GraphicsFormat.R32G32B32A32_SFloat,
                        filterMode: FilterMode.Point, name: "Echo object lifetime");
            }
            public void Dispose() { foreach (var texture in Textures) texture.Release(); }
        }

        sealed class Data
        {
            public TextureHandle Source, Previous, Mask, Scene;
            public Material Material;
            public Vector4 Size, Timing;
            public EchoRenderItem Item;
            public EchoOcclusionMode Occlusion;
            public RendererListHandle[] RendererLists;
        }

        readonly Dictionary<(EchoController, UnityEngine.Object), History> _histories = new Dictionary<(EchoController, UnityEngine.Object), History>();
        readonly List<(EchoController, UnityEngine.Object)> _stale = new List<(EchoController, UnityEngine.Object)>();
        static readonly int HistoryId = Shader.PropertyToID("_EchoHistory");
        static readonly int SizeId = Shader.PropertyToID("_EchoSourceSize");
        static readonly int TimingId = Shader.PropertyToID("_EchoEffectTiming");
        static readonly int MaskId = Shader.PropertyToID("_EchoObjectMask");
        static readonly int SceneId = Shader.PropertyToID("_EchoSceneColor");
        static readonly int OpacityId = Shader.PropertyToID("_EchoOriginalOpacity");
        static readonly int OutlineColorId = Shader.PropertyToID("_EchoOutlineColor");
        static readonly int OutlineWidthId = Shader.PropertyToID("_EchoOutlineWidth");
        static readonly int ContactMatrixId = Shader.PropertyToID("_EchoContactMatrix");
        static readonly ShaderTagId LightMode = new ShaderTagId("LightMode");

        public void Dispose()
        {
            foreach (var history in _histories.Values) history.Dispose();
            _histories.Clear();
        }

        public void Prune()
        {
            _stale.Clear();
            foreach (var entry in _histories)
                if (!entry.Key.Item1 || !entry.Key.Item1.isActiveAndEnabled || !entry.Key.Item2 ||
                    entry.Value.Rule == null || !entry.Value.Rule.IsEnabled ||
                    (!entry.Value.Rule.OriginalEnabled && !entry.Value.Rule.OutlineEnabled) ||
                    !entry.Key.Item1.Settings.TagColorRules.Contains(entry.Value.Rule))
                    _stale.Add(entry.Key);
            foreach (var key in _stale) { _histories[key].Dispose(); _histories.Remove(key); }
        }

        static void Copy(RenderGraph graph, TextureHandle source, TextureHandle destination, Material composite)
        {
            using var builder = graph.AddRasterRenderPass<Data>("Echo.CopyBackground", out var data);
            data.Source = source;
            data.Material = composite;
            builder.UseTexture(source, AccessFlags.Read);
            builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
            builder.SetRenderFunc((Data d, RasterGraphContext ctx) =>
                Blitter.BlitTexture(ctx.cmd, d.Source, new Vector4(1, 1, 0, 0), d.Material, 6));
        }

        public void Record(RenderGraph graph, ContextContainer context, UniversalResourceData resources, EchoController controller,
            EchoRenderItem[] items, TextureDesc desc, TextureHandle accumulated, TextureHandle shadows, EchoUrpRenderBackend backend)
        {
            var item = items[0];
            var rule = item.Rule;
            var key = (controller, item.EffectGroup);
            float now = controller.Settings.TimeMode == EchoTimeMode.Scaled ? Time.time : Time.unscaledTime;
            var bounds = item.Renderer.bounds;
            foreach (var member in items) bounds.Encapsulate(member.Renderer.bounds);
            bool possibleContact = false;
            foreach (var wave in controller.Frames)
                if (EchoVolumeMath.Intersects(bounds, wave.Origin, wave.OuterRadius)) { possibleContact = true; break; }
            _histories.TryGetValue(key, out var history);
            if (!possibleContact && (history == null || history.EnableVersion != controller.EnableVersion ||
                now - history.LatestPossibleContact > Mathf.Max(rule.OriginalHoldDuration, rule.OutlineHoldDuration)))
            {
                if (history != null) { history.Dispose(); _histories.Remove(key); }
                return;
            }
            var maskDesc = desc;
            maskDesc.name = "Echo complete silhouette and contact";
            var mask = graph.CreateTexture(maskDesc);
            using (var builder = graph.AddRasterRenderPass<EchoHighlightDrawData>("Echo.ObjectContact", out var data))
            {
                data.Waves.Capture(controller);
                data.Waves.Shadows = shadows;
                data.Item = item;
                data.Items = items;
                data.Material = backend.Highlight;
                // Rは全体、Bはカメラ深度で見える部分。各機能が合成時に独立して選ぶ。
                data.Occlusion = EchoOcclusionMode.VisibleOnly;
                if (shadows.IsValid()) builder.UseTexture(shadows, AccessFlags.Read);
                if (resources.cameraDepthTexture.IsValid()) builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
                builder.SetRenderAttachment(mask, 0, AccessFlags.Write);
                builder.AllowGlobalStateModification(true);
                builder.SetRenderFunc((EchoHighlightDrawData d, RasterGraphContext ctx) =>
                {
                    d.Waves.Bind(ctx.cmd);
                    ctx.cmd.SetGlobalFloat("_EchoOcclusion", d.Occlusion == EchoOcclusionMode.VisibleOnly ? 1 : 0);
                    foreach (var member in d.Items)
                        if (member.Renderer)
                            for (int s = 0; s < member.SubMeshCount; s++) ctx.cmd.DrawRenderer(member.Renderer, d.Material, s, 1);
                });
            }

            // カメラ外・背面の接触も拾うため、物体全体を3軸方向から独立にラスタライズ。
            // Cull/ZTestを無効にし、重なる全三角形の接触を最大値で集約する。
            int contactResolution = rule.ContactResolution;
            var contact = graph.CreateTexture(new TextureDesc(contactResolution, contactResolution)
            {
                name = "Echo world-space object contact", colorFormat = GraphicsFormat.R16G16B16A16_SFloat,
                clearBuffer = true, clearColor = Color.clear
            });
            using (var builder = graph.AddRasterRenderPass<EchoHighlightDrawData>("Echo.WorldContact", out var data))
            {
                data.Waves.Capture(controller);
                data.Waves.Shadows = shadows;
                data.Items = items;
                data.Material = backend.Highlight;
                float radius = Mathf.Max(.1f, bounds.extents.magnitude) * 1.01f;
                data.ContactMatrices = new Matrix4x4[3];
                var projection = GL.GetGPUProjectionMatrix(Matrix4x4.Ortho(-radius, radius, -radius, radius, .01f, radius * 4), true);
                for (int axis = 0; axis < 3; axis++)
                {
                    Vector3 forward = axis == 0 ? Vector3.forward : axis == 1 ? Vector3.right : Vector3.up;
                    Vector3 up = axis == 2 ? Vector3.forward : Vector3.up;
                    var view = Matrix4x4.Scale(new Vector3(1, 1, -1)) * Matrix4x4.TRS(bounds.center - forward * radius * 2,
                        Quaternion.LookRotation(forward, up), Vector3.one).inverse;
                    data.ContactMatrices[axis] = projection * view;
                }
                if (shadows.IsValid()) builder.UseTexture(shadows, AccessFlags.Read);
                builder.SetRenderAttachment(contact, 0, AccessFlags.Write);
                builder.AllowGlobalStateModification(true);
                builder.SetRenderFunc((EchoHighlightDrawData d, RasterGraphContext ctx) =>
                {
                    d.Waves.Bind(ctx.cmd);
                    foreach (var matrix in d.ContactMatrices)
                    {
                        ctx.cmd.SetGlobalMatrix(ContactMatrixId, matrix);
                        foreach (var member in d.Items)
                            if (member.Renderer)
                                for (int s = 0; s < member.SubMeshCount; s++) ctx.cmd.DrawRenderer(member.Renderer, d.Material, s, 2);
                    }
                });
            }

            var reduced = contact;
            int width = contactResolution, height = contactResolution;
            while (width > 1 || height > 1)
            {
                var size = new Vector4(width, height, 1f / width, 1f / height);
                width = (width + 3) / 4;
                height = (height + 3) / 4;
                var reduceDesc = new TextureDesc(width, height) { colorFormat = GraphicsFormat.R16G16B16A16_SFloat, name = "Echo contact reduction" };
                var next = graph.CreateTexture(reduceDesc);
                using (var builder = graph.AddRasterRenderPass<Data>("Echo.ReduceContact", out var data))
                {
                    data.Source = reduced;
                    data.Material = backend.Composite;
                    data.Size = size;
                    builder.UseTexture(reduced, AccessFlags.Read);
                    builder.SetRenderAttachment(next, 0, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc((Data d, RasterGraphContext ctx) =>
                    {
                        ctx.cmd.SetGlobalVector(SizeId, d.Size);
                        Blitter.BlitTexture(ctx.cmd, d.Source, new Vector4(1, 1, 0, 0), d.Material, 2);
                    });
                }
                reduced = next;
            }

            if (history == null) _histories.Add(key, history = new History());
            bool reset = history.Frame < 0 || history.TimeMode != controller.Settings.TimeMode || history.EnableVersion != controller.EnableVersion || history.Rule != rule;
            var previous = graph.ImportTexture(history.Textures[history.Index]);
            history.Index = 1 - history.Index;
            var current = graph.ImportTexture(history.Textures[history.Index]);
            using (var builder = graph.AddRasterRenderPass<Data>("Echo.UpdateObjectLifetime", out var data))
            {
                data.Source = reduced;
                data.Previous = previous;
                data.Material = backend.Composite;
                data.Timing = new Vector4(reset ? 0 : Mathf.Max(0, now - history.Time), rule.OriginalHoldDuration, rule.OutlineHoldDuration, reset ? 1 : 0);
                builder.UseTexture(reduced, AccessFlags.Read);
                builder.UseTexture(previous, AccessFlags.Read);
                builder.SetRenderAttachment(current, 0, AccessFlags.Write);
                builder.AllowGlobalStateModification(true);
                builder.SetRenderFunc((Data d, RasterGraphContext ctx) =>
                {
                    ctx.cmd.SetGlobalTexture(HistoryId, d.Previous);
                    ctx.cmd.SetGlobalVector(TimingId, d.Timing);
                    Blitter.BlitTexture(ctx.cmd, d.Source, new Vector4(1, 1, 0, 0), d.Material, 3);
                });
            }
            history.Time = now;
            history.Frame = Time.frameCount;
            history.TimeMode = controller.Settings.TimeMode;
            history.EnableVersion = controller.EnableVersion;
            history.Rule = rule;
            if (possibleContact) history.LatestPossibleContact = now;

            if (rule.OriginalEnabled && rule.HideOriginalOutsideEcho)
            {
                var originalDesc = desc;
                originalDesc.name = "Echo original material background";
                var scene = graph.CreateTexture(originalDesc);
                originalDesc.name = "Echo original material rendering";
                var original = graph.CreateTexture(originalDesc);
                Copy(graph, resources.activeColorTexture, scene, backend.Composite);
                Copy(graph, scene, original, backend.Composite);
                var depthDesc = desc;
                depthDesc.name = "Echo original material depth";
                depthDesc.colorFormat = GraphicsFormat.None;
                depthDesc.depthBufferBits = DepthBits.Depth32;
                var depth = graph.CreateTexture(depthDesc);
                var renderingData = context.Get<UniversalRenderingData>();
                var cameraData = context.Get<UniversalCameraData>();
                var lightData = context.Get<UniversalLightData>();
                var lists = new List<RendererListHandle>();
                foreach (var member in items)
                {
                    var originals = EchoTargetRegistry.GetOriginalMaterials(member.Renderer);
                    var invisible = EchoTargetRegistry.GetInvisibleMaterials(member.Renderer);
                    if (invisible == null) continue;
                    for (int s = 0; s < originals.Length; s++)
                    {
                        int pass = FindForwardPass(originals[s]);
                        if (pass < 0) continue;
                        var drawing = RenderingUtils.CreateDrawingSettings(new ShaderTagId("EchoOriginal"), renderingData,
                            cameraData, lightData, SortingCriteria.CommonOpaque);
                        drawing.overrideMaterial = originals[s];
                        drawing.overrideMaterialPassIndex = pass;
                        var range = new RenderQueueRange { lowerBound = invisible[s].renderQueue, upperBound = invisible[s].renderQueue };
                        var filtering = new FilteringSettings(range);
                        lists.Add(graph.CreateRendererList(new RendererListParams(renderingData.cullResults, drawing, filtering)));
                    }
                }
                using (var builder = graph.AddRasterRenderPass<Data>("Echo.DrawOriginalMaterials", out var data))
                {
                    data.Item = item;
                    data.RendererLists = lists.ToArray();
                    foreach (var list in data.RendererLists) builder.UseRendererList(list);
                    builder.UseAllGlobalTextures(true);
                    builder.SetRenderAttachment(original, 0, AccessFlags.ReadWrite);
                    builder.SetRenderAttachmentDepth(depth, AccessFlags.Write);
                    builder.SetRenderFunc((Data d, RasterGraphContext ctx) =>
                    {
                        foreach (var list in d.RendererLists) ctx.cmd.DrawRendererList(list);
                    });
                }
                using (var builder = graph.AddRasterRenderPass<Data>("Echo.RevealOriginal", out var data))
                {
                    data.Source = original;
                    data.Scene = scene;
                    data.Mask = mask;
                    data.Occlusion = EchoTagColorRule.ResolveOcclusion(rule.OriginalOcclusion, controller.Settings.OcclusionMode);
                    data.Previous = current;
                    data.Material = backend.Composite;
                    data.Item = item;
                    builder.UseTexture(original, AccessFlags.Read);
                    builder.UseTexture(scene, AccessFlags.Read);
                    builder.UseTexture(mask, AccessFlags.Read);
                    builder.UseTexture(current, AccessFlags.Read);
                    builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc((Data d, RasterGraphContext ctx) =>
                    {
                        ctx.cmd.SetGlobalTexture(HistoryId, d.Previous);
                        ctx.cmd.SetGlobalTexture(MaskId, d.Mask);
                        ctx.cmd.SetGlobalFloat("_EchoOriginalOcclusion", d.Occlusion == EchoOcclusionMode.VisibleOnly ? 1 : 0);
                        ctx.cmd.SetGlobalTexture(SceneId, d.Scene);
                        ctx.cmd.SetGlobalFloat(OpacityId, d.Item.Rule.OriginalOpacity);
                        Blitter.BlitTexture(ctx.cmd, d.Source, new Vector4(1, 1, 0, 0), d.Material, 5);
                    });
                }
            }

            if (rule.OutlineEnabled)
            {
                using var builder = graph.AddRasterRenderPass<Data>("Echo.WholeObjectOutline", out var data);
                data.Source = mask;
                data.Previous = current;
                data.Material = backend.Composite;
                data.Item = item;
                data.Size = new Vector4(desc.width, desc.height, 1f / desc.width, 1f / desc.height);
                data.Occlusion = EchoTagColorRule.ResolveOcclusion(rule.OutlineOcclusion, controller.Settings.OcclusionMode);
                builder.UseTexture(mask, AccessFlags.Read);
                builder.UseTexture(current, AccessFlags.Read);
                builder.SetRenderAttachment(accumulated, 0, AccessFlags.ReadWrite);
                builder.AllowGlobalStateModification(true);
                builder.SetRenderFunc((Data d, RasterGraphContext ctx) =>
                {
                    var c = d.Item.Rule.OutlineColor;
                    if (QualitySettings.activeColorSpace == ColorSpace.Linear) c = c.linear;
                    ctx.cmd.SetGlobalTexture(HistoryId, d.Previous);
                    ctx.cmd.SetGlobalVector(SizeId, d.Size);
                    ctx.cmd.SetGlobalVector(OutlineColorId, c);
                    ctx.cmd.SetGlobalFloat(OutlineWidthId, d.Item.Rule.OutlineWidth);
                    ctx.cmd.SetGlobalFloat("_EchoOutlineOcclusion", d.Occlusion == EchoOcclusionMode.VisibleOnly ? 1 : 0);
                    Blitter.BlitTexture(ctx.cmd, d.Source, new Vector4(1, 1, 0, 0), d.Material, 4);
                });
            }
        }

        static int FindForwardPass(Material material)
        {
            if (!material) return -1;
            for (int pass = 0; pass < material.passCount; pass++)
            {
                var tag = material.shader.FindPassTagValue(pass, LightMode).name;
                // LightMode省略はSRPDefaultUnlit。URP標準Unlitもこの形式。
                if (tag == "UniversalForward" || tag == "UniversalForwardOnly" || tag == "SRPDefaultUnlit" || string.IsNullOrEmpty(tag)) return pass;
            }
            return -1;
        }
    }
}
