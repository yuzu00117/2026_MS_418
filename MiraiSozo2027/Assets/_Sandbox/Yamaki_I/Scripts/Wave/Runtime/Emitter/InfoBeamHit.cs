using UnityEngine;

namespace Aura.Wave
{
    /// @brief ビームが物に当たったときの情報。受け手（ReactionTrigger）に渡し、画面にも出す
    public readonly struct InfoBeamHit
    {
        /// @brief 当たりの情報を作る
        /// @param type 波の種類
        /// @param energy 届いたエネルギー（1 秒あたりの強さ × 経過時間 × 弱点の倍率）
        /// @param multiplier 弱点の倍率。弱点でなければ 1
        /// @param point 当たった位置
        /// @param collider 当たったコライダー
        /// @param reflectionCount 当たる前に跳ね返った回数。0 なら直接当たった
        /// @param isReflected この当たりでビームが跳ね返ったか
        public InfoBeamHit(WaveType type, float energy, float multiplier, Vector3 point, Collider collider,
            int reflectionCount, bool isReflected)
        {
            Type = type;
            Energy = energy;
            Multiplier = multiplier;
            Point = point;
            Collider = collider;
            ReflectionCount = reflectionCount;
            IsReflected = isReflected;
        }

        /// @brief 波の種類
        public WaveType Type { get; }

        /// @brief 届いたエネルギー（1 秒あたりの強さ × 経過時間 × 弱点の倍率）
        public float Energy { get; }

        /// @brief 弱点の倍率。弱点（WeakPoint）でなければ 1
        public float Multiplier { get; }

        /// @brief 当たった位置（ワールド座標）
        public Vector3 Point { get; }

        /// @brief 当たったコライダー
        public Collider Collider { get; }

        /// @brief 当たる前に跳ね返った回数。0 なら直接当たった
        public int ReflectionCount { get; }

        /// @brief この当たりでビームが跳ね返ったか（素材が跳ね返すものだったか）
        public bool IsReflected { get; }
    }
}
