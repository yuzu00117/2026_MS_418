// Inspectorのタグ・色一覧の1行を定義する。
using System;
using UnityEngine;

namespace Echo.Echolocation
{
    /// <summary>タグ完全一致による表示色。アルファは不透明度。</summary>
    [Serializable]
    public sealed class EchoTagColorRule : ISerializationCallbackReceiver
    {
        // 照射中の表示（EchoDisplaySettings）と同じ範囲にそろえるため共有する。
        internal const float DefaultOutlineWidth = 3;
        internal const float MinimumOutlineWidth = 1;
        internal const float MaximumOutlineWidth = 16;
        // 照射中の表示を追加する前のデータは0のまま読み込まれる。
        private const int CurrentSettingsVersion = 1;
        // 実体化すると通常の描画が完全に見えるため、③はその見た目（不透明度1）へ近づける。
        private const float MaterializedOriginalOpacity = 1;
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
        // 旧仕様では③元のマテリアル表示の中の設定だった。移行時に_isNormalRenderingHiddenへ引き継ぐためだけに残す。
        [SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("_hideOriginalOutsideEcho")]
        private bool _isOriginalHiddenOutsideEcho = true;
        [SerializeField, Tooltip("このタグの通常描画を隠し、透明状態から始めます。Controller無効化時は元の状態に戻します。")]
        private bool _isNormalRenderingHidden;
        [SerializeField] private EchoDisplaySettings _irradiated = new EchoDisplaySettings();
        [SerializeField] private EchoMaterializationSettings _materialization = new EchoMaterializationSettings();
        // 新規作成時も0だが、移行しても既定値が変わらないため問題ない。
        [SerializeField] private int _settingsVersion;
        [NonSerialized] private EchoTagColorRule _irradiatedRule;
        // 照射中の表示用の複製だけが持つ、移行先（実体化しているときの表示）。
        [NonSerialized] private EchoTagColorRule _transitionTarget;

        /// <summary>通常の描画を隠すか。隠す対象は透明状態から始まり、照射で実体化する。</summary>
        public bool IsNormalRenderingHidden { get => _isNormalRenderingHidden; set { _isNormalRenderingHidden = value; InvalidateDisplayRules(); } }
        /// <summary>透明・照射中に使う表示。</summary>
        public EchoDisplaySettings Irradiated => _irradiated ??= new EchoDisplaySettings();
        public EchoMaterializationSettings Materialization => _materialization ??= new EchoMaterializationSettings();
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
        // 隠す対象は表示がなくても実体化のために接触を調べ続ける必要があるため、描画対象に含める。
        public bool HasAnyEffect => IsEnabled && (Fill.IsEnabled || Outline.IsEnabled || Original.IsEnabled || IsNormalRenderingHidden);

        /// <summary>状態に応じて描画に使う設定。透明・照射中は照射中の表示、実体化中はこの設定自身。</summary>
        public EchoTagColorRule GetDisplayRule(EchoMaterializationState state)
        {
            if (!IsNormalRenderingHidden || state == EchoMaterializationState.Materialized) return this;
            return _irradiatedRule ??= CreateIrradiatedRule();
        }

        /// <summary>照射中の表示用に作った設定を捨てる。設定を変更した後に呼ぶ。</summary>
        public void InvalidateDisplayRules() => _irradiatedRule = null;

        // 描画側は1つのEchoTagColorRuleを前提に組まれているため、表示部分だけを差し替えた複製を渡す。
        EchoTagColorRule CreateIrradiatedRule()
        {
            var display = Irradiated;
            var rule = (EchoTagColorRule)MemberwiseClone();
            rule._fill = display.Fill.Copy(); rule._outline = display.Outline.Copy(); rule._original = display.Original.Copy();
            rule._color = display.Color; rule._outlineColor = display.OutlineColor;
            rule._outlineWidth = display.OutlineWidth; rule._originalOpacity = display.OriginalOpacity;
            rule._irradiatedRule = null;
            rule._transitionTarget = this;
            return rule;
        }

        /// <summary>①の色。progressは実体化までの進み具合（0～1）。</summary>
        public Color GetFillColor(float progress)
        {
            if (!IsTransitioning(Fill)) return _color;
            // 実体化後に①を表示しない場合は、同じ色のまま消えていくようにする。
            var target = _transitionTarget.Fill.IsEnabled ? _transitionTarget._color : WithoutAlpha(_color);
            return Color.Lerp(_color, target, Mathf.Clamp01(progress));
        }

        /// <summary>②の色。progressは実体化までの進み具合（0～1）。</summary>
        public Color GetOutlineColor(float progress)
        {
            if (!IsTransitioning(Outline)) return _outlineColor;
            var target = _transitionTarget.Outline.IsEnabled ? _transitionTarget._outlineColor : WithoutAlpha(_outlineColor);
            return Color.Lerp(_outlineColor, target, Mathf.Clamp01(progress));
        }

        /// <summary>②の幅。progressは実体化までの進み具合（0～1）。</summary>
        public float GetOutlineWidth(float progress)
        {
            if (!IsTransitioning(Outline) || !_transitionTarget.Outline.IsEnabled) return _outlineWidth;
            return Mathf.Lerp(_outlineWidth, _transitionTarget._outlineWidth, Mathf.Clamp01(progress));
        }

        /// <summary>③の不透明度。progressは実体化までの進み具合（0～1）。</summary>
        public float GetOriginalOpacity(float progress)
        {
            if (!IsTransitioning(Original)) return _originalOpacity;
            return Mathf.Lerp(_originalOpacity, MaterializedOriginalOpacity, Mathf.Clamp01(progress));
        }

        bool IsTransitioning(EchoEffectSettings effect) => _transitionTarget != null && effect.IsSeamlessTransition;
        static Color WithoutAlpha(Color color) => new Color(color.r, color.g, color.b, 0);

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }
        // 既存アセットを開き直さなくても新しい設定で動くよう、読み込み時に移行する。Unity APIは使わないため読み込み中でも安全。
        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            MigrateSettings();
            InvalidateDisplayRules();
        }

        /// <summary>照射中の表示を追加する前のデータを、見た目が変わらないように移行する。</summary>
        public void MigrateSettings()
        {
            if (_settingsVersion >= CurrentSettingsVersion) return;
            _settingsVersion = CurrentSettingsVersion;
            // 旧仕様は③が有効なときだけ通常の描画を隠していた。
            _isNormalRenderingHidden = Original.IsEnabled && _isOriginalHiddenOutsideEcho;
            if (!_isNormalRenderingHidden) return;
            // 旧仕様の隠す対象は実体化の概念がなかったため、実体化するまでは従来と同じ表示を使う。
            CopyMaterializedDisplayToIrradiated();
        }

        /// <summary>実体化しているときの表示①②③を、照射中の表示へ写す。</summary>
        public void CopyMaterializedDisplayToIrradiated()
        {
            CopyMaterializedFillToIrradiated();
            CopyMaterializedOutlineToIrradiated();
            CopyMaterializedOriginalToIrradiated();
        }

        /// <summary>実体化しているときの①塗りつぶしだけを、照射中の表示へ写す。</summary>
        public void CopyMaterializedFillToIrradiated()
        {
            Irradiated.Color = _color;
            Irradiated.Fill.CopyFrom(Fill);
            InvalidateDisplayRules();
        }

        /// <summary>実体化しているときの②アウトラインだけを、照射中の表示へ写す。</summary>
        public void CopyMaterializedOutlineToIrradiated()
        {
            Irradiated.OutlineColor = _outlineColor;
            Irradiated.OutlineWidth = _outlineWidth;
            Irradiated.Outline.CopyFrom(Outline);
            InvalidateDisplayRules();
        }

        /// <summary>実体化しているときの③元のマテリアルだけを、照射中の表示へ写す。</summary>
        public void CopyMaterializedOriginalToIrradiated()
        {
            Irradiated.OriginalOpacity = _originalOpacity;
            Irradiated.Original.CopyFrom(Original);
            InvalidateDisplayRules();
        }
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
            Irradiated.Validate();
            InvalidateDisplayRules();
        }

        internal static float FiniteClamp(float value, float min, float max, float fallback) =>
            float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Clamp(value, min, max);
        public EchoTagColorRule Copy()
        {
            var copy = (EchoTagColorRule)MemberwiseClone();
            copy._fill = Fill.Copy(); copy._outline = Outline.Copy(); copy._original = Original.Copy();
            copy._irradiated = Irradiated.Copy(); copy._materialization = Materialization.Copy();
            copy._irradiatedRule = null;
            return copy;
        }
    }
}
