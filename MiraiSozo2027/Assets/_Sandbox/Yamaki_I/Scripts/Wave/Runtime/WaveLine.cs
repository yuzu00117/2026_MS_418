using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Aura.Wave
{
    /// @brief LineRenderer で、折れ線に沿って正弦波の線を描く。ビームと単発の波の見た目に使う
    /// 跳ね返った曲がり目では揺れを 0 にして、波が面に触れて折れ返るように見せる
    /// 揺らさない折れ線（DrawStraight）も描ける。予測線に使う
    [RequireComponent(typeof(LineRenderer))]
    public sealed class WaveLine : MonoBehaviour
    {
        private const int PointsPerWavelength = 12;
        private const int MaxPoints = 512;
        private const float BeamFadeInLength = 0.3f;     ///< ビームの根元で振幅を 0 から上げる長さ（m）
        private const float CornerFadeLength = 0.3f;     ///< 曲がり目の前後で振幅を 0 にする長さ（m）
        private const float MinSegmentLength = 0.0001f;  ///< これより短い区間は、向きが決まらないので描かない（m）
        private const float MinSideSqrMagnitude = 0.0001f;  ///< 進む向きと up の外積の長さの 2 乗がこれより小さければ、up がほぼ平行で揺れる向きが決まらないとみなす
        private const float StraightEndAlphaScale = 0.25f;  ///< 揺らさない線の終わりの不透明度を、始めの何倍にするか
        private const string DefaultShaderName = "Sprites/Default";

        private static Material _defaultMaterial;  ///< マテリアルを指定しないときに使う、頂点カラーで色を付けるマテリアル

        private LineRenderer _lineRenderer;
        private readonly Vector3[] _points = new Vector3[MaxPoints];
        private readonly Vector3[] _segmentEnds = new Vector3[2];  ///< 2 点の間に描くときに使い回す配列

        /// @brief 波の線を持つ GameObject を作る
        /// @param name GameObject の名前
        /// @param parent 親。null ならシーンの直下
        /// @param material 線のマテリアル。null なら頂点カラーの既定のマテリアル
        /// @return 作った WaveLine
        public static WaveLine Create(string name, Transform parent, Material material)
        {
            var lineObject = new GameObject(name);
            lineObject.transform.SetParent(parent, false);

            var lineRenderer = lineObject.AddComponent<LineRenderer>();
            lineRenderer.useWorldSpace = true;
            lineRenderer.shadowCastingMode = ShadowCastingMode.Off;
            lineRenderer.receiveShadows = false;
            lineRenderer.numCapVertices = 2;
            lineRenderer.positionCount = 0;
            lineRenderer.sharedMaterial = material != null ? material : GetDefaultMaterial();

            return lineObject.AddComponent<WaveLine>();
        }

        /// @brief 2 点の間に波を描く
        /// @param start 始点
        /// @param end 終点
        /// @param up 波が揺れる面を決める上の向き
        /// @param type 波の種類（色、波長、振幅、太さ）
        /// @param phase 波の位相（m）。時間とともに増やすと波が流れて見える
        /// @param isPacket true なら両端を細くした波の塊、false なら根元だけ細くしたビーム
        public void Draw(Vector3 start, Vector3 end, Vector3 up, WaveType type, float phase, bool isPacket)
        {
            _segmentEnds[0] = start;
            _segmentEnds[1] = end;
            Draw(_segmentEnds, up, type, phase, isPacket);
        }

        /// @brief 折れ線に沿って波を描く。間の曲がり目（跳ね返った位置）では揺れを 0 にする
        /// @param corners 折れ線の点（始点、曲がり目、終点の順）。2 つ以上
        /// @param up 波が揺れる面を決める上の向き
        /// @param type 波の種類（色、波長、振幅、太さ）
        /// @param phase 波の位相（m）。corners の最初の点からの道のりで数える。時間とともに増やすと波が流れて見える
        /// @param isPacket true なら両端を細くした波の塊、false なら根元だけ細くしたビーム
        public void Draw(IReadOnlyList<Vector3> corners, Vector3 up, WaveType type, float phase, bool isPacket)
        {
            if (type == null || corners == null || corners.Count < 2)
            {
                Hide();
                return;
            }

            // 短すぎる区間（長さが NaN の区間も）を除いて、描く区間の数と長さの合計を求める
            // 区間は点の数の上限に収まる数（MaxPoints - 1）まで描き、残りは描かない
            int segmentCount = 0;
            float totalLength = 0.0f;
            for (int i = 1; i < corners.Count && segmentCount < MaxPoints - 1; ++i)
            {
                float length = (corners[i] - corners[i - 1]).magnitude;
                if (length >= MinSegmentLength)
                {
                    ++segmentCount;
                    totalLength += length;
                }
            }
            if (totalLength < MinSegmentLength)
            {
                Hide();
                return;
            }

            int intervalBudget = Mathf.Clamp(Mathf.CeilToInt(totalLength / type.Wavelength * PointsPerWavelength) + 1, 2, MaxPoints) - 1;
            int freeIntervals = MaxPoints - 1;  // まだ使える点の間の数
            float angularScale = 2.0f * Mathf.PI / type.Wavelength;
            int count = 0;
            int segmentIndex = 0;
            float segmentStartDistance = 0.0f;
            for (int i = 1; i < corners.Count && segmentIndex < segmentCount; ++i)
            {
                Vector3 start = corners[i - 1];
                Vector3 delta = corners[i] - start;
                float length = delta.magnitude;
                // 数えるときと同じ決まりで飛ばす（NaN も飛ばすよう、比べた結果を否定する）
                if (!(length >= MinSegmentLength))
                {
                    continue;
                }

                Vector3 forward = delta / length;
                Vector3 side = Vector3.Cross(forward, up);
                if (side.sqrMagnitude < MinSideSqrMagnitude)
                {
                    side = Vector3.Cross(forward, Vector3.right);
                }
                Vector3 swing = Vector3.Cross(side.normalized, forward);

                // 点の間の数は区間の長さに比べて割り振り、後の区間にも 1 つずつは残す
                int laterSegmentCount = segmentCount - segmentIndex - 1;
                int intervals = Mathf.Max(1, Mathf.CeilToInt(length / totalLength * intervalBudget));
                intervals = Mathf.Min(intervals, freeIntervals - laterSegmentCount);
                if (intervals <= 0)
                {
                    break;
                }
                freeIntervals -= intervals;

                bool hasCornerAtStart = segmentIndex > 0;
                bool hasCornerAtEnd = segmentIndex < segmentCount - 1;
                // 2 つ目からの区間の始点は、前の区間の終点と同じなので入れない
                for (int j = segmentIndex == 0 ? 0 : 1; j <= intervals; ++j)
                {
                    float t = (float)j / intervals;
                    float distanceInSegment = t * length;
                    float distance = segmentStartDistance + distanceInSegment;
                    float envelope = isPacket
                        ? Mathf.Sin(distance / totalLength * Mathf.PI)
                        : Mathf.SmoothStep(0.0f, 1.0f, distance / BeamFadeInLength);
                    if (hasCornerAtStart)
                    {
                        envelope *= Mathf.SmoothStep(0.0f, 1.0f, distanceInSegment / CornerFadeLength);
                    }
                    if (hasCornerAtEnd)
                    {
                        envelope *= Mathf.SmoothStep(0.0f, 1.0f, (length - distanceInSegment) / CornerFadeLength);
                    }
                    float offset = Mathf.Sin((distance - phase) * angularScale) * type.Amplitude * envelope;
                    _points[count] = start + forward * distanceInSegment + swing * offset;
                    ++count;
                }
                segmentStartDistance += length;
                ++segmentIndex;
            }

            _lineRenderer.positionCount = count;
            _lineRenderer.SetPositions(_points);
            _lineRenderer.widthMultiplier = type.LineWidth;

            Color endColor = type.Color;
            endColor.a *= isPacket ? 1.0f : 0.5f;
            _lineRenderer.startColor = type.Color;
            _lineRenderer.endColor = endColor;
            _lineRenderer.enabled = true;
        }

        /// @brief 折れ線を揺らさずにそのまま描く（予測線などに使う）。終わりに向かって薄くなる
        /// @param corners 折れ線の点（始点、曲がり目、終点の順）。2 つ以上
        /// @param color 始めの色。終わりでは不透明度が 4 分の 1 になる
        /// @param width 線の太さ（m）
        public void DrawStraight(IReadOnlyList<Vector3> corners, Color color, float width)
        {
            if (corners == null || corners.Count < 2)
            {
                Hide();
                return;
            }

            // NaN や無限大を含む点と、前の点に近すぎる点（向きが決まらず線が乱れる）は飛ばす
            int count = 0;
            for (int i = 0; i < corners.Count && count < MaxPoints; ++i)
            {
                Vector3 point = corners[i];
                if (!IsFinite(point) || (count > 0 && (point - _points[count - 1]).magnitude < MinSegmentLength))
                {
                    continue;
                }
                _points[count] = point;
                ++count;
            }
            if (count < 2)
            {
                Hide();
                return;
            }

            _lineRenderer.positionCount = count;
            _lineRenderer.SetPositions(_points);
            _lineRenderer.widthMultiplier = width;

            Color endColor = color;
            endColor.a *= StraightEndAlphaScale;
            _lineRenderer.startColor = color;
            _lineRenderer.endColor = endColor;
            _lineRenderer.enabled = true;
        }

        /// @brief 線を消す
        public void Hide()
        {
            if (_lineRenderer != null)
            {
                _lineRenderer.enabled = false;
            }
        }

        private void Awake()
        {
            _lineRenderer = GetComponent<LineRenderer>();
        }

        private static bool IsFinite(Vector3 point)
        {
            return !float.IsNaN(point.x) && !float.IsNaN(point.y) && !float.IsNaN(point.z)
                && !float.IsInfinity(point.x) && !float.IsInfinity(point.y) && !float.IsInfinity(point.z);
        }

        private static Material GetDefaultMaterial()
        {
            if (_defaultMaterial == null)
            {
                _defaultMaterial = new Material(Shader.Find(DefaultShaderName));
                _defaultMaterial.name = "WaveLine (Default)";
            }
            return _defaultMaterial;
        }
    }
}
