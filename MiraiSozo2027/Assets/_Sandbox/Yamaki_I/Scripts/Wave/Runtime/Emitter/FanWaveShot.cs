using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aura.Wave
{
    /// @brief 扇（近距離）の 1 発。撃った瞬間の位置・向き・波の種類と、扇の中で当たる物を覚えておく
    /// 撃った後に撃ち手が動いても、1 発はその場から広がる。輪の先頭（波面）が届いた物から順に、WaveEmitter が波を渡す
    /// 見た目の輪（FanWaveLine）を 1 つ持ち、撃ち終わったら次の 1 発に使い回す
    internal sealed class FanWaveShot
    {
        private static readonly Comparison<RaycastHit> ByDistance = (a, b) => a.distance.CompareTo(b.distance);

        private readonly List<RaycastHit> _pendingHits = new List<RaycastHit>();  ///< 扇の中で当たる物（出る位置から近い順）
        private int _nextHitIndex;  ///< 次に波面が届く当たりの番号

        /// @param line この 1 発の見た目の輪
        public FanWaveShot(FanWaveLine line)
        {
            Line = line;
        }

        /// @brief この 1 発の見た目の輪
        public FanWaveLine Line { get; }

        /// @brief 撃った後で、まだ輪が進んでいるか
        public bool IsActive { get; private set; }

        /// @brief 撃った位置
        public Vector3 Origin { get; private set; }

        /// @brief 撃った向き（正規化したもの）
        public Vector3 Direction { get; private set; }

        /// @brief 撃ったときの、出る位置の上の向き（輪の面の向きを決める）
        public Vector3 Up { get; private set; }

        /// @brief 届く距離（m）
        public float Range { get; private set; }

        /// @brief 扇の半角（度）
        public float HalfAngle { get; private set; }

        /// @brief 波の種類
        public WaveType Type { get; private set; }

        /// @brief 1 つの受け手に渡す量（弱点の倍率を掛ける前）
        public float Energy { get; private set; }

        /// @brief 撃った時刻
        public float StartTime { get; private set; }

        /// @brief この 1 発で、もう波を渡した受け手。同じ受け手には 1 発で 1 回だけ渡す
        public HashSet<ReactionTrigger> DeliveredTriggers { get; } = new HashSet<ReactionTrigger>();

        /// @brief まだ波面が届いていない当たりがあるか
        public bool HasPendingHits => _nextHitIndex < _pendingHits.Count;

        /// @brief 撃つ。当たる物は、この後 AddHit で足してから SortHits で並べる
        public void Begin(Vector3 origin, Vector3 direction, Vector3 up, float range, float halfAngle, WaveType type,
            float energy, float startTime)
        {
            Origin = origin;
            Direction = direction;
            Up = up;
            Range = range;
            HalfAngle = halfAngle;
            Type = type;
            Energy = energy;
            StartTime = startTime;
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
        /// @param frontDistance 波面（先頭の輪）の、出る位置からの距離（m）
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

        /// @brief 撃ち終える。輪を消し、覚えていた当たりを捨てる
        public void End()
        {
            IsActive = false;
            Type = null;
            _pendingHits.Clear();
            _nextHitIndex = 0;
            DeliveredTriggers.Clear();
            if (Line != null)
            {
                Line.Hide();
            }
        }
    }
}
