// 1フレームの描画用スナップショット。描画側では時間を進めない。
using UnityEngine;

namespace Echo.Echolocation
{
    /// <summary>CPUとGPUに同じスキャン条件を渡す不変データ。</summary>
    public readonly struct EchoFrameData
    {
        public Vector3 Origin { get; }
        public Vector3 Forward { get; }
        public float Angle { get; }
        public float OuterRadius { get; }
        public float EraseRadius { get; }
        public EchoState State { get; }
        public EchoOcclusionMode Occlusion { get; }
        public long PulseId { get; }
        /// <summary>消去半径より内側を含まない帯状の波か。消去中と違い、消去波の色では描かない。</summary>
        public bool IsBand { get; }

        /// <summary>ワールド座標の中心・方向と現在の表示領域を固定する。</summary>
        public EchoFrameData(Vector3 origin, Vector3 forward, float angle, float outer, float erase, EchoState state, EchoOcclusionMode occlusion, long pulseId = 0, bool isBand = false)
        {
            IsBand = isBand;
            Origin = origin;
            Forward = forward.normalized;
            Angle = angle;
            OuterRadius = outer;
            EraseRadius = erase;
            State = state;
            Occlusion = occlusion;
            PulseId = pulseId;
        }
    }
}
