// 設定の値域補正とコピーを担当する。入力設定は別コンポーネントに保持する。
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Echo.Echolocation
{
    /// <summary>スキャン設定。距離はワールド単位、角度は全開き角、時間は秒。</summary>
    [Serializable]
    public sealed class EchoSettings
    {
        [Min(0), Tooltip("最大半径。TransformのScaleには依存しません。")]
        [UnityEngine.SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("Radius")]
        private float _radius = 10;
        public float Radius { get => _radius; set => _radius = value; }

        [Range(0, 360), Tooltip("全開き角。正面から片側はこの半分、360で全方向です。")]
        [UnityEngine.SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("AngleDegrees")]
        private float _angleDegrees = 360;
        public float AngleDegrees { get => _angleDegrees; set => _angleDegrees = value; }

        [Min(0), Tooltip("半径0から最大半径までの秒数。0なら即時です。")]
        [UnityEngine.SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("Duration")]
        private float _duration = 1;
        public float Duration { get => _duration; set => _duration = value; }

        [Tooltip("追従は従来の動作。ソナーは発生位置・向きを固定した波を繰り返し出します。")]
        [UnityEngine.SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("ScanMode")]
        private EchoScanMode _scanMode;
        public EchoScanMode ScanMode { get => _scanMode; set => _scanMode = value; }

        [Min(.05f), Tooltip("ソナーを再展開する間隔n（秒）。同時波数64を超える設定では、残存時間を守るため間隔を自動補正します。")]
        [UnityEngine.SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("PulseInterval")]
        private float _pulseInterval = 2;
        public float PulseInterval { get => _pulseInterval; set => _pulseInterval = value; }

        [Min(0), Tooltip("最大半径へ到達してから残る時間m（秒）。各波は発生から伝播時間＋この秒数で消えます。")]
        [UnityEngine.SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("PulseHoldDuration")]
        private float _pulseHoldDuration = 3;
        public float PulseHoldDuration { get => _pulseHoldDuration; set => _pulseHoldDuration = value; }

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
            Radius = GetFiniteNonNegative(Radius, 10);
            Duration = GetFiniteNonNegative(Duration, 1);
            PulseHoldDuration = GetFiniteNonNegative(PulseHoldDuration, 3);
            PulseInterval = Mathf.Max(.05f, GetFiniteNonNegative(PulseInterval, 2), (Duration + PulseHoldDuration) / EchoPulseTimeline.MaxPulses);
            AngleDegrees = Mathf.Clamp(GetFiniteNonNegative(AngleDegrees, 360), 0, 360);
            TagColorRules ??= new List<EchoTagColorRule>();
        }

        static float GetFiniteNonNegative(float v, float fallback) => float.IsNaN(v) || float.IsInfinity(v) ? fallback : Mathf.Max(0, v);
        [UnityEngine.SerializeField]
        [UnityEngine.Serialization.FormerlySerializedAs("Shadow")]
        private EchoShadowSettings _shadow = new EchoShadowSettings();
        public EchoShadowSettings Shadow { get => _shadow; set => _shadow = value; }

        /// <summary>外部からの設定変更が実行中設定へ漏れないよう深くコピーする。</summary>
        public EchoSettings Copy()
        {
            var copy = (EchoSettings)MemberwiseClone();
            copy.Shadow = Shadow?.Copy() ?? new EchoShadowSettings();
            copy.TagColorRules = new List<EchoTagColorRule>();
            if (TagColorRules != null)
                foreach (var r in TagColorRules)
                    if (r != null)
                        copy.TagColorRules.Add(new EchoTagColorRule { IsEnabled = r.IsEnabled, TagName = r.TagName, Color = r.Color });
            copy.Validate();
            return copy;
        }
    }
}
