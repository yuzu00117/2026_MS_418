namespace Aura.Gimmick
{
    /// @brief WaveRider が線の上の道のり s を進める計算（終点で止まる・折り返す、乗った時点の道のりを動ける範囲に収める）
    /// 動ける範囲は [startLimit, endLimit]。startLimit は折り返すときの始まり側の端、endLimit は終点側の端
    ///
    ///   線の始まり(砲台)                                   線の終点(壁)
    ///   |--- 後ろの広がり ---|=========== 動ける範囲 ===========|--- 前の広がり ---|
    ///   0                startLimit                         endLimit           長さ
    internal static class WaveRiderTravel
    {
        private const float MinTravelRoom = 0.001f;  ///< 動ける範囲の長さがこれ以下なら、動ける余地が無いとみなす（m）

        /// @brief 道のりを 1 ステップ分進めて返す。終点では止まり、折り返すなら向きを変える。始まり側では startLimit で折り返す
        /// 動ける範囲が無い（線がライダーの前後の広がり以下に短い）ときは、終点側の端に置いて、向きは変えない（毎ステップ反転して震えないため）
        /// @param direction 進む向き（1: 終点へ、-1: 始まりへ）。折り返したら書き換える
        /// 計算量: O(1)
        public static float Advance(float distance, ref float direction, float step, float startLimit, float endLimit, bool isPingPong)
        {
            if (endLimit - startLimit <= MinTravelRoom)
            {
                return endLimit;
            }

            distance += step * direction;
            if (distance >= endLimit)
            {
                if (isPingPong)
                {
                    direction = -1.0f;
                }
                return endLimit;
            }
            if (direction < 0.0f && distance <= startLimit)
            {
                direction = 1.0f;
                return startLimit;
            }
            return distance;
        }
    }
}
