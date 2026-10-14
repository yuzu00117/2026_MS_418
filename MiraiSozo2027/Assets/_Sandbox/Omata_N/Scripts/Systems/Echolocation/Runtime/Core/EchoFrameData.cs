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

        /// <summary>ワールド座標の中心・方向と現在の表示領域を固定する。</summary>
        public EchoFrameData(Vector3 origin, Vector3 forward, float angle, float outer, float erase, EchoState state, EchoOcclusionMode occlusion, long pulseId = 0)
        {
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
