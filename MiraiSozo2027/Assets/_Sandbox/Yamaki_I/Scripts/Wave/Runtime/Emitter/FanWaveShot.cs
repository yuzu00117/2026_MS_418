using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aura.Wave
{
    /// @brief 扇（近距離）の 1 発の実体。撃った瞬間の情報（FanShotInfo）と、扇の中で当たる物を覚えておく
    /// 撃った後に撃ち手が動いても、1 発はその場から広がる。波面が届いた物から順に、WaveEmitter が波を渡す
    /// 見た目は持たない（見た目は IFanWaveView の部品が、FanShotInfo から自分で描く）
    internal sealed class FanWaveShot
    {
        private static readonly Comparison<RaycastHit> ByDistance = (a, b) => a.distance.CompareTo(b.distance);

        private readonly List<RaycastHit> _pendingHits = new List<RaycastHit>();  ///< 扇の中で当たる物（出る位置から近い順）
        private int _nextHitIndex;  ///< 次に波面が届く当たりの番号

        /// @brief 波面が進んでいるか（撃ってから、波面が届く距離に着いて、すべての当たりに波を渡すまで）
        public bool IsActive { get; private set; }

        /// @brief 撃った 1 発の情報（位置・向き・届く距離・半角・速さ・撃った時刻・波の種類）
        public FanShotInfo Info { get; private set; }

        /// @brief 1 つの受け手に渡す量（弱点の倍率を掛ける前）
        public float Energy { get; private set; }

        /// @brief この 1 発で、もう波を渡した受け手。同じ受け手には 1 発で 1 回だけ渡す
        public HashSet<ReactionTrigger> DeliveredTriggers { get; } = new HashSet<ReactionTrigger>();

        /// @brief まだ波面が届いていない当たりがあるか
        public bool HasPendingHits => _nextHitIndex < _pendingHits.Count;

        /// @brief 撃つ。当たる物は、この後 AddHit で足してから SortHits で並べる
        /// @param info 撃った 1 発の情報
        /// @param energy 1 つの受け手に渡す量（弱点の倍率を掛ける前）
        public void Begin(in FanShotInfo info, float energy)
        {
            Info = info;
            Energy = energy;
            _pendingHits.Clear();
            _nextHitIndex = 0;
            DeliveredTriggers.Clear();
            IsActive = true;
        }

        /// @brief 扇の中で当たる物を足す（distance は出る位置からの距離）
        public void AddHit(in RaycastHit hit)
        {
            _pendingHits.Add(hit);
        }

        /// @brief 当たる物を、出る位置から近い順に並べる
        public void SortHits()
        {
            _pendingHits.Sort(ByDistance);
        }

        /// @brief 波面が届いた当たりを、近い順に 1 つ取り出す
        /// @param frontDistance 波面の、出る位置からの距離（m）
        /// @param outHit 取り出した当たり
        /// @return 届いた当たりがあれば true
        public bool TryTakeArrivedHit(float frontDistance, out RaycastHit outHit)
        {
            if (_nextHitIndex < _pendingHits.Count && _pendingHits[_nextHitIndex].distance <= frontDistance)
            {
                outHit = _pendingHits[_nextHitIndex++];
                return true;
            }
            outHit = default;
            return false;
        }

        /// @brief 撃ち終える。覚えていた当たりを捨てる（まだ届いていない物には渡さない）
        public void End()
        {
            IsActive = false;
            Info = default;
            _pendingHits.Clear();
            _nextHitIndex = 0;
            DeliveredTriggers.Clear();
        }
    }
}
