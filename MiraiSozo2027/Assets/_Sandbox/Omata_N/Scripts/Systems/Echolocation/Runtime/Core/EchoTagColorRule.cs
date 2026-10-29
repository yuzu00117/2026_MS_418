// Inspectorのタグ・色一覧の1行を定義する。
using System;
using UnityEngine;

namespace Echo.Echolocation
{
    /// <summary>タグ完全一致による表示色。アルファは不透明度。</summary>
    [Serializable]
    public sealed class EchoTagColorRule
    {
        [Tooltip("この行を有効にします。")]
        [UnityEngine.SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("Enabled")]
        private bool _isEnabled = true;
        [SerializeField] private EchoTagDefinition _definition;
        public EchoTagDefinition Definition { get => _definition; set => _definition = value; }
        [SerializeField] private EchoOriginalRevealMode _revealMode;
        public EchoOriginalRevealMode RevealMode { get => _revealMode; set => _revealMode = value; }
        public bool IsEnabled { get => _isEnabled; set => _isEnabled = value; }

        [Tooltip("対象GameObjectのタグ。子へは自動継承しません。")]
        [UnityEngine.SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("TagName")]
        private string _tagName = "Untagged";
        public string TagName { get => _tagName; set => _tagName = value; }

        [Tooltip("RGBが強調色、Aが不透明度です。")]
        [UnityEngine.SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("Color")]
        private Color _color = new Color(0, 1, 1, .65f);
        public Color Color { get => _color; set => _color = value; }

        [SerializeField, Tooltip("従来の、波が届いた表面の色塗りです。")]
        private bool _fillEnabled = true;
        public bool FillEnabled { get => _fillEnabled; set => _fillEnabled = value; }
        [SerializeField] private EchoEffectOcclusionMode _fillOcclusion;
        public EchoEffectOcclusionMode FillOcclusion { get => _fillOcclusion; set => _fillOcclusion = value; }
        [SerializeField] private EchoEffectOcclusionMode _outlineOcclusion;
        public EchoEffectOcclusionMode OutlineOcclusion { get => _outlineOcclusion; set => _outlineOcclusion = value; }
        [SerializeField] private EchoEffectOcclusionMode _originalOcclusion;
        public EchoEffectOcclusionMode OriginalOcclusion { get => _originalOcclusion; set => _originalOcclusion = value; }

        public static EchoOcclusionMode ResolveOcclusion(EchoEffectOcclusionMode mode, EchoOcclusionMode common) =>
            mode == EchoEffectOcclusionMode.ThroughWalls ? EchoOcclusionMode.ThroughWalls :
            mode == EchoEffectOcclusionMode.VisibleOnly ? EchoOcclusionMode.VisibleOnly : common;
        [SerializeField] private bool _outlineEnabled;
        public bool OutlineEnabled { get => _outlineEnabled; set => _outlineEnabled = value; }
        [SerializeField] private Color _outlineColor = Color.cyan;
        public Color OutlineColor { get => _outlineColor; set => _outlineColor = value; }
        [SerializeField, Range(1, 16), Tooltip("画面上の輪郭の太さ（ピクセル）。内側に描きます。")]
        private float _outlineWidth = 3;
        public float OutlineWidth { get => _outlineWidth; set => _outlineWidth = value; }
        [SerializeField, Min(0)] private float _outlineHoldDuration;
        public float OutlineHoldDuration { get => _outlineHoldDuration; set => _outlineHoldDuration = value; }
        [SerializeField, Tooltip("表示範囲に従い本来のマテリアルで描画します。ContactRegionは波が届いた表面のみ、ベイク不要です。")]
        private bool _originalEnabled;
        public bool OriginalEnabled { get => _originalEnabled; set => _originalEnabled = value; }
        [SerializeField, Tooltip("このタグの通常描画を隠します。Controller無効化時は元の状態に戻します。")]
        private bool _hideOriginalOutsideEcho = true;
        public bool HideOriginalOutsideEcho { get => _hideOriginalOutsideEcho; set => _hideOriginalOutsideEcho = value; }
        [SerializeField, Min(0), Tooltip("WholeObjectの残存秒数。ContactRegionはライトと同じく現在届いている表面だけを表示します。")]
        private float _originalHoldDuration = 2;
        public float OriginalHoldDuration { get => _originalHoldDuration; set => _originalHoldDuration = value; }
        [SerializeField, Range(0, 1)] private float _originalOpacity = 1;
        public float OriginalOpacity { get => _originalOpacity; set => _originalOpacity = value; }
        [SerializeField, Range(64, 1024), Tooltip("全体表示の接触判定解像度。大きい/細い物体は上げてください。高いほどGPU負荷が増えます。")]
        private int _contactResolution = 256;
        public int ContactResolution { get => _contactResolution; set => _contactResolution = value; }

        [SerializeField] private EchoGlitchSettings _glitch = new EchoGlitchSettings();
        public EchoGlitchSettings Glitch => _glitch ??= new EchoGlitchSettings();

        public void Validate()
        {
            _outlineWidth = FiniteClamp(_outlineWidth, 1, 16, 3);
            _outlineHoldDuration = FiniteClamp(_outlineHoldDuration, 0, 3600, 0);
            _originalHoldDuration = FiniteClamp(_originalHoldDuration, 0, 3600, 2);
            _originalOpacity = FiniteClamp(_originalOpacity, 0, 1, 1);
            _contactResolution = Mathf.ClosestPowerOfTwo(Mathf.Clamp(_contactResolution, 64, 1024));
        }

        static float FiniteClamp(float value, float min, float max, float fallback) =>
            float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, min, max);
        public EchoTagColorRule Copy()
        {
            var copy = (EchoTagColorRule)MemberwiseClone();
            copy._glitch = Glitch.Copy();
            return copy;
        }
    }
}
