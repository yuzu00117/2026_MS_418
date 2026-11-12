// GPU側EchoVolume.hlslと同じ点包含判定。boundsは候補絞り込みだけに使う。
using UnityEngine;

namespace Echo.Echolocation
{
    /// <summary>球の扇形および消去領域の判定。</summary>
    public static class EchoVolumeMath
    {
        private const float FullCircleDegrees = 360;
        // 発生地点とほぼ同じ位置では向きを決められないため、角度判定を省く距離。
        private const float DirectionEpsilon = .00001f;
        /// <summary>表面位置が現在の表示領域に含まれるか判定する。</summary>
        public static bool Contains(in EchoFrameData frame, Vector3 point)
        {
            if (frame.State == EchoState.Hidden || frame.Angle <= 0)
                return false;
            Vector3 offset = point - frame.Origin;
            float distance = offset.magnitude;
            if (distance > frame.OuterRadius || ((frame.State == EchoState.Hiding || frame.IsBand) && distance <= frame.EraseRadius))
                return false;
            return frame.Angle >= FullCircleDegrees || distance <= DirectionEpsilon || Vector3.Dot(offset / distance, frame.Forward) >= Mathf.Cos(frame.Angle * .5f * Mathf.Deg2Rad);
        }

        /// <summary>AABBの中心だけではなく、球との交差で候補を選ぶ。</summary>
        public static bool Intersects(Bounds bounds, Vector3 origin, float radius) => bounds.SqrDistance(origin) <= radius * radius;
    }
}
