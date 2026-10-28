// Inspectorのタグ・色一覧の1行を定義する。
using System;
using UnityEngine;

namespace Echo.Echolocation
{
    /// <summary>タグ完全一致による表示色。アルファは不透明度。</summary>
    [Serializable]
    public sealed class EchoTagColorRule
    {
        private const float DefaultOutlineWidth = 3;
        private const float MinimumOutlineWidth = 1;
        private const float MaximumOutlineWidth = 16;
        private const float DefaultOriginalHoldDuration = 2;
        private const float MaximumHoldDuration = 3600;
        private const int DefaultContactResolution = 256;
        private const int MinimumContactResolution = 64;
        private const int MaximumContactResolution = 1024;
        private const int DefaultSurfaceMemoryResolution = 32;
        private const int MinimumSurfaceMemoryResolution = 16;
        private const int MaximumSurfaceMemoryResolution = 128;
        [Tooltip("この行を有効にします。")]
        [UnityEngine.SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("Enabled")]
        private bool _isEnabled = true;
        [SerializeField] private EchoTagDefinition _definition;
        public EchoTagDefinition Definition { get => _definition; set => _definition = value; }
        [SerializeField] private EchoOriginalRevealMode _revealMode;
        public EchoOriginalRevealMode RevealMode { get => Original.DisplayScope; set { _revealMode = value; Original.DisplayScope = value; } }
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
        [UnityEngine.Serialization.FormerlySerializedAs("_fillEnabled")]
        private bool _isFillEnabled = true;
        public bool IsFillEnabled { get => Fill.IsEnabled; set { _isFillEnabled = value; Fill.IsEnabled = value; } }
        [SerializeField] private EchoEffectOcclusionMode _fillOcclusion;
        public EchoEffectOcclusionMode FillOcclusion { get => _fillOcclusion; set { _fillOcclusion = value; Fill.IsThroughWalls = value == EchoEffectOcclusionMode.ThroughWalls; } }
        [SerializeField] private EchoEffectOcclusionMode _outlineOcclusion;
        public EchoEffectOcclusionMode OutlineOcclusion { get => _outlineOcclusion; set { _outlineOcclusion = value; Outline.IsThroughWalls = value == EchoEffectOcclusionMode.ThroughWalls; } }
        [SerializeField] private EchoEffectOcclusionMode _originalOcclusion;
        public EchoEffectOcclusionMode OriginalOcclusion { get => _originalOcclusion; set { _originalOcclusion = value; Original.IsThroughWalls = value == EchoEffectOcclusionMode.ThroughWalls; } }

        public static EchoOcclusionMode ResolveOcclusion(EchoEffectOcclusionMode mode, EchoOcclusionMode common) =>
            mode == EchoEffectOcclusionMode.ThroughWalls ? EchoOcclusionMode.ThroughWalls :
            mode == EchoEffectOcclusionMode.VisibleOnly ? EchoOcclusionMode.VisibleOnly : common;
        [SerializeField, UnityEngine.Serialization.FormerlySerializedAs("_outlineEnabled")] private bool _isOutlineEnabled;
        public bool IsOutlineEnabled { get => Outline.IsEnabled; set { _isOutlineEnabled = value; Outline.IsEnabled = value; } }
        [SerializeField] private Color _outlineColor = Color.cyan;
        public Color OutlineColor { get => _outlineColor; set => _outlineColor = value; }
        [SerializeField, Range(MinimumOutlineWidth, MaximumOutlineWidth), Tooltip("画面上の輪郭の太さ（ピクセル）。内側に描きます。")]
        private float _outlineWidth = DefaultOutlineWidth;
        public float OutlineWidth { get => _outlineWidth; set => _outlineWidth = value; }
        [SerializeField, Min(0)] private float _outlineHoldDuration;
        public float OutlineHoldDuration { get => Outline.EffectivePersistence; set { _outlineHoldDuration = value; Outline.HasPersistence = value > 0; Outline.PersistenceSeconds = value; } }
        [SerializeField, Tooltip("表示範囲に従い本来のマテリアルで描画します。ContactRegionは波が届いた表面のみ、ベイク不要です。")]
        [UnityEngine.Serialization.FormerlySerializedAs("_originalEnabled")]
        private bool _isOriginalEnabled;
        public bool IsOriginalEnabled { get => Original.IsEnabled; set { _isOriginalEnabled = value; Original.IsEnabled = value; } }
        [SerializeField, Tooltip("このタグの通常描画を隠します。Controller無効化時は元の状態に戻します。")]
        [UnityEngine.Serialization.FormerlySerializedAs("_hideOriginalOutsideEcho")]
        private bool _isOriginalHiddenOutsideEcho = true;
        public bool IsOriginalHiddenOutsideEcho { get => _isOriginalHiddenOutsideEcho; set => _isOriginalHiddenOutsideEcho = value; }
        [SerializeField, Min(0), Tooltip("WholeObjectの残存秒数。ContactRegionはライトと同じく現在届いている表面だけを表示します。")]
        private float _originalHoldDuration = DefaultOriginalHoldDuration;
        public float OriginalHoldDuration { get => Original.EffectivePersistence; set { _originalHoldDuration = value; Original.HasPersistence = value > 0; Original.PersistenceSeconds = value; } }
        [SerializeField, Range(0, 1)] private float _originalOpacity = 1;
        public float OriginalOpacity { get => _originalOpacity; set => _originalOpacity = value; }
        [SerializeField, Range(MinimumContactResolution, MaximumContactResolution), Tooltip("全体表示の接触判定解像度。大きい/細い物体は上げてください。高いほどGPU負荷が増えます。")]
        private int _contactResolution = DefaultContactResolution;
        public int ContactResolution { get => _contactResolution; set => _contactResolution = value; }

        [SerializeField] private EchoEffectSettings _fill = new EchoEffectSettings { IsEnabled = true };
        [SerializeField] private EchoEffectSettings _outline = new EchoEffectSettings { DisplayScope = EchoOriginalRevealMode.WholeObject };
        [SerializeField] private EchoEffectSettings _original = new EchoEffectSettings { DisplayScope = EchoOriginalRevealMode.WholeObject };
        [SerializeField, Range(MinimumSurfaceMemoryResolution, MaximumSurfaceMemoryResolution)] private int _surfaceMemoryResolution = DefaultSurfaceMemoryResolution;
        public EchoEffectSettings Fill => _fill ??= new EchoEffectSettings { IsEnabled = true };
        public EchoEffectSettings Outline => _outline ??= new EchoEffectSettings();
        public EchoEffectSettings Original => _original ??= new EchoEffectSettings();
        public int SurfaceMemoryResolution => Mathf.ClosestPowerOfTwo(Mathf.Clamp(_surfaceMemoryResolution, MinimumSurfaceMemoryResolution, MaximumSurfaceMemoryResolution));
        public bool HasAnyEffect => IsEnabled && (Fill.IsEnabled || Outline.IsEnabled || Original.IsEnabled);
        public bool NeedsWholeObjectContact => Fill.NeedsWholeObjectContact || Outline.NeedsWholeObjectContact ||
            Original.NeedsWholeObjectContact;
        public bool NeedsSurfaceMemory => Fill.NeedsSurfaceMemory || Outline.NeedsSurfaceMemory || Original.NeedsSurfaceMemory;
        public float MaximumPersistence => Mathf.Max(Fill.EffectivePersistence, Mathf.Max(Outline.EffectivePersistence, Original.EffectivePersistence));
        public void ImportLegacySettings(EchoOcclusionMode common)
        {
            Fill.IsEnabled = _isFillEnabled; Outline.IsEnabled = _isOutlineEnabled; Original.IsEnabled = _isOriginalEnabled;
            Fill.IsThroughWalls = ResolveOcclusion(_fillOcclusion, common) == EchoOcclusionMode.ThroughWalls;
            Outline.IsThroughWalls = ResolveOcclusion(_outlineOcclusion, common) == EchoOcclusionMode.ThroughWalls;
            Original.IsThroughWalls = ResolveOcclusion(_originalOcclusion, common) == EchoOcclusionMode.ThroughWalls;
            Original.DisplayScope = _revealMode;
            Outline.HasPersistence = _outlineHoldDuration > 0; Outline.PersistenceSeconds = _outlineHoldDuration;
            Original.HasPersistence = _originalHoldDuration > 0; Original.PersistenceSeconds = _originalHoldDuration;
        }
        public void Validate()
        {
            _outlineWidth = FiniteClamp(_outlineWidth, MinimumOutlineWidth, MaximumOutlineWidth, DefaultOutlineWidth);
            _outlineHoldDuration = FiniteClamp(_outlineHoldDuration, 0, MaximumHoldDuration, 0);
            _originalHoldDuration = FiniteClamp(_originalHoldDuration, 0, MaximumHoldDuration, DefaultOriginalHoldDuration);
            _originalOpacity = FiniteClamp(_originalOpacity, 0, 1, 1);
            _contactResolution = Mathf.ClosestPowerOfTwo(Mathf.Clamp(_contactResolution, MinimumContactResolution, MaximumContactResolution));
        }

        static float FiniteClamp(float value, float min, float max, float fallback) =>
            float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, min, max);
        public EchoTagColorRule Copy()
        {
            var copy = (EchoTagColorRule)MemberwiseClone();
            copy._fill = Fill.Copy(); copy._outline = Outline.Copy(); copy._original = Original.Copy();
            return copy;
        }
    }
}
