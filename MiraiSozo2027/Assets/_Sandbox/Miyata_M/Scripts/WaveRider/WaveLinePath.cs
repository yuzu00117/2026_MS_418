using System.Collections.Generic;
using UnityEngine;

namespace Aura.Gimmick
{
    /// @brief 波の線（点を結んだ折れ線）の上の位置を扱う計算。MonoBehaviour に依存しない
    /// 線の上の位置は「先頭の点からの道のり s（m）」で表す。曲がった所の角も、道のりにそのまま数える
    public static class WaveLinePath
    {
        private const float MinSegmentLength = 0.0001f;  ///< これより短い線は、向きが取れないので飛ばす（m）

        /// @brief 折れ線の長さを求める
        /// @param corners 折れ線の点（先頭から順）
        /// @return 道のりの合計（m）。点が 2 つ未満なら 0
        /// 計算量: O(N)（N は点の数）
        public static float GetLength(IReadOnlyList<Vector3> corners)
        {
            float length = 0.0f;
            for (int i = 1; i < corners.Count; i++)
            {
                length += Vector3.Distance(corners[i - 1], corners[i]);
            }
            return length;
        }

        /// @brief 先頭から道のり s の位置と、そこでの進む向きを求める
        /// @param corners 折れ線の点（先頭から順）
        /// @param distance 先頭からの道のり（m）。0〜長さに丸める
        /// @param direction 進む向き（長さ 1）。向きが取れなければ Vector3.zero
        /// @return 折れ線の上の位置。点が無ければ Vector3.zero
        /// 計算量: O(N)（N は点の数）
        public static Vector3 GetPointAt(IReadOnlyList<Vector3> corners, float distance, out Vector3 direction)
        {
            direction = Vector3.zero;
            if (corners.Count == 0)
            {
                return Vector3.zero;
            }

            float remaining = Mathf.Max(distance, 0.0f);
            Vector3 point = corners[0];
            for (int i = 1; i < corners.Count; i++)
            {
                Vector3 segment = corners[i] - corners[i - 1];
                float segmentLength = segment.magnitude;
                if (segmentLength < MinSegmentLength)
                {
                    continue;
                }

                direction = segment / segmentLength;
                if (remaining <= segmentLength)
                {
                    return corners[i - 1] + direction * remaining;
                }
                remaining -= segmentLength;
                point = corners[i];
            }
            return point;  // 長さを超えたら終点
        }

        /// @brief 位置に一番近い折れ線の上の点を探し、その道のりを返す
        /// @param corners 折れ線の点（先頭から順）
        /// @param position 調べる位置（ワールド座標）
        /// @param sqrDistance 折れ線までの距離の 2 乗（m^2）。比べるために返す
        /// @return 先頭からの道のり（m）。点が 2 つ未満なら 0
        /// 計算量: O(N)（N は点の数）
        public static float Project(IReadOnlyList<Vector3> corners, Vector3 position, out float sqrDistance)
        {
            sqrDistance = float.MaxValue;
            float bestDistance = 0.0f;
            float travelled = 0.0f;
            for (int i = 1; i < corners.Count; i++)
            {
                Vector3 segment = corners[i] - corners[i - 1];
                float segmentLength = segment.magnitude;
                if (segmentLength < MinSegmentLength)
                {
                    continue;
                }

                Vector3 unit = segment / segmentLength;
                float along = Mathf.Clamp(Vector3.Dot(position - corners[i - 1], unit), 0.0f, segmentLength);
                Vector3 nearest = corners[i - 1] + unit * along;
                float sqr = (position - nearest).sqrMagnitude;
                if (sqr < sqrDistance)
                {
                    sqrDistance = sqr;
                    bestDistance = travelled + along;
                }
                travelled += segmentLength;
            }
            return bestDistance;
        }
    }
}
