using UnityEngine;

namespace Aura.ObjectInfoDemo
{
    /// @brief ビームと照準の当たり判定をまとめたもの
    /// 今ある WavePhysics（Aura.Wave）はアセンブリの外から使えないので、同じ決まりの判定をここに持つ
    /// 点と線のビームの点（BeamPoint）は、念力で掴むためだけにあるので、ビームと照準には当たらない（どのビームの点でも素通りする）
    internal static class BeamPhysics
    {
        private const int MaxCastHits = 32;
        private static RaycastHit[] _castHits = new RaycastHit[MaxCastHits];  ///< 判定の結果を受ける使い回しの配列。埋まったら大きくしてやり直す

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

            int count;
            while (true)
            {
                count = radius > 0.0f
                    ? Physics.SphereCastNonAlloc(origin, radius, direction, _castHits, distance, mask, triggerInteraction)
                    : Physics.RaycastNonAlloc(origin, direction, _castHits, distance, mask, triggerInteraction);
                // 受け皿が埋まったときは、一番近い当たりが抜けているかもしれない（近い順には返らない）ので、大きくしてやり直す
                // （ビームの点が並ぶ所を通ると、点だけで埋まることがある）
                if (count < _castHits.Length)
                {
                    break;
                }
                _castHits = new RaycastHit[_castHits.Length * 2];
            }

            bool hasHit = false;
            float nearestDistance = float.MaxValue;
            for (int i = 0; i < count; ++i)
            {
                RaycastHit hit = _castHits[i];
                if (ignoreRoot != null && hit.collider.transform.IsChildOf(ignoreRoot))
                {
                    continue;
                }
                if (IsBeamPoint(hit.collider))
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

        /// @brief ビームの点（BeamPoint）のコライダーか。点の Rigidbody に BeamPoint が付いているかで見分ける
        private static bool IsBeamPoint(Collider hitCollider)
        {
            Rigidbody attachedRigidbody = hitCollider.attachedRigidbody;
            return attachedRigidbody != null && attachedRigidbody.TryGetComponent(out BeamPoint _);
        }
    }
}
