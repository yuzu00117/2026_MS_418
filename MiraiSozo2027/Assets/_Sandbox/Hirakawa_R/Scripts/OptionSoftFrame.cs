using UnityEngine;
using UnityEngine.UI;

namespace MiraiSozo.Options
{
    /// <summary>パネルに重ねる柔らかいSF調の枠装飾（細い枠線・外側の淡い光・縁取り・内側の線）。色と幅はOptionMenuThemeで変更できる。</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class OptionSoftFrame : MaskableGraphic
    {
        protected override void OnEnable()
        {
            base.OnEnable();
            OptionMenuTheme.Changed += SetVerticesDirty;
        }

        protected override void OnDisable()
        {
            OptionMenuTheme.Changed -= SetVerticesDirty;
            base.OnDisable();
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            var theme = OptionMenuTheme.Current;
            var rect = rectTransform.rect;
            float radius = theme.PanelRadius;
            // 外側へ向けて消えていく淡い光。
            OptionSoftShapes.AddRoundedRing(vertices, rect, radius, theme.OutlineWidth, theme.OutlineWidth + theme.HaloWidth,
                theme.HaloColor, OptionSoftShapes.GetColorWithAlpha(theme.HaloColor, 0f));
            // 輪郭を締める細い枠線。
            OptionSoftShapes.AddRoundedRing(vertices, rect, radius, 0f, theme.OutlineWidth, theme.OutlineColor, theme.OutlineColor);
            // ガラスの縁のような縁取りと、内側の細い線。
            OptionSoftShapes.AddRoundedRing(vertices, rect, radius, -theme.RimWidth, 0f, theme.RimColor, theme.RimColor);
            float inner = -theme.InnerLineOffset;
            OptionSoftShapes.AddRoundedRing(vertices, rect, radius, inner - theme.InnerLineWidth, inner, theme.InnerLineColor, theme.InnerLineColor);
        }
    }
}
