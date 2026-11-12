using UnityEngine;

namespace Aura.Wave
{
    /// @brief 波が物に当たったときの情報。受け手の IWaveReceiver に渡す
    public readonly struct WaveHit
    {
        /// @brief 当たりの情報を作る
        /// @param type 波の種類
        /// @param energy 届いたエネルギー（跳ね返った後なら、跳ね返りで減らした後の量）
        /// @param point 当たった位置
        /// @param normal 当たった面の法線
        /// @param direction 当たったときの波の進む向き
        /// @param collider 当たったコライダー
        /// @param source 波を出した物（Emitter の GameObject）
        /// @param fireMode 撃ち方
        /// @param reflectionCount 当たる前に跳ね返った回数。0 なら直接当たった
        /// @param waveNumber 波の番号（WaveNumber を見る）
        public WaveHit(WaveType type, float energy, Vector3 point, Vector3 normal, Vector3 direction,
            Collider collider, GameObject source, WaveFireMode fireMode, int reflectionCount, int waveNumber)
        {
            Type = type;
            Energy = energy;
            Point = point;
            Normal = normal;
            Direction = direction;
            Collider = collider;
            Source = source;
            FireMode = fireMode;
            ReflectionCount = reflectionCount;
            WaveNumber = waveNumber;
        }

        /// @brief 波の種類
        public WaveType Type { get; }

        /// @brief 届いたエネルギー。単発は 1 発ぶん、連続は 1 フレームぶん（毎秒の量 × 経過時間）
        /// 跳ね返った後は、跳ね返るたびに WaveEmitter の Reflection Energy Multiplier を掛けた量
        public float Energy { get; }

        /// @brief 当たった位置（ワールド座標）
        public Vector3 Point { get; }

        /// @brief 当たった面の法線
        public Vector3 Normal { get; }

        /// @brief 当たったときの波の進む向き。跳ね返った後なら、最後に跳ね返った後の向き
        public Vector3 Direction { get; }

        /// @brief 当たったコライダー
        public Collider Collider { get; }

        /// @brief 波を出した物。消えていれば null
        public GameObject Source { get; }

        /// @brief 撃ち方
        public WaveFireMode FireMode { get; }

        /// @brief 当たる前に跳ね返った回数。0 なら直接当たった
        public int ReflectionCount { get; }

        /// @brief 波の番号。1 本の波（単発は 1 発、ビームは 1 フレーム）ごとに振る通し番号で、大きいほど後に出た波
        /// 跳ね返った後の当たりも同じ番号になるので、飛んでいる別の単発の当たりと見分けられる
        public int WaveNumber { get; }
    }
}
