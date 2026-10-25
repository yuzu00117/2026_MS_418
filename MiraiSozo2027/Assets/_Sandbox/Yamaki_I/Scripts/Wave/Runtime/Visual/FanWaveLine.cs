using UnityEngine;
using UnityEngine.Rendering;

namespace Aura.Wave
{
    /// @brief 扇（近距離）の波の見た目。出る位置から前へ、輪を広げながら流す（扇の半角と同じ広がりの円すいに沿う）
    /// 見た目だけで、当たり判定には関わらない（判定は WaveEmitter の扇の処理が行う）
    /// 色・太さ・流れる速さは、ビームと同じく WaveType から読む
    public sealed class FanWaveLine : MonoBehaviour
    {
        private const int RingCount = 8;                     ///< 同時に見せる輪の数
        private const int RingSegments = 40;                 ///< 輪 1 つを描く折れ線の区間の数（両端をつないで閉じる）
        private const float FarFadeStart = 0.6f;             ///< 輪がここ（届く距離に対する割合）から先で薄れ、届く距離で消える
        private const float FarWidthScale = 2.0f;            ///< 届く距離まで進んだ輪の太さ（WaveType の太さの何倍か）。遠くの輪が細く見えすぎないように、進むほど太くする
        private const float MinRadius = 0.05f;               ///< これより小さい半径の輪は描かない（m）
        private const float NearFadeLength = 0.4f;           ///< 出る位置の近くで、透明から浮かび上がる長さ（m）
        private const float MinSideSqrMagnitude = 0.0001f;   ///< 向きと up の外積の長さの 2 乗がこれより小さければ、up がほぼ平行で輪の面の向きが決まらないとみなす

        private readonly Vector3[] _ringPoints = new Vector3[RingSegments];
        private LineRenderer[] _rings;

        /// @brief 扇の見た目を持つ GameObject を作る
        /// @param name GameObject の名前
        /// @param parent 親。null ならシーンの直下
        /// @param material 輪のマテリアル。null なら頂点カラーの既定のマテリアル
        /// @return 作った FanWaveLine
        public static FanWaveLine Create(string name, Transform parent, Material material)
        {
            var fanObject = new GameObject(name);
            fanObject.transform.SetParent(parent, false);
            FanWaveLine fanLine = fanObject.AddComponent<FanWaveLine>();
            fanLine._rings = new LineRenderer[RingCount];
            for (int i = 0; i < RingCount; ++i)
            {
                var ringObject = new GameObject("Ring " + i);
                ringObject.transform.SetParent(fanObject.transform, false);
                var lineRenderer = ringObject.AddComponent<LineRenderer>();
                lineRenderer.useWorldSpace = true;
                lineRenderer.loop = true;
                lineRenderer.shadowCastingMode = ShadowCastingMode.Off;
                lineRenderer.receiveShadows = false;
                lineRenderer.positionCount = 0;
                lineRenderer.sharedMaterial = material != null ? material : InfoBeamLine.GetDefaultMaterial();
                lineRenderer.enabled = false;
                fanLine._rings[i] = lineRenderer;
            }
            return fanLine;
        }

        /// @brief 扇の輪を描く。輪を広げながら前へ流れて見える
        /// 輪は、出る位置から同じ距離の点を、扇の半角の円すいの縁に沿って結んだもの（球面に広がる波の、いちばん外の縁）
        /// なので、いちばん外の輪の縁が届く距離にちょうど届く。輪の中心は、出す向きの上で、その距離の cos（半角）倍の位置
        /// @param origin 出る位置
        /// @param direction 出す向き（正規化したもの）
        /// @param up 輪の面の向きを決める軸（ふつうは出る位置の上の向き）
        /// @param halfAngle 扇の半角（度）
        /// @param range 届く距離（m）
        /// @param type 波の種類（色、太さ、流れる速さ）
        /// @param phase 波の位相（m）。時間とともに増やすと輪が前へ流れて見える
        public void Draw(Vector3 origin, Vector3 direction, Vector3 up, float halfAngle, float range, WaveType type,
            float phase)
        {
            if (type == null || range <= MinRadius || direction.sqrMagnitude < 0.5f)
            {
                Hide();
                return;
            }

            // 輪の面を張る 2 本の向き（どちらも出す向きと直交する）。up が向きとほぼ平行で決まらないときは、代わりの軸を使う
            Vector3 axis = up;
            if (Vector3.Cross(direction, axis).sqrMagnitude < MinSideSqrMagnitude)
            {
                axis = Vector3.right;
                if (Vector3.Cross(direction, axis).sqrMagnitude < MinSideSqrMagnitude)
                {
                    axis = Vector3.forward;
                }
            }
            Vector3 side = Vector3.Cross(axis, direction).normalized;
            Vector3 lift = Vector3.Cross(direction, side);
            float radians = halfAngle * Mathf.Deg2Rad;
            float centerScale = Mathf.Cos(radians);
            float radiusScale = Mathf.Sin(radians);

            float spacing = range / RingCount;
            float offset = Mathf.Repeat(phase, spacing);
            for (int i = 0; i < RingCount; ++i)
            {
                LineRenderer ring = _rings[i];
                float distance = offset + spacing * i;  // 出る位置から輪の縁までの距離（m）
                float radius = distance * radiusScale;
                if (radius < MinRadius || distance > range)
                {
                    ring.enabled = false;
                    continue;
                }

                Vector3 center = origin + direction * (distance * centerScale);
                for (int j = 0; j < RingSegments; ++j)
                {
                    float angle = 2.0f * Mathf.PI * j / RingSegments;
                    _ringPoints[j] = center + (side * Mathf.Cos(angle) + lift * Mathf.Sin(angle)) * radius;
                }

                // 根元で浮かび上がり、届く距離の手前から薄れて消える。進むほど太くする
                float ratio = distance / range;
                Color color = type.Color;
                color.a *= Mathf.SmoothStep(0.0f, 1.0f, distance / NearFadeLength)
                    * (1.0f - Mathf.SmoothStep(0.0f, 1.0f, Mathf.InverseLerp(FarFadeStart, 1.0f, ratio)));
                ring.positionCount = _ringPoints.Length;
                ring.SetPositions(_ringPoints);
                ring.widthMultiplier = type.LineWidth * Mathf.Lerp(1.0f, FarWidthScale, ratio);
                ring.startColor = color;
                ring.endColor = color;
                ring.enabled = true;
            }
        }

        /// @brief 輪を消す
        public void Hide()
        {
            if (_rings == null)
            {
                return;
            }
            foreach (LineRenderer ring in _rings)
            {
                if (ring != null)
                {
                    ring.enabled = false;
                }
            }
        }
    }
}
