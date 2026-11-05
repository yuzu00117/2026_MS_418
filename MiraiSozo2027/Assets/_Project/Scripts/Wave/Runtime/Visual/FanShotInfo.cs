using UnityEngine;

namespace Aura.Wave
{
    /// @brief 扇（近距離）の 1 発の情報。WaveEmitter が撃った瞬間に作り、扇の見た目の部品（IFanWaveView）に渡す
    /// 1 発の波面は、撃った位置から「速さ × 撃ってからの時間」だけ進んだ所にあり、波面が届いた物に波が渡る（GetFrontDistance）
    /// 見た目も波面に合わせて進めると、当たりと見た目がそろう
    public readonly struct FanShotInfo
    {
        /// @param origin 撃った位置
        /// @param direction 撃った向き（正規化したもの）
        /// @param up 撃ったときの、出る位置の上の向き
        /// @param range 届く距離（m）
        /// @param halfAngle 扇の半角（度）
        /// @param speed 波面が進む速さ（m/s）
        /// @param startTime 撃った時刻（Time.time）
        /// @param type 波の種類
        public FanShotInfo(Vector3 origin, Vector3 direction, Vector3 up, float range, float halfAngle, float speed,
            float startTime, WaveType type)
        {
            Origin = origin;
            Direction = direction;
            Up = up;
            Range = range;
            HalfAngle = halfAngle;
            Speed = speed;
            StartTime = startTime;
            Type = type;
        }

        /// @brief 撃った位置。撃った後に撃ち手が動いても、1 発はここから広がる
        public Vector3 Origin { get; }

        /// @brief 撃った向き（正規化したもの）
        public Vector3 Direction { get; }

        /// @brief 撃ったときの、出る位置の上の向き（輪の見た目では、輪の面の向きを決めるのに使っている）
        public Vector3 Up { get; }

        /// @brief 届く距離（m）。波面がここまで進むと、1 発の当たりは終わる
        public float Range { get; }

        /// @brief 扇の半角（度）。撃った向きからこの角度以内の物に当たる
        public float HalfAngle { get; }

        /// @brief 波面が進む速さ（m/s）
        public float Speed { get; }

        /// @brief 撃った時刻（Time.time）
        public float StartTime { get; }

        /// @brief 波の種類（色など）
        public WaveType Type { get; }

        /// @brief その時刻での波面の、撃った位置からの距離（m）
        /// @param time 時刻（Time.time）
        public float GetFrontDistance(float time)
        {
            return Mathf.Max(0.0f, time - StartTime) * Speed;
        }
    }
}
