using UnityEngine;

namespace Aura.ObjectInfoDemo
{
    /// @brief ビームと照準の当たり判定をまとめたもの
    /// 今ある WavePhysics（Aura.Wave）はアセンブリの外から使えないので、同じ決まりの判定をここに持つ
    internal static class BeamPhysics
    {
        private const int MaxCastHits = 32;
        private static readonly RaycastHit[] CastHits = new RaycastHit[MaxCastHits];  ///< 判定の結果を受ける使い回しの配列

        /// @brief 球（radius が 0 なら線）を飛ばし、ignoreRoot の下を除いた一番近い当たりを返す
        /// @param origin 始点
        /// @param direction 向き（正規化したもの）
        /// @param radius 球の半径（m）。0 なら線で判定する
        /// @param distance 判定する距離（m）
        /// @param mask 当たるレイヤー
        /// @param triggerInteraction トリガーに当たるか
        /// @param ignoreRoot この Transform の下のコライダーには当たらない。null なら除かない
        /// @param outHit 一番近い当たり
        /// @return 何かに当たったら true
        public static bool TryCast(Vector3 origin, Vector3 direction, float radius, float distance, LayerMask mask,
            QueryTriggerInteraction triggerInteraction, Transform ignoreRoot, out RaycastHit outHit)
        {
            outHit = default;
            if (distance <= 0.0f)
            {
                return false;
            }

            int count = radius > 0.0f
                ? Physics.SphereCastNonAlloc(origin, radius, direction, CastHits, distance, mask, triggerInteraction)
                : Physics.RaycastNonAlloc(origin, direction, CastHits, distance, mask, triggerInteraction);

            bool hasHit = false;
            float nearestDistance = float.MaxValue;
            for (int i = 0; i < count; ++i)
            {
                RaycastHit hit = CastHits[i];
                if (ignoreRoot != null && hit.collider.transform.IsChildOf(ignoreRoot))
                {
                    continue;
                }

                // 始点ですでに重なっているコライダーは、距離 0、位置 (0,0,0) で返るので位置を直す
                if (hit.distance <= 0.0f)
                {
                    hit.point = origin;
                    hit.normal = -direction;
                }

                if (hit.distance < nearestDistance)
                {
                    nearestDistance = hit.distance;
                    outHit = hit;
                    hasHit = true;
                }
            }
            return hasHit;
        }
    }
}
