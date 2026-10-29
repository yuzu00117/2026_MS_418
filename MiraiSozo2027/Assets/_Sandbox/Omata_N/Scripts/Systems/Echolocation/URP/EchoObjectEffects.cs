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
            public EchoOriginalRevealMode RevealMode;
            public float LatestPossibleContact;
            public EchoSurfaceMemory Memory;
            public History()
            {
                for (int i = 0; i < 2; i++)
                    Textures[i] = RTHandles.Alloc(1, 1, colorFormat: GraphicsFormat.R32G32B32A32_SFloat,
                        filterMode: FilterMode.Point, name: "Echo object lifetime");
            }
            public void Dispose() { Memory?.Dispose(); foreach (var texture in Textures) texture.Release(); }
        }

        sealed class Data
        {
            public TextureHandle Source, Previous, Mask, Scene;
            public Material Material;
            public Vector4 Size, Timing;
            public EchoRenderItem Item;
            public EchoOcclusionMode Occlusion;
            public readonly List<RendererListHandle> RendererLists = new List<RendererListHandle>();
            public bool IsPartial;
            public float Time;
            public Vector4 Effect;
            public Vector4 Persistence;
            public Rect Scissor;
        }

        readonly Dictionary<(EchoController, UnityEngine.Object), History> _histories = new Dictionary<(EchoController, UnityEngine.Object), History>();
        readonly List<(EchoController, UnityEngine.Object)> _stale = new List<(EchoController, UnityEngine.Object)>();
        private const float ContactBoundsPadding = 1.01f;
        private const float MinimumContactRadius = .1f;
        private const int ReductionFactor = 4;
        // Hidden/Echo/Highlightのパス番号。
        private const int ObjectMaskPass = 1;
        private const int WorldContactPass = 2;
        // Hidden/Echo/Compositeのパス番号。
        private const int ReducePass = 2;
        private const int HistoryPass = 3;
        private const int OutlinePass = 4;
        private const int OriginalPass = 5;
        private const int CopyPass = 6;
        private const int FillPass = 7;
        private const int MinimumContactResolution = 64;
        private const int MaximumContactResolution = 1024;
        // TAAのジッターと境界の丸めを吸収する余白（画素）。
        private const float ScreenRectPadding = 4;
        // これ以下のwはカメラの背後またはほぼ真横で、投影すると反転する。
        private const float MinimumClipW = 1e-5f;
        // 全画面コピー用。拡大縮小とずらしを行わない。
        private static readonly Vector4 IdentityScaleBias = new Vector4(1, 1, 0, 0);
        private static readonly int EffectId = Shader.PropertyToID("_EchoEffect");
        private static readonly int PersistenceId = Shader.PropertyToID("_EchoPersistence");
        private static readonly int SurfaceMemoryId = Shader.PropertyToID("_EchoSurfaceMemory");
        private static readonly int HasSurfaceMemoryId = Shader.PropertyToID("_EchoHasSurfaceMemory");
        private static readonly int WorldToMemoryId = Shader.PropertyToID("_EchoWorldToMemory");
        private static readonly int ContactTimeId = Shader.PropertyToID("_EchoContactTime");
        private static readonly int RegionId = Shader.PropertyToID("_EchoContactRegion");
        private static readonly int OcclusionId = Shader.PropertyToID("_EchoOcclusion");
        static readonly int HistoryId = Shader.PropertyToID("_EchoHistory");
        static readonly int SizeId = Shader.PropertyToID("_EchoSourceSize");
        static readonly int TimingId = Shader.PropertyToID("_EchoEffectTiming");
        static readonly int MaskId = Shader.PropertyToID("_EchoObjectMask");
        static readonly int SceneId = Shader.PropertyToID("_EchoSceneColor");
        static readonly int OriginalOcclusionId = Shader.PropertyToID("_EchoOriginalOcclusion");
        static readonly int OutlineOcclusionId = Shader.PropertyToID("_EchoOutlineOcclusion");
        static readonly int OpacityId = Shader.PropertyToID("_EchoOriginalOpacity");
        static readonly int OutlineColorId = Shader.PropertyToID("_EchoOutlineColor");
        static readonly int OutlineWidthId = Shader.PropertyToID("_EchoOutlineWidth");
        static readonly int ContactMatrixId = Shader.PropertyToID("_EchoContactMatrix");
        static readonly ShaderTagId LightMode = new ShaderTagId("LightMode");
        static readonly ShaderTagId OriginalShaderTag = new ShaderTagId("EchoOriginal");

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
                    !entry.Value.Rule.HasAnyEffect)
                    _stale.Add(entry.Key);
            foreach (var key in _stale) { _histories[key].Dispose(); _histories.Remove(key); }
        }

        static void Copy(RenderGraph graph, TextureHandle source, TextureHandle destination, Material composite, Rect scissor)
        {
            using var builder = graph.AddRasterRenderPass<Data>("Echo.CopyBackground", out var data);
            data.Source = source;
            data.Material = composite;
            data.Scissor = scissor;
            builder.UseTexture(source, AccessFlags.Read);
            builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
            builder.SetRenderFunc((Data passData, RasterGraphContext ctx) => BlitInRect(ctx, passData, CopyPass));
        }

        // 画面全体の合成は対象の投影範囲の外では何も変えないため、その範囲だけを処理する。
        // 対象数×全画面だった負荷が、対象の画面上の大きさに比例する負荷になる。
        static void BlitInRect(RasterGraphContext ctx, Data passData, int pass)
        {
            ctx.cmd.EnableScissorRect(passData.Scissor);
            Blitter.BlitTexture(ctx.cmd, passData.Source, IdentityScaleBias, passData.Material, pass);
            ctx.cmd.DisableScissorRect();
        }

        /// <summary>物体の境界をカメラへ投影した画素範囲。画面外ならfalse。</summary>
        static bool TryGetScreenRect(UniversalCameraData camera, Bounds bounds, int width, int height, out Rect rect)
        {
            rect = new Rect(0, 0, width, height);
            var viewProjection = camera.GetProjectionMatrix() * camera.GetViewMatrix();
            var planes = GeometryUtility.CalculateFrustumPlanes(viewProjection);
            if (!GeometryUtility.TestPlanesAABB(planes, bounds))
                return false;
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue), max = new Vector2(float.MinValue, float.MinValue);
            for (int i = 0; i < 8; i++)
            {
                var corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var clip = viewProjection * new Vector4(corner.x, corner.y, corner.z, 1);
                // カメラの背後へ回り込む角があると投影が反転するため、全画面のまま扱う。
                if (clip.w <= MinimumClipW)
                    return true;
                var ndc = new Vector2(clip.x / clip.w, clip.y / clip.w);
                min = Vector2.Min(min, ndc);
                max = Vector2.Max(max, ndc);
            }

            float xMin = Mathf.Clamp(Mathf.Floor((min.x * .5f + .5f) * width - ScreenRectPadding), 0, width);
            float yMin = Mathf.Clamp(Mathf.Floor((min.y * .5f + .5f) * height - ScreenRectPadding), 0, height);
            float xMax = Mathf.Clamp(Mathf.Ceil((max.x * .5f + .5f) * width + ScreenRectPadding), 0, width);
            float yMax = Mathf.Clamp(Mathf.Ceil((max.y * .5f + .5f) * height + ScreenRectPadding), 0, height);
            if (xMax <= xMin || yMax <= yMin)
                return false;
            rect = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
            return true;
        }

        public void Record(RenderGraph graph, ContextContainer context, UniversalResourceData resources, EchoController controller,
            List<EchoRenderItem> items, TextureDesc desc, TextureHandle accumulated, TextureHandle shadows, EchoUrpRenderBackend backend)
        {
            var item = items[0];
            var rule = item.Rule;
            bool isPartial = rule.RevealMode == EchoOriginalRevealMode.ContactRegion;
            var key = (controller, item.EffectGroup);
            float now = controller.Settings.TimeMode == EchoTimeMode.Scaled ? Time.time : Time.unscaledTime;
            var bounds = item.Renderer.bounds;
            foreach (var member in items) bounds.Encapsulate(member.Renderer.bounds);
            bool hasPossibleContact = false;
            foreach (var wave in controller.Frames)
                if (EchoVolumeMath.Intersects(bounds, wave.Origin, wave.OuterRadius)) { hasPossibleContact = true; break; }
            _histories.TryGetValue(key, out var history);
            // スクリプトから表示状態を参照している対象だけ、GPUの接触結果を読み戻す。
            var tracked = item.EffectGroup as EchoTarget;
            if (tracked && !tracked.IsRevealTracked) tracked = null;
            if (!hasPossibleContact && (history == null || history.EnableVersion != controller.EnableVersion ||
                now - history.LatestPossibleContact > rule.MaximumPersistence))
            {
                if (history != null) { history.Dispose(); _histories.Remove(key); }
                if (tracked) tracked.ReportNoContact(rule.MaximumPersistence, controller.Settings.TimeMode);
                return;
            }
            if (history == null) _histories.Add(key, history = new History());
            // 画面外でも接触と残存時間は更新する。省略するのは画面上の表示だけ。
            bool isOnScreen = TryGetScreenRect(context.Get<UniversalCameraData>(), bounds, desc.width, desc.height, out var screenRect);
            bool hasMemoryReset = history.Frame < 0 || history.TimeMode != controller.Settings.TimeMode || history.EnableVersion != controller.EnableVersion || history.Rule != rule;
            if (rule.NeedsSurfaceMemory)
            {
                history.Memory ??= new EchoSurfaceMemory();
                history.Memory.Capture(controller, items, rule.SurfaceMemoryResolution, now, hasMemoryReset, hasPossibleContact);
            }
            else if (history.Memory != null) { history.Memory.Dispose(); history.Memory = null; }
            var mask = TextureHandle.nullHandle;
            if (isOnScreen)
            {
                var maskDesc = desc;
                maskDesc.name = "Echo complete silhouette and contact";
                mask = graph.CreateTexture(maskDesc);
                RecordObjectMask(graph, resources, controller, items, history, backend, mask, desc, shadows, now);
            }

            // 接触部分だけの表示でも「どこかに届いたか」は全体の接触判定でしか分からないため、追跡中は常に行う。
            var reduced = hasPossibleContact && (rule.NeedsWholeObjectContact || tracked)
                ? RecordWholeObjectContact(graph, controller, items, bounds, shadows, backend)
                : graph.defaultResources.blackTexture;
            RecordLifetime(graph, controller, rule, history, reduced, backend, hasPossibleContact, now, out var current);
            if (tracked) RecordReadback(graph, current, history.Textures[history.Index],
                tracked.GetReadbackCallback(rule.MaximumPersistence, controller.Settings.TimeMode));
            if (!isOnScreen) return;
            RecordScreenEffects(graph, context, resources, controller, items, desc, accumulated, backend, mask, current, screenRect, now);
        }

        sealed class ReadbackData
        {
            public RTHandle Texture;
            public Action<AsyncGPUReadbackRequest> Callback;
        }

        // 1画素だけを非同期で読むため、GPUの完了を待たない。結果は数フレーム後にEchoTargetへ届く。
        private static void RecordReadback(RenderGraph graph, TextureHandle current, RTHandle texture, Action<AsyncGPUReadbackRequest> callback)
        {
            using var builder = graph.AddUnsafePass<ReadbackData>("Echo.ReadRevealState", out var data);
            data.Texture = texture;
            data.Callback = callback;
            builder.UseTexture(current, AccessFlags.Read);
            builder.AllowPassCulling(false);
            builder.SetRenderFunc((ReadbackData passData, UnsafeGraphContext ctx) => ctx.cmd.RequestAsyncReadback(passData.Texture.rt, passData.Callback));
        }

        private static void RecordObjectMask(RenderGraph graph, UniversalResourceData resources, EchoController controller, List<EchoRenderItem> items,
            History history, EchoUrpRenderBackend backend, TextureHandle mask, TextureDesc desc, TextureHandle shadows, float now)
        {
            var item = items[0];
            var maskDepthDesc = desc;
            maskDepthDesc.colorFormat = GraphicsFormat.None;
            maskDepthDesc.depthBufferBits = DepthBits.Depth32;
            maskDepthDesc.name = "Echo visible surface depth";
            var maskDepth = graph.CreateTexture(maskDepthDesc);
            using (var builder = graph.AddRasterRenderPass<EchoHighlightDrawData>("Echo.ObjectContact", out var data))
            {
                data.Waves.Capture(controller);
                data.Waves.Shadows = shadows;
                data.Item = item;
                data.Items = items;
                data.Material = backend.Highlight;
                data.ContactTime = now;
                if (history.Memory != null)
                {
                    data.SurfaceMemory = graph.ImportTexture(history.Memory.Handle);
                    data.WorldToMemory = history.Memory.WorldToMemory;
                    builder.UseTexture(data.SurfaceMemory, AccessFlags.Read);
                }
                // Rは全体、Bはカメラ深度で見える部分。各機能が合成時に独立して選ぶ。
                data.Occlusion = EchoOcclusionMode.VisibleOnly;
                if (shadows.IsValid()) builder.UseTexture(shadows, AccessFlags.Read);
                if (resources.cameraDepthTexture.IsValid()) builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
                builder.SetRenderAttachment(mask, 0, AccessFlags.Write);
                builder.SetRenderAttachmentDepth(maskDepth, AccessFlags.Write);
                builder.AllowGlobalStateModification(true);
                builder.SetRenderFunc((EchoHighlightDrawData passData, RasterGraphContext ctx) =>
                {
                    passData.Waves.Bind(ctx.cmd);
                    ctx.cmd.SetGlobalFloat(HasSurfaceMemoryId, passData.SurfaceMemory.IsValid() ? 1 : 0);
                    ctx.cmd.SetGlobalFloat(ContactTimeId, passData.ContactTime);
                    if (passData.SurfaceMemory.IsValid())
                    {
                        ctx.cmd.SetGlobalTexture(SurfaceMemoryId, passData.SurfaceMemory);
                        ctx.cmd.SetGlobalMatrix(WorldToMemoryId, passData.WorldToMemory);
                    }
                    ctx.cmd.SetGlobalFloat(OcclusionId, passData.Occlusion == EchoOcclusionMode.VisibleOnly ? 1 : 0);
                    foreach (var member in passData.Items)
                        if (member.Renderer)
                            for (int subMesh = 0; subMesh < member.SubMeshCount; subMesh++) ctx.cmd.DrawRenderer(member.Renderer, passData.Material, subMesh, ObjectMaskPass);
                });
            }
        }

        private static void RecordLifetime(RenderGraph graph, EchoController controller, EchoTagColorRule rule,
            History history, TextureHandle reduced, EchoUrpRenderBackend backend, bool hasPossibleContact, float now, out TextureHandle current)
        {
            bool isResetRequired = history.Frame < 0 || history.TimeMode != controller.Settings.TimeMode || history.EnableVersion != controller.EnableVersion || history.Rule != rule || history.RevealMode != rule.RevealMode;
            var previous = graph.ImportTexture(history.Textures[history.Index]);
            history.Index = 1 - history.Index;
            current = graph.ImportTexture(history.Textures[history.Index]);
            using (var builder = graph.AddRasterRenderPass<Data>("Echo.UpdateObjectLifetime", out var data))
            {
                data.Source = reduced;
                data.Previous = previous;
                data.Material = backend.Composite;
                data.Timing = new Vector4(now, 0, 0, isResetRequired ? 1 : 0);
                builder.UseTexture(reduced, AccessFlags.Read);
                builder.UseTexture(previous, AccessFlags.Read);
                builder.SetRenderAttachment(current, 0, AccessFlags.Write);
                builder.AllowGlobalStateModification(true);
                builder.SetRenderFunc((Data passData, RasterGraphContext ctx) =>
                {
                    ctx.cmd.SetGlobalTexture(HistoryId, passData.Previous);
                    ctx.cmd.SetGlobalVector(TimingId, passData.Timing);
                    Blitter.BlitTexture(ctx.cmd, passData.Source, IdentityScaleBias, passData.Material, HistoryPass);
                });
            }
            history.Time = now;
            history.Frame = Time.frameCount;
            history.TimeMode = controller.Settings.TimeMode;
            history.EnableVersion = controller.EnableVersion;
            history.Rule = rule;
            history.RevealMode = rule.RevealMode;
            if (hasPossibleContact) history.LatestPossibleContact = now;
        }

        private static void RecordScreenEffects(RenderGraph graph, ContextContainer context, UniversalResourceData resources, EchoController controller,
            List<EchoRenderItem> items, TextureDesc desc, TextureHandle accumulated, EchoUrpRenderBackend backend, TextureHandle mask,
            TextureHandle current, Rect screenRect, float now)
        {
            var item = items[0];
            var rule = item.Rule;
            if (rule.IsOriginalEnabled && rule.IsOriginalHiddenOutsideEcho)
            {
                var originalMask = mask;
                var originalDesc = desc;
                originalDesc.name = "Echo original material background";
                var scene = graph.CreateTexture(originalDesc);
                originalDesc.name = "Echo original material rendering";
                var original = graph.CreateTexture(originalDesc);
                Copy(graph, resources.activeColorTexture, scene, backend.Composite, screenRect);
                Copy(graph, scene, original, backend.Composite, screenRect);
                var depthDesc = desc;
                depthDesc.name = "Echo original material depth";
                depthDesc.colorFormat = GraphicsFormat.None;
                depthDesc.depthBufferBits = DepthBits.Depth32;
                var depth = graph.CreateTexture(depthDesc);
                var renderingData = context.Get<UniversalRenderingData>();
                var cameraData = context.Get<UniversalCameraData>();
                var lightData = context.Get<UniversalLightData>();
                using (var builder = graph.AddRasterRenderPass<Data>("Echo.DrawOriginalMaterials", out var data))
                {
                    data.Item = item;
                    // パスのデータはRender Graphが使い回すため、リストを空にして詰め直す。
                    data.RendererLists.Clear();
                    foreach (var member in items)
                    {
                        var originals = EchoTargetRegistry.GetOriginalMaterials(member.Renderer);
                        var invisible = EchoTargetRegistry.GetInvisibleMaterials(member.Renderer);
                        if (invisible == null) continue;
                        for (int subMesh = 0; subMesh < originals.Length; subMesh++)
                        {
                            int pass = FindForwardPass(originals[subMesh]);
                            if (pass < 0) continue;
                            var drawing = RenderingUtils.CreateDrawingSettings(OriginalShaderTag, renderingData,
                                cameraData, lightData, SortingCriteria.CommonOpaque);
                            drawing.overrideMaterial = originals[subMesh];
                            drawing.overrideMaterialPassIndex = pass;
                            var range = new RenderQueueRange { lowerBound = invisible[subMesh].renderQueue, upperBound = invisible[subMesh].renderQueue };
                            var filtering = new FilteringSettings(range);
                            data.RendererLists.Add(graph.CreateRendererList(new RendererListParams(renderingData.cullResults, drawing, filtering)));
                        }
                    }
                    foreach (var list in data.RendererLists) builder.UseRendererList(list);
                    builder.UseAllGlobalTextures(true);
                    builder.SetRenderAttachment(original, 0, AccessFlags.ReadWrite);
                    builder.SetRenderAttachmentDepth(depth, AccessFlags.Write);
                    builder.SetRenderFunc((Data passData, RasterGraphContext ctx) =>
                    {
                        foreach (var list in passData.RendererLists) ctx.cmd.DrawRendererList(list);
                    });
                }
                using (var builder = graph.AddRasterRenderPass<Data>("Echo.RevealOriginal", out var data))
                {
                    data.Source = original;
                    data.Scene = scene;
                    data.Mask = originalMask;
                    SetEffect(data, rule.Original, now);
                    data.Occlusion = EchoTagColorRule.ResolveOcclusion(rule.OriginalOcclusion, controller.Settings.OcclusionMode);
                    data.Previous = current;
                    data.Material = backend.Composite;
                    data.Item = item;
                    data.Scissor = screenRect;
                    builder.UseTexture(original, AccessFlags.Read);
                    builder.UseTexture(scene, AccessFlags.Read);
                    builder.UseTexture(originalMask, AccessFlags.Read);
                    builder.UseTexture(current, AccessFlags.Read);
                    builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.Write);
                    builder.AllowGlobalStateModification(true);
                    builder.SetRenderFunc((Data passData, RasterGraphContext ctx) =>
                    {
                        ctx.cmd.SetGlobalTexture(HistoryId, passData.Previous);
                        ctx.cmd.SetGlobalTexture(MaskId, passData.Mask);
                        ctx.cmd.SetGlobalFloat(OriginalOcclusionId, passData.Occlusion == EchoOcclusionMode.VisibleOnly ? 1 : 0);
                        ctx.cmd.SetGlobalTexture(SceneId, passData.Scene);
                        BindEffect(passData, ctx);
                        ctx.cmd.SetGlobalFloat(OpacityId, passData.Item.Rule.OriginalOpacity);
                        BlitInRect(ctx, passData, OriginalPass);
                    });
                }
            }

            if (rule.IsOutlineEnabled)
            {
                using var builder = graph.AddRasterRenderPass<Data>("Echo.WholeObjectOutline", out var data);
                data.Source = mask;
                SetEffect(data, rule.Outline, now);
                data.Previous = current;
                data.Material = backend.Composite;
                data.Item = item;
                data.Size = new Vector4(desc.width, desc.height, 1f / desc.width, 1f / desc.height);
                data.Occlusion = EchoTagColorRule.ResolveOcclusion(rule.OutlineOcclusion, controller.Settings.OcclusionMode);
                data.Scissor = screenRect;
                builder.UseTexture(mask, AccessFlags.Read);
                builder.UseTexture(current, AccessFlags.Read);
                builder.SetRenderAttachment(accumulated, 0, AccessFlags.ReadWrite);
                builder.AllowGlobalStateModification(true);
                builder.SetRenderFunc((Data passData, RasterGraphContext ctx) =>
                {
                    var outlineColor = passData.Item.Rule.OutlineColor;
                    if (QualitySettings.activeColorSpace == ColorSpace.Linear) outlineColor = outlineColor.linear;
                    ctx.cmd.SetGlobalTexture(HistoryId, passData.Previous);
                    BindEffect(passData, ctx);
                    ctx.cmd.SetGlobalVector(SizeId, passData.Size);
                    ctx.cmd.SetGlobalVector(OutlineColorId, outlineColor);
                    ctx.cmd.SetGlobalFloat(OutlineWidthId, passData.Item.Rule.OutlineWidth);
                    ctx.cmd.SetGlobalFloat(OutlineOcclusionId, passData.Occlusion == EchoOcclusionMode.VisibleOnly ? 1 : 0);
                    BlitInRect(ctx, passData, OutlinePass);
                });
            }
            if (rule.Fill.IsEnabled) RecordFill(graph, mask, current, accumulated, item, backend.Composite, now, screenRect);
        }


        private static TextureHandle RecordWholeObjectContact(RenderGraph graph, EchoController controller,
            List<EchoRenderItem> items, Bounds bounds, TextureHandle shadows, EchoUrpRenderBackend backend)
        {
            var rule = items[0].Rule;
            // カメラ外・背面の接触も拾うため、物体全体を3軸方向から独立にラスタライズ。
            // Cull/ZTestを無効にし、重なる全三角形の接触を最大値で集約する。
            int contactResolution = Mathf.ClosestPowerOfTwo(Mathf.Clamp(rule.ContactResolution, MinimumContactResolution, MaximumContactResolution));
            TextureHandle contact;
            {
                contact = graph.CreateTexture(new TextureDesc(contactResolution, contactResolution)
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
                    float radius = Mathf.Max(MinimumContactRadius, bounds.extents.magnitude) * ContactBoundsPadding;
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
                    builder.SetRenderFunc((EchoHighlightDrawData passData, RasterGraphContext ctx) =>
                    {
                        passData.Waves.Bind(ctx.cmd);
                        foreach (var matrix in passData.ContactMatrices)
                        {
                            ctx.cmd.SetGlobalMatrix(ContactMatrixId, matrix);
                            foreach (var member in passData.Items)
                                if (member.Renderer)
                                    for (int subMesh = 0; subMesh < member.SubMeshCount; subMesh++) ctx.cmd.DrawRenderer(member.Renderer, passData.Material, subMesh, WorldContactPass);
                        }
                    });
                }

            }
            var reduced = contact;
            int width = contactResolution, height = contactResolution;
            while (width > 1 || height > 1)
            {
                var size = new Vector4(width, height, 1f / width, 1f / height);
                width = (width + ReductionFactor - 1) / ReductionFactor;
                height = (height + ReductionFactor - 1) / ReductionFactor;
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
                    builder.SetRenderFunc((Data passData, RasterGraphContext ctx) =>
                    {
                        ctx.cmd.SetGlobalVector(SizeId, passData.Size);
                        Blitter.BlitTexture(ctx.cmd, passData.Source, IdentityScaleBias, passData.Material, ReducePass);
                    });
                }
                reduced = next;
            }

            return reduced;
        }

        private static void SetEffect(Data data, EchoEffectSettings settings, float now)
        {
            data.Effect = new Vector4(settings.IsEnabled ? 1 : 0, settings.IsThroughWalls ? 1 : 0,
                settings.DisplayScope == EchoOriginalRevealMode.WholeObject ? 1 : 0, now);
            data.Persistence = new Vector4(settings.HasPersistence ? 1 : 0, settings.PersistenceSeconds,
                settings.DisplayScope == EchoOriginalRevealMode.WholeObject ? 1 : 0, 0);
        }
        private static void BindEffect(Data data, RasterGraphContext context)
        {
            context.cmd.SetGlobalVector(EffectId, data.Effect);
            context.cmd.SetGlobalVector(PersistenceId, data.Persistence);
        }
        private static void RecordFill(RenderGraph graph, TextureHandle mask, TextureHandle history,
            TextureHandle accumulated, EchoRenderItem item, Material composite, float now, Rect scissor)
        {
            using var builder = graph.AddRasterRenderPass<Data>("Echo.TagFill", out var data);
            data.Source = mask; data.Previous = history; data.Item = item; data.Material = composite; data.Scissor = scissor;
            SetEffect(data, item.Rule.Fill, now);
            builder.UseTexture(mask, AccessFlags.Read); builder.UseTexture(history, AccessFlags.Read);
            builder.SetRenderAttachment(accumulated, 0, AccessFlags.ReadWrite);
            builder.AllowGlobalStateModification(true);
            builder.SetRenderFunc((Data passData, RasterGraphContext ctx) =>
            {
                BindEffect(passData, ctx); ctx.cmd.SetGlobalTexture(HistoryId, passData.Previous);
                var color = QualitySettings.activeColorSpace == ColorSpace.Linear ? passData.Item.Rule.Color.linear : passData.Item.Rule.Color;
                ctx.cmd.SetGlobalVector(OutlineColorId, color);
                BlitInRect(ctx, passData, FillPass);
            });
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
