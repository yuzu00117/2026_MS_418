using System;
using UnityEngine;

namespace Echo.Echolocation
{
    /// <summary>ライト方式で照らし続けられる時間と、残り時間に応じて範囲を狭める設定。</summary>
    [Serializable]
    public sealed class EchoLightSustainSettings
    {
        private const float DefaultRecoverySpeed = 1;
        private const float DefaultMinimumRadius = 2;
        private const float DefaultMinimumAngleDegrees = 30;
        private const float DefaultSustainSeconds = 10;
        // 0秒では点けた瞬間に使い切ってしまうため、時間制限ありの最小値を設ける。
        private const float MinimumSustainSeconds = .1f;

        [SerializeField, Tooltip("ONのまま照らし続けられる時間に制限を設けます。オフなら時間制限なしです。")]
        private bool _hasSustainLimit;
        [SerializeField, Min(MinimumSustainSeconds), Tooltip("ONのまま照らし続けられる秒数。")]
        private float _sustainSeconds = DefaultSustainSeconds;
        [SerializeField, Tooltip("持続可能な時間を使い切ったときの扱い。")]
        private EchoLightDepletionMode _depletionMode = EchoLightDepletionMode.TurnOff;
        [SerializeField, Tooltip("使った持続可能な時間の戻し方。回復しない場合は、スクリプトからRecoverLightSustainなどで戻します。")]
        private EchoLightRecoveryMode _recoveryMode = EchoLightRecoveryMode.WhileOff;
        [SerializeField, Min(0), Tooltip("OFFの1秒あたりに回復する秒数。1なら使った時間と同じ速さで回復します。")]
        private float _recoverySpeed = DefaultRecoverySpeed;
        [SerializeField, Tooltip("残り時間に応じて最大半径を狭めます。")]
        private bool _isRadiusShrinking;
        [SerializeField, Min(0), Tooltip("持続可能な時間が0になったときの半径（m）。最大半径より大きい値は最大半径に合わせます。")]
        private float _minimumRadius = DefaultMinimumRadius;
        [SerializeField, Tooltip("残り時間に応じて全開き角を狭めます。")]
        private bool _isAngleShrinking;
        [SerializeField, Range(0, EchoSettings.FullCircleDegrees), Tooltip("持続可能な時間が0になったときの全開き角（度）。全開き角より大きい値は全開き角に合わせます。")]
        private float _minimumAngleDegrees = DefaultMinimumAngleDegrees;

        /// <summary>持続可能な時間を設定するか。falseなら時間制限なし。</summary>
        public bool HasSustainLimit { get => _hasSustainLimit; set => _hasSustainLimit = value; }
        /// <summary>ONのまま照らし続けられる秒数。HasSustainLimitがtrueのときだけ効く。</summary>
        public float SustainSeconds { get => _sustainSeconds; set => _sustainSeconds = value; }
        /// <summary>時間制限があるか。</summary>
        public bool IsLimited => _hasSustainLimit && _sustainSeconds > 0;
        /// <summary>持続可能な時間を使い切ったときの扱い（自動でOFFにするか、最小範囲で照らし続けるか）。</summary>
        public EchoLightDepletionMode DepletionMode { get => _depletionMode; set => _depletionMode = value; }
        /// <summary>使った持続可能な時間の戻し方。</summary>
        public EchoLightRecoveryMode RecoveryMode { get => _recoveryMode; set => _recoveryMode = value; }
        /// <summary>OFFの1秒あたりに回復する秒数（倍率）。RecoveryModeがWhileOffのときだけ効く。</summary>
        public float RecoverySpeed { get => _recoverySpeed; set => _recoverySpeed = value; }
        /// <summary>残り時間に応じて最大半径を狭めるか。</summary>
        public bool IsRadiusShrinking { get => _isRadiusShrinking; set => _isRadiusShrinking = value; }
        /// <summary>残り時間が0のときの半径（m）。IsRadiusShrinkingがtrueのときだけ効く。</summary>
        public float MinimumRadius { get => _minimumRadius; set => _minimumRadius = value; }
        /// <summary>残り時間に応じて全開き角を狭めるか。</summary>
        public bool IsAngleShrinking { get => _isAngleShrinking; set => _isAngleShrinking = value; }
        /// <summary>残り時間が0のときの全開き角（度）。IsAngleShrinkingがtrueのときだけ効く。</summary>
        public float MinimumAngleDegrees { get => _minimumAngleDegrees; set => _minimumAngleDegrees = value; }

        /// <summary>残り時間の割合（0～1）に応じた半径。狭めない設定なら最大半径のまま。</summary>
        public float GetRadius(float maximumRadius, float remainingRatio) =>
            _isRadiusShrinking ? Mathf.Lerp(Mathf.Min(_minimumRadius, maximumRadius), maximumRadius, Mathf.Clamp01(remainingRatio)) : maximumRadius;

        /// <summary>残り時間の割合（0～1）に応じた全開き角。狭めない設定なら最大の全開き角のまま。</summary>
        public float GetAngleDegrees(float maximumAngleDegrees, float remainingRatio) =>
            _isAngleShrinking ? Mathf.Lerp(Mathf.Min(_minimumAngleDegrees, maximumAngleDegrees), maximumAngleDegrees, Mathf.Clamp01(remainingRatio)) : maximumAngleDegrees;

        public void Validate()
        {
            _sustainSeconds = Mathf.Max(MinimumSustainSeconds, EchoSettings.GetFiniteNonNegative(_sustainSeconds, DefaultSustainSeconds));
            _recoverySpeed = EchoSettings.GetFiniteNonNegative(_recoverySpeed, DefaultRecoverySpeed);
            _minimumRadius = EchoSettings.GetFiniteNonNegative(_minimumRadius, DefaultMinimumRadius);
            _minimumAngleDegrees = Mathf.Min(EchoSettings.GetFiniteNonNegative(_minimumAngleDegrees, DefaultMinimumAngleDegrees), EchoSettings.FullCircleDegrees);
        }

        public EchoLightSustainSettings Copy() => (EchoLightSustainSettings)MemberwiseClone();
    }
}
