// 設定の値域補正とコピーを担当する。入力設定は別コンポーネントに保持する。
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aura.Echolocation
{
    /// <summary>スキャン設定。距離はワールド単位、角度は全開き角、時間は秒。</summary>
    [Serializable]
    public sealed class EchoSettings
    {
        private const float DefaultRadius = 15;
        internal const float FullCircleDegrees = 360;
        private const float DefaultAngleDegrees = 70;
        private const float DefaultDuration = .75f;
        private const float DefaultAdsRadius = 30;
        private const float DefaultAdsAngleDegrees = 38.6f;
        // カメラのADSの遷移時間（0.3秒）に合わせ、範囲と画面が同じ速さで切り替わるようにする。
        private const float DefaultAdsTransitionSeconds = .3f;
        private const float DefaultAdsAimDistance = 100;
        // Ignore Raycastレイヤー（2番）は、照準のレイにも当てない。
        private const int DefaultAdsAimLayers = ~(1 << 2);
        private const float DefaultPulseInterval = 2;
        private const float MinimumPulseInterval = .05f;
        private const float DefaultPulseHoldDuration = 3;
        private const float DefaultOnceRechargeSeconds = 2;
        [Min(0), Tooltip("最大半径。TransformのScaleには依存しません。")]
        [UnityEngine.SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("Radius")]
        private float _radius = DefaultRadius;
        public float Radius { get => _radius; set => _radius = value; }

        [Range(0, FullCircleDegrees), Tooltip("全開き角。正面から片側はこの半分、360で全方向です。")]
        [UnityEngine.SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("AngleDegrees")]
        private float _angleDegrees = DefaultAngleDegrees;
        public float AngleDegrees { get => _angleDegrees; set => _angleDegrees = value; }

        [Min(0), Tooltip("ADS中の最大半径。TransformのScaleには依存しません。")]
        [UnityEngine.SerializeField]
        private float _adsRadius = DefaultAdsRadius;
        /// <summary>ADS中の最大半径（m）。</summary>
        public float AdsRadius { get => _adsRadius; set => _adsRadius = value; }

        [Range(0, FullCircleDegrees), Tooltip("ADS中の全開き角（度）。")]
        [UnityEngine.SerializeField]
        private float _adsAngleDegrees = DefaultAdsAngleDegrees;
        /// <summary>ADS中の全開き角（度）。</summary>
        public float AdsAngleDegrees { get => _adsAngleDegrees; set => _adsAngleDegrees = value; }

        [Tooltip("ADSの開始・終了時に、半径と全開き角を少しずつ切り替えます。オフなら一瞬で切り替えます。")]
        [UnityEngine.SerializeField]
        private bool _isAdsTransitionSeamless = true;
        /// <summary>ADSの開始・終了時に、半径と全開き角を少しずつ切り替えるか。</summary>
        public bool IsAdsTransitionSeamless { get => _isAdsTransitionSeamless; set => _isAdsTransitionSeamless = value; }

        [Min(0), Tooltip("少しずつ切り替える時に、切り替わりきるまでの秒数。0なら一瞬で切り替えます。")]
        [UnityEngine.SerializeField]
        private float _adsTransitionSeconds = DefaultAdsTransitionSeconds;
        /// <summary>ADSの切り替えにかける秒数。IsAdsTransitionSeamlessがtrueのときだけ効く。</summary>
        public float AdsTransitionSeconds { get => _adsTransitionSeconds; set => _adsTransitionSeconds = value; }

        [Min(0), Tooltip("ADS中に描画カメラの向きへ飛ばすレイの長さ（m）。最初に当たった場所、当たらなければレイの端へエコーを向けます。0なら正面の基準の向きのままです。")]
        [UnityEngine.SerializeField]
        private float _adsAimDistance = DefaultAdsAimDistance;
        /// <summary>ADS中に描画カメラの向きへ飛ばすレイの長さ（m）。0ならエコーは正面の基準の向きのまま。</summary>
        public float AdsAimDistance { get => _adsAimDistance; set => _adsAimDistance = value; }

        [Tooltip("ADS中のレイが当たるレイヤー。")]
        [UnityEngine.SerializeField]
        private LayerMask _adsAimLayers = DefaultAdsAimLayers;
        /// <summary>ADS中のレイが当たるレイヤー。</summary>
        public LayerMask AdsAimLayers { get => _adsAimLayers; set => _adsAimLayers = value; }

        [Min(0), Tooltip("半径0から最大半径までの秒数。0なら即時です。")]
        [UnityEngine.SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("Duration")]
        private float _duration = DefaultDuration;
        public float Duration { get => _duration; set => _duration = value; }

        [Tooltip("ライト方式はプレイヤーに追従して照らし続けます。断続方式は発生位置・向きを固定した波を繰り返し出し、単発方式は帯状の波を1回だけ出します。")]
        [UnityEngine.SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("ScanMode")]
        private EchoScanMode _scanMode;
        public EchoScanMode ScanMode { get => _scanMode; set => _scanMode = value; }

        [Min(MinimumPulseInterval), Tooltip("断続方式で波を出し直す間隔n（秒）。同時波数64を超える設定では、残存時間を守るため間隔を自動補正します。")]
        [UnityEngine.SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("PulseInterval")]
        private float _pulseInterval = DefaultPulseInterval;
        public float PulseInterval { get => _pulseInterval; set => _pulseInterval = value; }

        [Min(0), Tooltip("最大半径へ到達してから残る時間m（秒）。各波は発生から伝播時間＋この秒数で消えます。")]
        [UnityEngine.SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("PulseHoldDuration")]
        private float _pulseHoldDuration = DefaultPulseHoldDuration;
        public float PulseHoldDuration { get => _pulseHoldDuration; set => _pulseHoldDuration = value; }

        [Min(0), Tooltip("ライト方式で展開しきった後、拡張波の表示を繰り返す間隔（秒）。0で無効。伝播時間と同じ速さで広がり、表示だけで検出範囲は変わりません。")]
        [UnityEngine.SerializeField]
        private float _followWaveInterval;
        public float FollowWaveInterval { get => _followWaveInterval; set => _followWaveInterval = value; }

        [UnityEngine.SerializeField]
        private EchoLightSustainSettings _lightSustain = new EchoLightSustainSettings();
        /// <summary>ライト方式の持続可能な時間と、残り時間に応じて範囲を狭める設定。</summary>
        public EchoLightSustainSettings LightSustain { get => _lightSustain ??= new EchoLightSustainSettings(); set => _lightSustain = value; }

        [Min(1), Tooltip("単発方式で連続で打てる回数（ストックの最大数）。")]
        [UnityEngine.SerializeField]
        private int _onceStockCapacity = 1;
        public int OnceStockCapacity { get => _onceStockCapacity; set => _onceStockCapacity = value; }

        [Min(0), Tooltip("ストックが1回復するまでの秒数。0なら打った直後に回復します。")]
        [UnityEngine.SerializeField]
        private float _onceRechargeSeconds = DefaultOnceRechargeSeconds;
        public float OnceRechargeSeconds { get => _onceRechargeSeconds; set => _onceRechargeSeconds = value; }

        [Tooltip("OFFを即時消去するか、手前から順に消すかを選択します。")]
        [UnityEngine.SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("OffMode")]
        private EchoOffMode _offMode;
        public EchoOffMode OffMode { get => _offMode; set => _offMode = value; }

        [Tooltip("ゲーム時間はポーズ中停止、実時間はポーズ中も進みます。")]
        [UnityEngine.SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("TimeMode")]
        private EchoTimeMode _timeMode;
        public EchoTimeMode TimeMode { get => _timeMode; set => _timeMode = value; }

        [Tooltip("壁越しに表示するか、通常の深度で隠すかを選択します。")]
        [UnityEngine.SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("OcclusionMode")]
        private EchoOcclusionMode _occlusionMode;
        public EchoOcclusionMode OcclusionMode { get => _occlusionMode; set => _occlusionMode = value; }

        [Tooltip("有効化時にスキャンを開始します。")]
        [UnityEngine.SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("StartEnabled")]
        private bool _isEnabledOnStart;
        public bool IsEnabledOnStart { get => _isEnabledOnStart; set => _isEnabledOnStart = value; }

        [Tooltip("対象所有者のレイヤーを絞り込みます。")]
        [UnityEngine.SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("TargetLayers")]
        private LayerMask _targetLayers = ~0;
        public LayerMask TargetLayers { get => _targetLayers; set => _targetLayers = value; }

        [Tooltip("起動時・シーン追加時にRendererを収集します。")]
        [UnityEngine.SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("AutoDiscover")]
        private bool _isAutoDiscoveryEnabled = true;
        public bool IsAutoDiscoveryEnabled { get => _isAutoDiscoveryEnabled; set => _isAutoDiscoveryEnabled = value; }

        [Tooltip("完全一致するタグと強調色の一覧です。")]
        [UnityEngine.SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("TagColorRules")]
        private List<EchoTagColorRule> _tagColorRules = new List<EchoTagColorRule>();
        public List<EchoTagColorRule> TagColorRules { get => _tagColorRules; set => _tagColorRules = value; }

        /// <summary>不正な数値を補正する。設定元を問わず同じ値域を使用する。</summary>
        public void Validate()
        {
            Shadow ??= new EchoShadowSettings();
            Shadow.Validate();
            Radius = GetFiniteNonNegative(Radius, DefaultRadius);
            Duration = GetFiniteNonNegative(Duration, DefaultDuration);
            PulseHoldDuration = GetFiniteNonNegative(PulseHoldDuration, DefaultPulseHoldDuration);
            PulseInterval = Mathf.Max(MinimumPulseInterval, GetFiniteNonNegative(PulseInterval, DefaultPulseInterval), (Duration + PulseHoldDuration) / EchoPulseTimeline.MaxPulses);
            AngleDegrees = Mathf.Clamp(GetFiniteNonNegative(AngleDegrees, DefaultAngleDegrees), 0, FullCircleDegrees);
            AdsRadius = GetFiniteNonNegative(AdsRadius, DefaultAdsRadius);
            AdsAngleDegrees = Mathf.Clamp(GetFiniteNonNegative(AdsAngleDegrees, DefaultAdsAngleDegrees), 0, FullCircleDegrees);
            AdsTransitionSeconds = GetFiniteNonNegative(AdsTransitionSeconds, DefaultAdsTransitionSeconds);
            AdsAimDistance = GetFiniteNonNegative(AdsAimDistance, DefaultAdsAimDistance);
            FollowWaveInterval = GetFiniteNonNegative(FollowWaveInterval, 0);
            LightSustain.Validate();
            OnceStockCapacity = Mathf.Clamp(OnceStockCapacity, 1, EchoPulseTimeline.MaxPulses);
            OnceRechargeSeconds = GetFiniteNonNegative(OnceRechargeSeconds, DefaultOnceRechargeSeconds);
            TagColorRules ??= new List<EchoTagColorRule>();
            foreach (var rule in TagColorRules) rule?.Validate();
        }

        internal static float GetFiniteNonNegative(float value, float fallback) => float.IsNaN(value) || float.IsInfinity(value) ? fallback : Mathf.Max(0, value);
        [UnityEngine.SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("Shadow")]
        private EchoShadowSettings _shadow = new EchoShadowSettings();
        public EchoShadowSettings Shadow { get => _shadow; set => _shadow = value; }

        /// <summary>外部からの設定変更が実行中設定へ漏れないよう深くコピーする。</summary>
        public EchoSettings Copy()
        {
            var copy = (EchoSettings)MemberwiseClone();
            copy.Shadow = Shadow?.Copy() ?? new EchoShadowSettings();
            copy.LightSustain = LightSustain.Copy();
            copy.TagColorRules = new List<EchoTagColorRule>();
            if (TagColorRules != null)
                foreach (var rule in TagColorRules)
                    if (rule != null)
                        copy.TagColorRules.Add(rule.Copy());
            copy.Validate();
            return copy;
        }
    }
}
