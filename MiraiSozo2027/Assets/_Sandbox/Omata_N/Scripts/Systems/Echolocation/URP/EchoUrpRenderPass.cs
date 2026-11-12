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
        private readonly EchoObjectEffects _objectEffects = new EchoObjectEffects();
        // 毎フレームの確保を避けるため、対象ごとのリストと並べ替え用の一覧は使い回す。
        private readonly Dictionary<Object, List<EchoRenderItem>> _effectGroups = new Dictionary<Object, List<EchoRenderItem>>();
        private readonly List<List<EchoRenderItem>> _sortedGroups = new List<List<EchoRenderItem>>();
        private readonly List<Object> _emptyGroups = new List<Object>();
        private readonly System.Comparison<List<EchoRenderItem>> _compareFarthestFirst;
        private Vector3 _cameraPosition;
        public void Dispose() { _shadowTexture.Dispose(); _objectEffects.Dispose(); }
        public void PruneObjectEffects() => _objectEffects.Prune();
        // Hidden/Echo/Compositeの、蓄積した色を画面へ合成するパス。
        private const int CompositePass = 0;
        private static readonly Vector4 IdentityScaleBias = new Vector4(1, 1, 0, 0);
        /// <summary>パイプライン固有リソースを受け取る。</summary>
        public EchoUrpRenderPass(EchoUrpRenderBackend backend)
        {
            this._backend = backend;
            _compareFarthestFirst = CompareFarthestFirst;
        }

        // 奥の対象から描き、手前の対象の色を後から重ねる。
        private int CompareFarthestFirst(List<EchoRenderItem> first, List<EchoRenderItem> second) =>
            (second[0].Renderer.bounds.center - _cameraPosition).sqrMagnitude.CompareTo((first[0].Renderer.bounds.center - _cameraPosition).sqrMagnitude);

        /// <summary>各カメラのリソース依存を明示し、Render Graphに寿命を任せる。</summary>
        public override void RecordRenderGraph(RenderGraph graph, ContextContainer context)
        {
            _objectEffects.Prune();
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
            foreach (var group in _effectGroups.Values)
                group.Clear();
            foreach (var item in controller.RenderItems)
                if (item.Renderer && item.Renderer.enabled && item.Renderer.gameObject.activeInHierarchy && item.Rule.HasAnyEffect)
                {
                    if (!_effectGroups.TryGetValue(item.EffectGroup, out var group))
                        _effectGroups.Add(item.EffectGroup, group = new List<EchoRenderItem>());
                    group.Add(item);
                }
            // 対象がいなくなったリストは捨て、辞書が増え続けないようにする。
            _emptyGroups.Clear();
            _sortedGroups.Clear();
            foreach (var pair in _effectGroups)
                if (pair.Value.Count == 0) _emptyGroups.Add(pair.Key);
                else _sortedGroups.Add(pair.Value);
            foreach (var key in _emptyGroups)
                _effectGroups.Remove(key);
            _cameraPosition = camera.camera.transform.position;
            _sortedGroups.Sort(_compareFarthestFirst);
            foreach (var group in _sortedGroups)
                _objectEffects.Record(graph, context, resources, controller, group, desc, color, paths, _backend);
            using (var builder = graph.AddRasterRenderPass<EchoCompositeData>("Echo.Composite", out EchoCompositeData data))
            {
                data.Source = color;
                data.Material = _backend.Composite;
                builder.UseTexture(color, AccessFlags.Read);
                builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                builder.SetRenderFunc((EchoCompositeData passData, RasterGraphContext ctx) => Blitter.BlitTexture(ctx.cmd, passData.Source, IdentityScaleBias, passData.Material, CompositePass));
            }
        }
    }
}
