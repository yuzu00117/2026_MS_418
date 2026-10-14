using System.Collections.Generic;
using UnityEngine;

namespace Aura.Wave
{
    /// @brief 単発で飛ぶ波の塊。WaveEmitter が実行中に作り、跳ね返らない物に当たるか、届く距離を進むと消える
    public sealed class WavePulse : MonoBehaviour
    {
        private readonly List<Vector3> _corners = new List<Vector3>();  ///< 線を描く折れ線の点。毎フレーム使い回す
        private WavePulseSettings _settings;
        private WaveTracer _tracer;
        private WaveLine _line;
        private float _tailTravelled;  ///< 後端が進んだ距離（m）

        /// @brief 単発の波を作って飛ばす
        /// @param settings 飛ばすときの設定
        /// @return 作った WavePulse
        internal static WavePulse Launch(in WavePulseSettings settings)
        {
            WaveLine line = WaveLine.Create("WavePulse", null, settings.LineMaterial);
            var pulse = line.gameObject.AddComponent<WavePulse>();
            pulse._settings = settings;
            pulse._line = line;
            pulse._tracer = new WaveTracer(settings.DeliveredCallback);
            pulse._tracer.Begin(settings.Trace, settings.Origin, settings.Direction, settings.Energy);
            pulse._line.Hide();
            return pulse;
        }

        private void Update()
        {
            // 実行中にスクリプトを書き換えると、たどる部品が消えて続きを飛ばせないので、波ごと消す
            if (_tracer == null)
            {
                Destroy(gameObject);
                return;
            }

            float step = _settings.Speed * Time.deltaTime;

            if (!_tracer.IsStopped)
            {
                _tracer.Advance(step);
            }

            // 後端は塊の長さだけ遅れて同じ道をたどり、止まった先端に追いつくと消える
            float headTravelled = _tracer.Travelled;
            _tailTravelled = Mathf.Max(_tailTravelled, headTravelled - _settings.Length);
            if (_tracer.IsStopped)
            {
                _tailTravelled += step;
                if (_tailTravelled >= headTravelled)
                {
                    Destroy(gameObject);
                    return;
                }
            }

            _tracer.GetCorners(_tailTravelled, headTravelled, false, _corners);
            WaveType type = _settings.Trace.Type;
            float phase = Time.time * type.ScrollSpeed;
            _line.Draw(_corners, _settings.Up, type, phase - _tailTravelled, isPacket: true);
        }
    }
}
