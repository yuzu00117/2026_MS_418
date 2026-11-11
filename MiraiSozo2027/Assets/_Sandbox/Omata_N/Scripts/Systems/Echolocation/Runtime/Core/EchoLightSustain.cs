// ライト方式の残り時間だけを持つ。Unityの時間やControllerの状態に依存させず、EditModeで検証できるようにする。
using UnityEngine;

namespace Echo.Echolocation
{
    /// <summary>ライト方式の持続可能な時間の消費と回復。</summary>
    public sealed class EchoLightSustain
    {
        // 設定前を表す値。最初の更新で満タンから始める。
        private const float NotAppliedSeconds = -1;
        private float _remainingSeconds;
        private float _appliedSustainSeconds = NotAppliedSeconds;

        /// <summary>残りの持続可能な時間（秒）。時間制限がない設定では0。</summary>
        public float RemainingSeconds => _remainingSeconds;

        /// <summary>残り時間の割合（0～1）。時間制限がない設定では1。</summary>
        public float GetRatio(EchoLightSustainSettings settings)
        {
            Apply(settings);
            return settings.IsLimited ? Mathf.Clamp01(_remainingSeconds / settings.SustainSeconds) : 1;
        }

        /// <summary>ONにできるか。使い切ってOFFになる設定では、少しでも回復するまでONにできない。</summary>
        public bool CanTurnOn(EchoLightSustainSettings settings)
        {
            Apply(settings);
            return !settings.IsLimited || _remainingSeconds > 0 || settings.DepletionMode == EchoLightDepletionMode.KeepMinimum;
        }

        /// <summary>1フレーム分、ONなら消費しOFFなら回復する。このフレームで使い切ったらtrue。</summary>
        public bool Advance(bool isOn, float deltaSeconds, EchoLightSustainSettings settings)
        {
            Apply(settings);
            if (!settings.IsLimited) return false;
            float delta = float.IsFinite(deltaSeconds) ? Mathf.Max(0, deltaSeconds) : 0;
            if (isOn)
            {
                bool hadRemaining = _remainingSeconds > 0;
                _remainingSeconds = Mathf.Max(0, _remainingSeconds - delta);
                return hadRemaining && _remainingSeconds <= 0;
            }

            if (settings.RecoveryMode == EchoLightRecoveryMode.WhileOff) Recover(delta * settings.RecoverySpeed, settings);
            else if (settings.RecoveryMode == EchoLightRecoveryMode.RefillOnOff) Refill(settings);
            return false;
        }

        /// <summary>指定した秒数だけ回復する。最大は持続可能な時間。</summary>
        public void Recover(float seconds, EchoLightSustainSettings settings)
        {
            Apply(settings);
            if (!float.IsFinite(seconds)) return;
            _remainingSeconds = Mathf.Clamp(_remainingSeconds + seconds, 0, GetLimitSeconds(settings));
        }

        /// <summary>満タンに戻す。</summary>
        public void Refill(EchoLightSustainSettings settings)
        {
            Apply(settings);
            _remainingSeconds = GetLimitSeconds(settings);
        }

        // 時間制限のオン・オフや持続可能な時間を変えたら満タンから始める。調整中に回復を待たずに試せるようにする。
        void Apply(EchoLightSustainSettings settings)
        {
            float limitSeconds = GetLimitSeconds(settings);
            if (_appliedSustainSeconds == limitSeconds) return;
            _appliedSustainSeconds = limitSeconds;
            _remainingSeconds = limitSeconds;
        }

        // 時間制限なしでは残り時間を持たないため、上限を0として扱う。
        static float GetLimitSeconds(EchoLightSustainSettings settings) => settings.IsLimited ? settings.SustainSeconds : 0;
    }
}
