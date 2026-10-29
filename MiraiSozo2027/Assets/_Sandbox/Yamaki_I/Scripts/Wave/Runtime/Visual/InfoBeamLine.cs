using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Aura.Wave
{
    /// @brief LineRenderer で、折れ線に沿って正弦波の線を描く。ビームの今の見た目（BeamLineView）が使う
    /// 「揺れを 0 にする曲がり目」を選べる
    /// 点と線のビームでは、跳ね返った位置だけ揺れを 0 にし、点どうしの小さな曲がり目では揺らしたまま、線に沿って流す
    /// 見せる範囲（WaveRevealVolume）を渡すと、その中にある部分だけを描く。範囲の外で途切れるごとに別の線（子の LineRenderer）を使い、揺れはつながったまま
    /// 揺らさない折れ線（DrawStraight）も描ける。WaveEmitter が、ビームの実体の線（点どうしをつなぐ直線）を見せるのに使う
    [RequireComponent(typeof(LineRenderer))]
    public sealed class InfoBeamLine : MonoBehaviour
    {
        private const int PointsPerWavelength = 12;
        private const int MaxPoints = 512;                  ///< 波をなめらかに描くための点の数の上限。区間がこれより多いときは、区間ごとに 1 つずつ点を置く
        private const int MaxLinePoints = 2048;             ///< 線が持てる点の数の上限。点と線のビームの点（1 本 1024 個まで）と跳ね返った位置を、最後まで描けるようにする
        private const float BeamFadeInLength = 0.3f;        ///< ビームの根元で振幅を 0 から上げる長さ（m）
        private const float CornerFadeLength = 0.3f;        ///< 曲がり目の前後で振幅を 0 にする長さ（m）
        private const float MinSegmentLength = 0.0001f;     ///< これより短い区間は、向きが決まらないので描かない（m）
        private const float MinSideSqrMagnitude = 0.0001f;  ///< 進む向きと up の外積の長さの 2 乗がこれより小さければ、up がほぼ平行で揺れる向きが決まらないとみなす
        private const float BeamEndAlphaScale = 0.5f;       ///< ビームの終わりの不透明度を、始めの何倍にするか
        private const float StraightEndAlphaScale = 0.25f;  ///< 揺らさない線の終わりの不透明度を、始めの何倍にするか
        private const string DefaultShaderName = "Sprites/Default";

        private static Material _defaultMaterial;  ///< マテリアルを指定しないときに使う、頂点カラーで色を付けるマテリアル

        private readonly Vector3[] _points = new Vector3[MaxLinePoints];
        private readonly Vector3[] _runPoints = new Vector3[MaxLinePoints];       ///< 途切れた部分の点を写す、使い回しの配列（LineRenderer には先頭からしか渡せないため）
        private readonly List<LineRenderer> _runLines = new List<LineRenderer>();  ///< 途切れた部分ごとの線。最初の 1 本はこの GameObject の LineRenderer、2 本目からは子に作る
        private LineRenderer _lineRenderer;

        /// @brief 線を持つ GameObject を作る
        /// @param name GameObject の名前
        /// @param parent 親。null ならシーンの直下
        /// @param material 線のマテリアル。null なら頂点カラーの既定のマテリアル
        /// @return 作った InfoBeamLine
        public static InfoBeamLine Create(string name, Transform parent, Material material)
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

            return lineObject.AddComponent<InfoBeamLine>();
        }

        /// @brief 折れ線に沿ってビームの波を描く。揺れを 0 にするのは、fadingCorners が true の曲がり目だけ
        /// @param corners 折れ線の点（始点、曲がり目、終点の順）。2 つ以上
        /// @param fadingCorners corners と同じ数。true の点の前後で揺れを 0 にする（跳ね返った位置など）。null なら間の曲がり目すべて
        /// @param up 波が揺れる面を決める上の向き
        /// @param type 波の種類（色、波長、振幅、太さ）
        /// @param phase 波の位相（m）。corners の最初の点からの道のりで数える。時間とともに増やすと波が流れて見える
        /// @param revealVolumes 見せる範囲。null ならすべて描く。渡すと、どれかの範囲の中にある部分だけを描く（空なら何も描かない）
        public void Draw(IReadOnlyList<Vector3> corners, IReadOnlyList<bool> fadingCorners, Vector3 up, WaveType type, float phase,
            IReadOnlyList<WaveRevealVolume> revealVolumes = null)
        {
            if (type == null || corners == null || corners.Count < 2)
            {
                Hide();
                return;
            }

            // 短すぎる区間（長さが NaN の区間も）を除いて、描く区間の数と長さの合計を求める
            // 区間は点の数の上限に収まる数（MaxLinePoints - 1）まで描き、残りは描かない
            int segmentCount = 0;
            float totalLength = 0.0f;
            for (int i = 1; i < corners.Count && segmentCount < MaxLinePoints - 1; ++i)
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
            // まだ使える点の間の数。区間が MaxPoints より多いとき（細かい点と線のビーム）は、区間ごとに 1 つずつ割り当てて最後まで描く
            int freeIntervals = Mathf.Max(MaxPoints - 1, segmentCount);
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

                bool hasCornerAtStart = segmentIndex > 0 && IsFading(fadingCorners, i - 1);
                bool hasCornerAtEnd = segmentIndex < segmentCount - 1 && IsFading(fadingCorners, i);
                // 2 つ目からの区間の始点は、前の区間の終点と同じなので入れない
                for (int j = segmentIndex == 0 ? 0 : 1; j <= intervals; ++j)
                {
                    float t = (float)j / intervals;
                    float distanceInSegment = t * length;
                    float distance = segmentStartDistance + distanceInSegment;
                    float envelope = Mathf.SmoothStep(0.0f, 1.0f, distance / BeamFadeInLength);
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

            if (revealVolumes == null)
            {
                ShowRun(0, 0, count, type.LineWidth, type.Color, 1.0f, BeamEndAlphaScale);
                HideRunsFrom(1);
                return;
            }
            ShowRevealedRuns(count, type.LineWidth, type.Color, revealVolumes);
        }

        /// @brief 折れ線を揺らさずにそのまま描く（ビームの実体の線を見せるのに使う）。終わりに向かって薄くなる
        /// @param corners 折れ線の点。2 つ以上
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
            for (int i = 0; i < corners.Count && count < MaxLinePoints; ++i)
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

            ShowRun(0, 0, count, width, color, 1.0f, StraightEndAlphaScale);
            HideRunsFrom(1);
        }

        /// @brief 線を消す
        public void Hide()
        {
            HideRunsFrom(0);
        }

        private void Awake()
        {
            _lineRenderer = GetComponent<LineRenderer>();
            _runLines.Add(_lineRenderer);
        }

        /// @brief 点の列のうち、見せる範囲の中にある部分だけを、途切れるごとに別の線で描く
        /// 不透明度は、ビーム全体の始めから終わりへの薄れ方（1 → BeamEndAlphaScale）を、その部分の位置で切り取る（つながって見えるように）
        private void ShowRevealedRuns(int count, float width, Color color, IReadOnlyList<WaveRevealVolume> revealVolumes)
        {
            int runCount = 0;
            int runStart = -1;
            for (int i = 0; i <= count; ++i)
            {
                if (i < count && IsRevealed(_points[i], revealVolumes))
                {
                    if (runStart < 0)
                    {
                        runStart = i;
                    }
                    continue;
                }
                if (runStart < 0)
                {
                    continue;
                }
                // 範囲の外に出た（か、点が尽きた）ので、ここまでを 1 本の線にする。点が 1 つだけの部分は線にならないので描かない
                int runLength = i - runStart;
                if (runLength >= 2)
                {
                    ShowRun(runCount, runStart, runLength, width, color, GetAlphaScale(runStart, count), GetAlphaScale(i - 1, count));
                    ++runCount;
                }
                runStart = -1;
            }
            HideRunsFrom(runCount);
        }

        /// @brief runIndex 本目の線に、_points の start から length 個の点を入れて見せる
        private void ShowRun(int runIndex, int start, int length, float width, Color color, float startAlphaScale, float endAlphaScale)
        {
            LineRenderer line = GetRunLine(runIndex);
            Vector3[] points = _points;
            if (start > 0)
            {
                Array.Copy(_points, start, _runPoints, 0, length);
                points = _runPoints;
            }
            line.positionCount = length;
            line.SetPositions(points);
            line.widthMultiplier = width;

            Color startColor = color;
            startColor.a *= startAlphaScale;
            Color endColor = color;
            endColor.a *= endAlphaScale;
            line.startColor = startColor;
            line.endColor = endColor;
            line.enabled = true;
        }

        /// @brief runIndex 本目の線を返す。足りなければ、この GameObject の LineRenderer と同じ設定で子に作る
        private LineRenderer GetRunLine(int runIndex)
        {
            while (_runLines.Count <= runIndex)
            {
                var runObject = new GameObject(name + " (" + _runLines.Count + ")");
                runObject.transform.SetParent(transform, false);
                var line = runObject.AddComponent<LineRenderer>();
                line.useWorldSpace = _lineRenderer.useWorldSpace;
                line.shadowCastingMode = _lineRenderer.shadowCastingMode;
                line.receiveShadows = _lineRenderer.receiveShadows;
                line.numCapVertices = _lineRenderer.numCapVertices;
                line.numCornerVertices = _lineRenderer.numCornerVertices;
                line.alignment = _lineRenderer.alignment;
                line.textureMode = _lineRenderer.textureMode;
                line.sharedMaterial = _lineRenderer.sharedMaterial;
                line.positionCount = 0;
                line.enabled = false;
                _runLines.Add(line);
            }
            return _runLines[runIndex];
        }

        /// @brief runIndex 本目から後の線を消す
        private void HideRunsFrom(int runIndex)
        {
            for (int i = runIndex; i < _runLines.Count; ++i)
            {
                if (_runLines[i] != null)
                {
                    _runLines[i].enabled = false;
                }
            }
        }

        /// @brief ビーム全体の始めから終わりへの薄れ方で、index 番目の点の不透明度の倍率
        private static float GetAlphaScale(int index, int count)
        {
            return count > 1 ? Mathf.Lerp(1.0f, BeamEndAlphaScale, (float)index / (count - 1)) : 1.0f;
        }

        /// @brief その位置が、見せる範囲のどれかの中か
        private static bool IsRevealed(Vector3 point, IReadOnlyList<WaveRevealVolume> revealVolumes)
        {
            for (int i = 0; i < revealVolumes.Count; ++i)
            {
                if (revealVolumes[i].Contains(point))
                {
                    return true;
                }
            }
            return false;
        }

        /// @brief その曲がり目で揺れを 0 にするか。fadingCorners が null なら、すべての曲がり目で 0 にする
        private static bool IsFading(IReadOnlyList<bool> fadingCorners, int index)
        {
            return fadingCorners == null || (index < fadingCorners.Count && fadingCorners[index]);
        }

        private static bool IsFinite(Vector3 point)
        {
            return !float.IsNaN(point.x) && !float.IsNaN(point.y) && !float.IsNaN(point.z)
                && !float.IsInfinity(point.x) && !float.IsInfinity(point.y) && !float.IsInfinity(point.z);
        }

        /// @brief マテリアルを指定しないときに使う、頂点カラーの既定のマテリアル（FanWaveLine とも共用）
        internal static Material GetDefaultMaterial()
        {
            if (_defaultMaterial == null)
            {
                _defaultMaterial = new Material(Shader.Find(DefaultShaderName));
                _defaultMaterial.name = "InfoBeamLine (Default)";
            }
            return _defaultMaterial;
        }
    }
}
