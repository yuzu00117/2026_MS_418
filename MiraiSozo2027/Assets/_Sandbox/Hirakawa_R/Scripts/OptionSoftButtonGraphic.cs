using UnityEngine;
using UnityEngine.UI;

namespace MiraiSozo.Options
{
    /// <summary>ボタンに重ねる柔らかいSF調の装飾（周囲の光・左端の表示灯・下線・右端の点）。</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class OptionSoftButtonGraphic : MaskableGraphic
    {
        // 形と寸法はここで、色はOptionMenuThemeで調整する。
        private const float GlowWidth = 9f;
        private const float IndicatorInset = 14f;
        private const float IndicatorWidth = 4f;
        private const float IdleIndicatorHeight = 8f;
        private const float ActiveIndicatorRatio = 0.5f;
        private const float LineInset = 22f;
        private const float LineOffset = 5f;
        private const float LineThickness = 1.5f;
        private const float DotInset = 18f;
        private const float IdleDotRadius = 2.5f;
        private const float ActiveDotRadius = 4f;
        private const float DotGlowRadius = 10f;
        private const float DotGlowOpacity = 0.2f;
        private float _highlight;

        /// <summary>0で通常、1で選択中。間の値で表示を補間する。</summary>
        public void SetHighlight(float highlight)
        {
            highlight = Mathf.Clamp01(highlight);
            if (Mathf.Approximately(_highlight, highlight)) return;
            _highlight = highlight;
            SetVerticesDirty();
        }

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
            if (_highlight > 0f)
            {
                var glow = theme.ButtonGlowColor;
                glow.a *= _highlight;
                OptionSoftShapes.AddRoundedRing(vertices, rect, theme.ButtonRadius, 0f, GlowWidth, glow, OptionSoftShapes.GetColorWithAlpha(glow, 0f));
            }
            AddIndicator(vertices, rect, theme);
            OptionSoftShapes.AddFadingLine(vertices, rect.xMin + LineInset, rect.xMax - LineInset, rect.yMin + LineOffset, LineThickness,
                Color.Lerp(theme.UnderlineColor, theme.UnderlineActiveColor, _highlight));
            AddDot(vertices, rect, theme);
        }

        // 左端の表示灯。選択に合わせて縦に伸び、濃くなる。
        private void AddIndicator(VertexHelper vertices, Rect rect, OptionMenuTheme theme)
        {
            float height = Mathf.Lerp(IdleIndicatorHeight, rect.height * ActiveIndicatorRatio, _highlight);
            var size = new Vector2(IndicatorWidth, height);
            var center = new Vector2(rect.xMin + IndicatorInset, rect.center.y);
            OptionSoftShapes.AddRoundedFill(vertices, new Rect(center - size * 0.5f, size), IndicatorWidth * 0.5f,
                Color.Lerp(theme.IndicatorColor, theme.IndicatorActiveColor, _highlight));
        }

        private void AddDot(VertexHelper vertices, Rect rect, OptionMenuTheme theme)
        {
            var center = new Vector2(rect.xMax - DotInset, rect.center.y);
            var color = Color.Lerp(theme.DotColor, theme.DotActiveColor, _highlight);
            if (_highlight > 0f) OptionSoftShapes.AddCircle(vertices, center, DotGlowRadius, OptionSoftShapes.GetColorWithAlpha(theme.DotActiveColor, DotGlowOpacity * _highlight));
            OptionSoftShapes.AddCircle(vertices, center, Mathf.Lerp(IdleDotRadius, ActiveDotRadius, _highlight), color);
        }
    }
}
