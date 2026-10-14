// 各波の位置・向き・寿命を保持する。描画と入力に依存せず、同じ時間基準で全波を進める。
using System.Collections.Generic;
using UnityEngine;

namespace Echo.Echolocation
{
    /// <summary>ON中に固定位置の波を追加し、伝播完了後m秒で破棄する。</summary>
    public sealed class EchoPulseTimeline
    {
        /// <summary>GPUと共有する最大同時波数。設定検証で間隔を補正して寿命を保つ。</summary>
        public const int MaxPulses = 64;
        private long _sequence;
        private readonly List<EchoPulse> _pulses = new List<EchoPulse>(MaxPulses);
        private double _sinceEmission;
        private double _eraseElapsed;
        private bool _isEmitting;
        private bool _isHiding;
        /// <summary>保持中の波をすべて破棄し、発生を停止する。</summary>
        public void Clear()
        {
            _pulses.Clear();
            _sinceEmission = _eraseElapsed = 0;
            _isEmitting = _isHiding = false;
        }

        /// <summary>残存波を破棄し、その場で最初の波を出す。</summary>
        public void Start(Vector3 origin, Vector3 forward, EchoSettings settings)
        {
            Clear();
            _isEmitting = true;
            if (settings.Radius > 0 && (settings.Duration > 0 || settings.PulseHoldDuration > 0))
                _pulses.Add(new EchoPulse { Origin = origin, Forward = forward.normalized, Id = ++_sequence });
        }

        /// <summary>新しい波を止める。波状OFFは各波の外側を固定し、各中心から消去する。</summary>
        public void Stop(EchoSettings settings)
        {
            _isEmitting = false;
            if (settings.OffMode == EchoOffMode.Immediate || settings.Duration <= 0)
            {
                Clear();
                return;
            }

            _isHiding = true;
            _eraseElapsed = 0;
            for (int i = 0; i < _pulses.Count; i++)
            {
                EchoPulse p = _pulses[i];
                p.FrozenRadius = EchoPropagation.GetDistance(settings.Radius, settings.Duration, (float)p.Age);
                _pulses[i] = p;
            }
        }

        /// <summary>時間を進め、古い順で生存中の領域を返す。長い停止フレーム後の発生は現在地で1回に集約する。</summary>
        public void Advance(float delta, EchoSettings settings, Vector3 origin, Vector3 forward, List<EchoFrameData> outputItems)
        {
            outputItems.Clear();
            delta = Mathf.Max(0, delta);
            if (_isHiding)
                _eraseElapsed += delta;
            float erase = EchoPropagation.GetDistance(settings.Radius, settings.Duration, (float)_eraseElapsed);
            for (int i = _pulses.Count - 1; i >= 0; i--)
            {
                EchoPulse p = _pulses[i];
                p.Age += delta;
                if (_isHiding ? erase >= p.FrozenRadius : p.Age >= (double)settings.Duration + settings.PulseHoldDuration)
                    _pulses.RemoveAt(i);
                else
                    _pulses[i] = p;
            }

            if (_isEmitting)
            {
                _sinceEmission += delta;
                if (_sinceEmission >= settings.PulseInterval)
                {
                    // 1フレームに大量発生させず、過去のプレイヤー位置を推測もしない。
                    _sinceEmission %= settings.PulseInterval;
                    // 寿命が0の波は、再発生時にも1フレームだけ表示しない。
                    if (settings.Radius > 0 && (settings.Duration > 0 || settings.PulseHoldDuration > 0) && _pulses.Count < MaxPulses)
                        _pulses.Add(new EchoPulse { Origin = origin, Forward = forward.normalized, Id = ++_sequence });
                }
            }

            foreach (EchoPulse p in _pulses)
            {
                var state = _isHiding ? EchoState.Hiding : p.Age < settings.Duration ? EchoState.Revealing : EchoState.Active;
                outputItems.Add(new EchoFrameData(p.Origin, p.Forward, settings.AngleDegrees, _isHiding ? p.FrozenRadius : EchoPropagation.GetDistance(settings.Radius, settings.Duration, (float)p.Age), _isHiding ? erase : 0, state, settings.OcclusionMode, p.Id));
            }
        }
    }
}
