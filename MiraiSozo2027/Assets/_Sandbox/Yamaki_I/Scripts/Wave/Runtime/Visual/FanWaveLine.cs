using UnityEngine;
using UnityEngine.Rendering;

namespace Aura.Wave
{
    /// @brief 扇（近距離）の波の見た目。Wi-Fi のマークのような弧を数本、半径を広げながら前へ流す
    /// 見た目だけで、当たり判定には関わらない（判定は WaveEmitter の扇の処理が行う）
    /// 色・太さ・流れる速さは、ビームと同じく WaveType から読む
    public sealed class FanWaveLine : MonoBehaviour
    {
        private const int ArcCount = 4;                      ///< 同時に見せる弧の数
        private const int ArcSegments = 24;                  ///< 弧 1 本を描く折れ線の区間の数
        private const float MinRadius = 0.05f;               ///< これより小さい半径の弧は描かない（m）
        private const float NearFadeLength = 0.4f;           ///< 出る位置の近くで、透明から浮かび上がる長さ（m）
        private const float MinSideSqrMagnitude = 0.0001f;   ///< 向きと up の外積の長さの 2 乗がこれより小さければ、up がほぼ平行で弧の面が決まらないとみなす

        private readonly Vector3[] _arcPoints = new Vector3[ArcSegments + 1];
        private LineRenderer[] _arcs;

        /// @brief 扇の見た目を持つ GameObject を作る
        /// @param name GameObject の名前
        /// @param parent 親。null ならシーンの直下
        /// @param material 弧のマテリアル。null なら頂点カラーの既定のマテリアル
        /// @return 作った FanWaveLine
        public static FanWaveLine Create(string name, Transform parent, Material material)
        {
            var fanObject = new GameObject(name);
            fanObject.transform.SetParent(parent, false);
            FanWaveLine fanLine = fanObject.AddComponent<FanWaveLine>();
            fanLine._arcs = new LineRenderer[ArcCount];
            for (int i = 0; i < ArcCount; ++i)
            {
                var arcObject = new GameObject("Arc " + i);
                arcObject.transform.SetParent(fanObject.transform, false);
                var lineRenderer = arcObject.AddComponent<LineRenderer>();
                lineRenderer.useWorldSpace = true;
                lineRenderer.shadowCastingMode = ShadowCastingMode.Off;
                lineRenderer.receiveShadows = false;
                lineRenderer.numCapVertices = 2;
                lineRenderer.positionCount = 0;
                lineRenderer.sharedMaterial = material != null ? material : InfoBeamLine.GetDefaultMaterial();
                lineRenderer.enabled = false;
                fanLine._arcs[i] = lineRenderer;
            }
            return fanLine;
        }

        /// @brief 扇の弧を描く。半径を広げながら前へ流れて見える
        /// @param origin 出る位置
        /// @param direction 出す向き（正規化したもの）
        /// @param up 弧を回す軸（ふつうは出る位置の上の向き。弧は向きと直交する横の広がりになる）
        /// @param halfAngle 扇の半角（度）
        /// @param range 届く距離（m）
        /// @param type 波の種類（色、太さ、流れる速さ）
        /// @param phase 波の位相（m）。時間とともに増やすと弧が前へ流れて見える
        public void Draw(Vector3 origin, Vector3 direction, Vector3 up, float halfAngle, float range, WaveType type,
            float phase)
        {
            if (type == null || range <= MinRadius || direction.sqrMagnitude < 0.5f)
            {
                Hide();
                return;
            }

            // 弧を回す軸。up が向きとほぼ平行で面が決まらないときは、代わりの軸を使う
            Vector3 axis = up;
            if (Vector3.Cross(direction, axis).sqrMagnitude < MinSideSqrMagnitude)
            {
                axis = Vector3.right;
                if (Vector3.Cross(direction, axis).sqrMagnitude < MinSideSqrMagnitude)
                {
                    axis = Vector3.forward;
                }
            }

            float spacing = range / ArcCount;
            float offset = Mathf.Repeat(phase, spacing);
            for (int i = 0; i < ArcCount; ++i)
            {
                LineRenderer arc = _arcs[i];
                float radius = offset + spacing * i;
                if (radius < MinRadius || radius > range)
                {
                    arc.enabled = false;
                    continue;
                }

                for (int j = 0; j <= ArcSegments; ++j)
                {
                    float angle = Mathf.Lerp(-halfAngle, halfAngle, (float)j / ArcSegments);
                    Vector3 arcDirection = Quaternion.AngleAxis(angle, axis) * direction;
                    _arcPoints[j] = origin + arcDirection * radius;
                }

                // 根元で浮かび上がり、遠いほど薄くなる
                Color color = type.Color;
                color.a *= Mathf.SmoothStep(0.0f, 1.0f, radius / NearFadeLength) * (1.0f - radius / range);
                arc.positionCount = _arcPoints.Length;
                arc.SetPositions(_arcPoints);
                arc.widthMultiplier = type.LineWidth;
                arc.startColor = color;
                arc.endColor = color;
                arc.enabled = true;
            }
        }

        /// @brief 弧を消す
        public void Hide()
        {
            if (_arcs == null)
            {
                return;
            }
            foreach (LineRenderer arc in _arcs)
            {
                if (arc != null)
                {
                    arc.enabled = false;
                }
            }
        }
    }
}
