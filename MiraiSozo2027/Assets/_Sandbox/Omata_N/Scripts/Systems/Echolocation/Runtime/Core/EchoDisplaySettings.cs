using System;
using UnityEngine;

namespace Echo.Echolocation
{
    /// <summary>
    /// ①塗りつぶし・②アウトライン・③元のマテリアルの表示設定ひとまとまり。照射中の表示に使う。
    /// 実体化しているときの表示は既存アセットとの互換のため、EchoTagColorRuleが直接持つ。
    /// </summary>
    [Serializable]
    public sealed class EchoDisplaySettings
    {
        // 透明な物体がうっすら見える程度にするため、実体化しているときの既定値（不透明）より薄くする。
        private const float DefaultOriginalOpacity = .5f;
        private static readonly Color DefaultColor = new Color(0, 1, 1, .65f);
        private static readonly Color DefaultOutlineColor = new Color(1, 1, 1, .5f);

        [SerializeField] private EchoEffectSettings _fill = new EchoEffectSettings();
        [SerializeField] private EchoEffectSettings _outline = new EchoEffectSettings { IsEnabled = true, DisplayScope = EchoOriginalRevealMode.WholeObject };
        [SerializeField] private EchoEffectSettings _original = new EchoEffectSettings();
        [SerializeField, Tooltip("RGBが強調色、Aが不透明度です。")]
        private Color _color = DefaultColor;
        [SerializeField] private Color _outlineColor = DefaultOutlineColor;
        [SerializeField, Range(EchoTagColorRule.MinimumOutlineWidth, EchoTagColorRule.MaximumOutlineWidth), Tooltip("画面上の輪郭の太さ（ピクセル）。内側に描きます。")]
        private float _outlineWidth = EchoTagColorRule.DefaultOutlineWidth;
        [SerializeField, Range(0, 1)] private float _originalOpacity = DefaultOriginalOpacity;

        public EchoEffectSettings Fill => _fill ??= new EchoEffectSettings();
        public EchoEffectSettings Outline => _outline ??= new EchoEffectSettings();
        public EchoEffectSettings Original => _original ??= new EchoEffectSettings();
        public Color Color { get => _color; set => _color = value; }
        public Color OutlineColor { get => _outlineColor; set => _outlineColor = value; }
        public float OutlineWidth { get => _outlineWidth; set => _outlineWidth = value; }
        public float OriginalOpacity { get => _originalOpacity; set => _originalOpacity = value; }

        public void Validate()
        {
            _outlineWidth = EchoTagColorRule.FiniteClamp(_outlineWidth, EchoTagColorRule.MinimumOutlineWidth, EchoTagColorRule.MaximumOutlineWidth, EchoTagColorRule.DefaultOutlineWidth);
            _originalOpacity = EchoTagColorRule.FiniteClamp(_originalOpacity, 0, 1, DefaultOriginalOpacity);
        }

        public EchoDisplaySettings Copy()
        {
            var copy = (EchoDisplaySettings)MemberwiseClone();
            copy._fill = Fill.Copy(); copy._outline = Outline.Copy(); copy._original = Original.Copy();
            return copy;
        }
    }
}
