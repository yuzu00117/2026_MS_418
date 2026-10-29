using UnityEngine;

namespace Aura.Wave
{
    /// @brief 波の見た目を見せる範囲（球の一部）。WaveRevealSource が持ち、「見せ方」が「見える範囲の中だけ」の見た目の部品が読む
    /// エコーロケーション（Omata_N の EchoFrameData）と同じ形: 中心から OuterRadius までの球を、正面から Angle（全体の角度）の扇で切ったもの
    /// InnerRadius より内側は見せない（エコーが消えていくときの、内側の輪の分）
    public readonly struct WaveRevealVolume
    {
        private const float MinDirectionDistance = 0.00001f;  ///< 中心からこれより近い点（m）は、向きを決めずに中とみなす

        private readonly float _cosHalfAngle;  ///< 扇の半角の cos。正面との向きの比べ方に使う

        /// @param origin 中心
        /// @param forward 正面の向き
        /// @param angle 見せる扇の全体の角度（度）。360 以上なら全方向。0 以下なら何も見せない
        /// @param outerRadius 外側の半径（m）。これより遠い点は見せない
        /// @param innerRadius 内側の半径（m）。これより近い点は見せない。0 以下なら内側も見せる
        public WaveRevealVolume(Vector3 origin, Vector3 forward, float angle, float outerRadius, float innerRadius)
        {
            Origin = origin;
            Forward = forward.normalized;
            Angle = angle;
            OuterRadius = outerRadius;
            InnerRadius = innerRadius;
            _cosHalfAngle = Mathf.Cos(angle * 0.5f * Mathf.Deg2Rad);
        }

        /// @brief 中心
        public Vector3 Origin { get; }

        /// @brief 正面の向き（正規化したもの）
        public Vector3 Forward { get; }

        /// @brief 見せる扇の全体の角度（度）。360 以上なら全方向。0 以下なら何も見せない
        public float Angle { get; }

        /// @brief 外側の半径（m）。これより遠い点は見せない
        public float OuterRadius { get; }

        /// @brief 内側の半径（m）。これより近い点は見せない。0 以下なら内側も見せる
        public float InnerRadius { get; }

        /// @brief 全方向の球（内側も見せる）を作る
        /// @param origin 中心
        /// @param radius 半径（m）
        public static WaveRevealVolume Sphere(Vector3 origin, float radius)
        {
            return new WaveRevealVolume(origin, Vector3.forward, 360.0f, radius, 0.0f);
        }

        /// @brief その位置が範囲の中か
        /// @param point 調べる位置（ワールド座標）
        public bool Contains(Vector3 point)
        {
            if (Angle <= 0.0f)
            {
                return false;
            }
            Vector3 offset = point - Origin;
            float distance = offset.magnitude;
            if (distance > OuterRadius || (InnerRadius > 0.0f && distance <= InnerRadius))
            {
                return false;
            }
            if (Angle >= 360.0f || distance <= MinDirectionDistance)
            {
                return true;
            }
            return Vector3.Dot(offset / distance, Forward) >= _cosHalfAngle;
        }
    }
}
