// GPU側EchoVolume.hlslと同じ点包含判定。boundsは候補絞り込みだけに使う。
using UnityEngine;

namespace Echo.Echolocation
{
    /// <summary>球の扇形および消去領域の判定。</summary>
    public static class EchoVolumeMath
    {
        /// <summary>表面位置が現在の表示領域に含まれるか判定する。</summary>
        public static bool Contains(in EchoFrameData f, Vector3 p)
        {
            if (f.State == EchoState.Hidden || f.Angle <= 0)
                return false;
            Vector3 v = p - f.Origin;
            float d = v.magnitude;
            if (d > f.OuterRadius || (f.State == EchoState.Hiding && d <= f.EraseRadius))
                return false;
            return f.Angle >= 360 || d <= .00001f || Vector3.Dot(v / d, f.Forward) >= Mathf.Cos(f.Angle * .5f * Mathf.Deg2Rad);
        }

        /// <summary>AABBの中心だけではなく、球との交差で候補を選ぶ。</summary>
        public static bool Intersects(Bounds bounds, Vector3 origin, float radius) => bounds.SqrDistance(origin) <= radius * radius;
    }
}
