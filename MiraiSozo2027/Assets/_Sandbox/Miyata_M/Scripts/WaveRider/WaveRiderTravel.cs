using UnityEngine;

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

        /// @brief 乗った時点の道のりを、次のステップで飛ばされない範囲に収めて返す
        /// 終点側の端より先なら端へ。始まりへ向かっているときは、始まり側の端より手前なら端へ（終点へ向かうときは、そのまま進めば範囲に入る）
        /// 計算量: O(1)
        public static float ClampOnBoard(float distance, float direction, float startLimit, float endLimit)
        {
            float lower = direction < 0.0f ? startLimit : 0.0f;
            return System.Math.Clamp(distance, System.Math.Min(lower, endLimit), endLimit);
        }

        /// @brief 線に付いていく速さの上限で抑えたため、線から遅れた距離を返す（m）
        /// 壁際では毎ステップ押し戻されるので、目指す位置との差には押し戻した分が入る。その分は遅れに数えない
        /// （数えると、反射の曲がり角などで大きいライダーが、線が動いていないのに降りてしまう）
        /// 引くのは、押し戻しのうち目指す位置から離れる向きの分だけ。横の壁から押し戻されている間に線が前後へ動いても、降りる距離は延びない
        ///
        ///   押し戻し(push) <-----●-----------------> 目指す位置   gap の向きと逆向きの成分だけ引く
        ///
        /// @param gap 速さの上限で抑えた位置から、目指す位置へのずれ（m）
        /// @param push 直前のステップで壁から押し戻した移動（m）
        /// 計算量: O(1)
        public static float GetFollowLag(Vector3 gap, Vector3 push)
        {
            float gapLength = gap.magnitude;
            if (gapLength <= 0.0f)
            {
                return 0.0f;
            }
            float pushedBack = Mathf.Max(0.0f, Vector3.Dot(-push, gap / gapLength));
            return Mathf.Max(0.0f, gapLength - pushedBack);
        }
    }
}
