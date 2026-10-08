// シーン深度から表面位置を復元し、対象タグのない床にもスキャン範囲を表示する。
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Echo.Echolocation
{
    /// <summary>強調対象の有無に関係なく範囲を描く。タグ色の強調より前に合成する。</summary>
    public sealed class EchoRangeRenderPass : ScriptableRenderPass
    {
        private readonly Material _material;
        private readonly EchoShadowTexture _shadowTexture = new EchoShadowTexture();
        public void Dispose() => _shadowTexture.Dispose();
        /// <summary>専用マテリアルを受け取る。Featureが寿命を管理する。</summary>
        public EchoRangeRenderPass(Material material)
        {
            this._material = material;
            renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
        }

        static Color GetLinearColor(Color c) => QualitySettings.activeColorSpace == ColorSpace.Linear ? c.linear : c;
        /// <summary>現在のスキャン領域と設定を固定し、フルスクリーンと境界線を追加する。</summary>
        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frameData)
        {
            var camera = frameData.Get<UniversalCameraData>();
            var controller = EchoController.GetForCamera(camera.camera);
            if (!controller || controller.Frames.Count == 0 || controller.Settings.AngleDegrees <= 0)
                return;
            var display = controller.GetComponent<EchoRangeDisplay>();
            if (!display || !display.isActiveAndEnabled)
                return;
            var resources = frameData.Get<UniversalResourceData>();
            if (!resources.cameraDepthTexture.IsValid())
                return;
            using (var builder = graph.AddRasterRenderPass<EchoRangePassData>("Echo.Range", out EchoRangePassData data))
            {
                data.Material = _material;
                data.Waves.Capture(controller);
                data.IsSurfaceVisible = display.IsSurfaceVisible;
                data.IsBoundaryVisible = display.IsBoundaryVisible;
                data.Waves.Shadows = _shadowTexture.Import(graph, controller);
                if (data.Waves.Shadows.IsValid())
                    builder.UseTexture(data.Waves.Shadows, AccessFlags.Read);
                data.Mesh = display.IsBoundaryVisible ? display.GetBoundaryMesh(controller.Settings.AngleDegrees) : null;
                data.Fill = GetLinearColor(display.FillColor);
                data.Wave = GetLinearColor(display.WaveColor);
                data.Erase = GetLinearColor(display.EraseColor);
                data.Boundary = GetLinearColor(display.BoundaryColor);
                data.Width = display.WaveWidth;
                builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
                builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                builder.AllowGlobalStateModification(true);
                builder.SetRenderFunc((EchoRangePassData d, RasterGraphContext ctx) =>
                {
                    d.Waves.Bind(ctx.cmd);
                    ctx.cmd.SetGlobalVector("_EchoRangeFill", d.Fill);
                    ctx.cmd.SetGlobalVector("_EchoRangeWave", d.Wave);
                    ctx.cmd.SetGlobalVector("_EchoRangeErase", d.Erase);
                    ctx.cmd.SetGlobalFloat("_EchoRangeWidth", d.Width);
                    if (d.IsSurfaceVisible)
                        ctx.cmd.DrawProcedural(Matrix4x4.identity, d.Material, 0, MeshTopology.Triangles, 3);
                    if (d.IsBoundaryVisible && d.Mesh)
                    {
                        for (int i = 0; i < d.Waves.Count; i++)
                        {
                            var f = d.Waves.Frames[i];
                            if (f.OuterRadius <= 0)
                                continue;
                            ctx.cmd.SetGlobalInteger("_EchoBoundaryWave", i);
                            // ZTestの代わりに専用シェーダーで深度比較し、カメラの深度を書き換えない。
                            ctx.cmd.SetGlobalVector("_EchoRangeLine", d.Boundary);
                            ctx.cmd.DrawMesh(d.Mesh, Matrix4x4.TRS(f.Origin, Quaternion.LookRotation(f.Forward), Vector3.one * f.OuterRadius), d.Material, 0, 1);
                            if (f.State == EchoState.Hiding && f.EraseRadius > 0)
                            {
                                ctx.cmd.SetGlobalVector("_EchoRangeLine", d.Erase);
                                ctx.cmd.DrawMesh(d.Mesh, Matrix4x4.TRS(f.Origin, Quaternion.LookRotation(f.Forward), Vector3.one * f.EraseRadius), d.Material, 0, 1);
                            }
                        }
                    }
                });
            }
        }
    }
}
