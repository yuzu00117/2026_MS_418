using System.Collections.Generic;
using UnityEngine;

namespace Aura.Wave
{
    /// @brief 波の当たり判定と、受け手への受け渡しをまとめたもの
    internal static class WavePhysics
    {
        private const int MaxCastHits = 32;
        private static readonly RaycastHit[] CastHits = new RaycastHit[MaxCastHits];  ///< 判定の結果を受ける使い回しの配列
        private static readonly List<IWaveReceiver> Receivers = new List<IWaveReceiver>();  ///< 受け手を探すときの使い回しのリスト

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

        /// @brief 当たったコライダーから親へたどり、最初に受け手（IWaveReceiver）が見つかった GameObject を返す
        /// @param collider 当たったコライダー
        /// @return 受け手を持つ GameObject。見つからなければ null
        public static GameObject FindReceiverObject(Collider collider)
        {
            if (collider == null)
            {
                return null;
            }
            IWaveReceiver nearest = collider.GetComponentInParent<IWaveReceiver>();
            return nearest is Component nearestComponent ? nearestComponent.gameObject : null;
        }

        /// @brief 受け手を持つ GameObject の、受け手すべてに波を渡す
        /// 同じ GameObject に複数の受け手（マイクとアンテナなど）を付けられるよう、全部に渡す
        /// @param hit 当たりの情報
        /// @param receiverObject 受け手を持つ GameObject（FindReceiverObject で探したもの）
        /// @return 1 つでも受け付けたら true
        public static bool Deliver(in WaveHit hit, GameObject receiverObject)
        {
            if (receiverObject == null)
            {
                return false;
            }

            receiverObject.GetComponents(Receivers);
            bool isAccepted = false;
            foreach (IWaveReceiver receiver in Receivers)
            {
                isAccepted |= receiver.ReceiveWave(hit);
            }
            Receivers.Clear();
            return isAccepted;
        }

        /// @brief 当たったコライダーから親へたどり、最初に見つかった有効な WaveSurface を返す。無効なものは無いものとして飛ばす
        /// @param collider 当たったコライダー
        /// @return 見つかった WaveSurface。なければ null
        public static WaveSurface FindSurface(Collider collider)
        {
            for (Transform current = collider.transform; current != null; current = current.parent)
            {
                if (current.TryGetComponent(out WaveSurface surface) && surface.isActiveAndEnabled)
                {
                    return surface;
                }
            }
            return null;
        }
    }
}
