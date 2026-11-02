using System;
using System.Collections.Generic;
using Aura.ObjectInfo;
using UnityEngine;

namespace Aura.Wave
{
    /// @brief ビーム・扇・照準の当たり判定をまとめたもの
    /// 点と線のビームの点（BeamPoint）は、念力で掴むためだけにあるので、ビームと照準には当たらない（どのビームの点でも素通りする）
    /// 素材（Substance）が「通す」の物にも当たらず、素通りする。通った物は別に集めて返し、ビームが波を当てたまま先へ進めるようにする
    internal static class BeamPhysics
    {
        private const int MaxCastHits = 32;
        private static readonly Comparison<RaycastHit> ByDistance = (a, b) => a.distance.CompareTo(b.distance);  ///< 近い順に並べる
        private static RaycastHit[] _castHits = new RaycastHit[MaxCastHits];  ///< 判定の結果を受ける使い回しの配列。埋まったら大きくしてやり直す
        private static Collider[] _overlapHits = new Collider[MaxCastHits];   ///< 扇の候補を受ける使い回しの配列。埋まったら大きくしてやり直す

        /// @brief 球（radius が 0 なら線）を飛ばし、ignoreRoot の下と素通りする物を除いた一番近い当たりを返す
        /// @param origin 始点
        /// @param direction 向き（正規化したもの）
        /// @param radius 球の半径（m）。0 なら線で判定する
        /// @param distance 判定する距離（m）
        /// @param mask 当たるレイヤー
        /// @param triggerInteraction トリガーに当たるか
        /// @param ignoreRoot この Transform の下のコライダーには当たらない。null なら除かない
        /// @param outHit 一番近い当たり
        /// @param passedHits 素材が「通す」で素通りした物の当たりを入れる先（近い順。返す当たりより手前の物だけ）。空にしてから入れる。null なら集めない
        /// @return 何かに当たったら true（「通す」の物にしか当たらなければ false）
        public static bool TryCast(Vector3 origin, Vector3 direction, float radius, float distance, LayerMask mask,
            QueryTriggerInteraction triggerInteraction, Transform ignoreRoot, out RaycastHit outHit,
            List<RaycastHit> passedHits = null)
        {
            outHit = default;
            passedHits?.Clear();
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

                // 素材が「通す」の物は素通りする（当たりとしては返さず、通った物として集める）
                if (ObjectInfoLookup.IsWavePassThrough(hit.collider, false))
                {
                    passedHits?.Add(hit);
                    continue;
                }

                if (hit.distance < nearestDistance)
                {
                    nearestDistance = hit.distance;
                    outHit = hit;
                    hasHit = true;
                }
            }

            // 通った物は、返す当たりより手前の物だけを近い順に残す（当たりより向こうの物には、そこまで届いていない）
            if (passedHits != null && passedHits.Count > 0)
            {
                if (hasHit)
                {
                    for (int i = passedHits.Count - 1; i >= 0; --i)
                    {
                        if (passedHits[i].distance > nearestDistance)
                        {
                            passedHits.RemoveAt(i);
                        }
                    }
                }
                passedHits.Sort(ByDistance);
            }
            return hasHit;
        }

        /// @brief 球の中のコライダーを集める。ignoreRoot の下と、ビームの点（BeamPoint）は除く（素材が「通す」の物は入る）
        /// @param center 球の中心
        /// @param radius 球の半径（m）
        /// @param mask 当たるレイヤー
        /// @param triggerInteraction トリガーに当たるか
        /// @param ignoreRoot この Transform の下のコライダーは集めない。null なら除かない
        /// @param outColliders 集めたコライダーが入る使い回しの配列（次に呼ぶと中身が変わる）
        /// @return 集めた数（outColliders の先頭からこの数だけが有効）
        public static int OverlapSphere(Vector3 center, float radius, LayerMask mask,
            QueryTriggerInteraction triggerInteraction, Transform ignoreRoot, out Collider[] outColliders)
        {
            int count;
            while (true)
            {
                count = Physics.OverlapSphereNonAlloc(center, radius, _overlapHits, mask, triggerInteraction);
                // 受け皿が埋まったときは、取りこぼしがあるかもしれないので、大きくしてやり直す
                if (count < _overlapHits.Length)
                {
                    break;
                }
                _overlapHits = new Collider[_overlapHits.Length * 2];
            }

            // 除く物を飛ばして、前へ詰める
            int kept = 0;
            for (int i = 0; i < count; ++i)
            {
                Collider candidate = _overlapHits[i];
                if (ignoreRoot != null && candidate.transform.IsChildOf(ignoreRoot))
                {
                    continue;
                }
                if (IsBeamPoint(candidate))
                {
                    continue;
                }
                _overlapHits[kept] = candidate;
                ++kept;
            }
            outColliders = _overlapHits;
            return kept;
        }

        /// @brief ビームの点（BeamPoint）のコライダーか。点の Rigidbody に BeamPoint が付いているかで見分ける
        private static bool IsBeamPoint(Collider hitCollider)
        {
            Rigidbody attachedRigidbody = hitCollider.attachedRigidbody;
            return attachedRigidbody != null && attachedRigidbody.TryGetComponent(out BeamPoint _);
        }
    }
}
