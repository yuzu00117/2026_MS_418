using UnityEngine;
using UnityEngine.UI;

namespace MiraiSozo.Options
{
    /// <summary>メニューを開くときのソナーの波紋を描画する。発信点から時間差で3つの輪が広がる。</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class OptionMenuSonar : MaskableGraphic
    {
        // 輪の数・色・太さなどはOptionMenuThemeで調整する。
        private const int Segments = 96;
        // 終盤のこの割合から全体を消していく。
        private const float FadeStart = 0.78f;
        // ほぼ一定の速さで広がり、終点の手前でわずかに減速する。
        private const float SpeedEase = 1.5f;

        private Vector2 _origin;
        private float _startRadius;
        private float _endRadius;
        private float _progress;
        private float _intensity = 1f;

        /// <summary>全体の濃さの倍率。背景で流す控えめな波紋では小さくする。</summary>
        public float Intensity
        {
            get => _intensity;
            set
            {
                _intensity = Mathf.Clamp01(value);
                SetVerticesDirty();
            }
        }

        /// <summary>最初の輪の先頭の半径。項目の表示範囲はこの半径に合わせる。</summary>
        public static float GetLeadRadius(float startRadius, float endRadius, float progress)
            => GetRingRadius(startRadius, endRadius, progress);

        private static float GetRingRadius(float startRadius, float endRadius, float progress)
        {
            float eased = 1f - Mathf.Pow(1f - Mathf.Clamp01(progress), SpeedEase);
            return Mathf.Lerp(startRadius, endRadius, eased);
        }

        /// <summary>progressは0から1。0で発信、1で全ての輪が消える。</summary>
        public void SetPulse(Vector2 origin, float startRadius, float endRadius, float progress)
        {
            _origin = origin;
            _startRadius = startRadius;
            _endRadius = endRadius;
            _progress = progress;
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
            float fade = _intensity * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(FadeStart, 1f, _progress)));
            if (fade <= 0f) return;
            float lead = GetRingRadius(_startRadius, _endRadius, _progress);
            AddScannedArea(vertices, lead - theme.TailLength, theme.ScannedColor, fade * (1f - _progress));
            for (int i = 0; i < theme.RingCount; i++)
            {
                float ringProgress = _progress - i * theme.RingDelay;
                if (ringProgress <= 0f) continue;
                float radius = GetRingRadius(_startRadius, _endRadius, ringProgress);
                AddRing(vertices, theme, radius, fade * Mathf.Pow(theme.RingFalloff, i));
            }
        }

        // 先頭が鋭く明るく、後ろへ尾を引いて消える波面。
        private void AddRing(VertexHelper vertices, OptionMenuTheme theme, float radius, float opacity)
        {
            if (radius <= 0f || opacity <= 0f) return;
            var tail = Fade(theme.WaveTailColor, opacity);
            var crest = Fade(theme.WaveCrestColor, opacity);
            var core = Fade(theme.WaveCoreColor, opacity);
            float crestStart = radius - theme.CrestWidth;
            AddBand(vertices, crestStart - theme.TailLength, crestStart, OptionSoftShapes.GetColorWithAlpha(tail, 0f), tail);
            AddBand(vertices, crestStart, radius, crest, core);
            AddBand(vertices, radius, radius + theme.FrontFeather, core, OptionSoftShapes.GetColorWithAlpha(crest, 0f));
        }

        private void AddScannedArea(VertexHelper vertices, float radius, Color color, float opacity)
        {
            if (radius <= 0f || opacity <= 0f) return;
            color = Fade(color, opacity);
            AddBand(vertices, 0f, radius, color, color);
        }

        // 色の不透明度にopacityを掛ける。
        private static Color Fade(Color color, float opacity)
        {
            color.a *= opacity;
            return color;
        }

        // 発信点を中心とした円環を、内側と外側の色の補間で塗る。
        private void AddBand(VertexHelper vertices, float inner, float outer, Color innerColor, Color outerColor)
        {
            inner = Mathf.Max(0f, inner);
            if (outer <= inner) return;
            int start = vertices.currentVertCount;
            for (int i = 0; i <= Segments; i++)
            {
                float angle = i * Mathf.PI * 2f / Segments;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                vertices.AddVert(_origin + direction * inner, innerColor, Vector2.zero);
                vertices.AddVert(_origin + direction * outer, outerColor, Vector2.zero);
                if (i == Segments) continue;
                int edge = start + i * 2;
                vertices.AddTriangle(edge, edge + 2, edge + 1);
                vertices.AddTriangle(edge + 2, edge + 3, edge + 1);
            }
        }
    }
}
