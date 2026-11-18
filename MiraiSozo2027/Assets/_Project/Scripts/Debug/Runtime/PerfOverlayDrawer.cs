using UnityEngine;

namespace Aura.DebugTools
{
    /// @brief PerfOverlay の画面への描画（IMGUI）だけを受け持つ。文字の見た目・寸法・行の幅の計測・行とグラフの描画
    /// 計測や文字作りは PerfOverlay 側で行い、ここは渡された行・値を描くだけにする（PerfOverlay への逆参照は持たない）
    internal sealed class PerfOverlayDrawer
    {
        private const float BaseScreenHeight = 1080.0f;     ///< 文字の大きさの基準にする画面の高さ
        private const float LineHeightRate = 1.4f;          ///< 文字の大きさに対する、1 行の高さの倍率
        private const float PaddingRate = 0.35f;            ///< 文字の大きさに対する、行の左右の余白の倍率
        private const float GapRate = 0.6f;                 ///< 行と行の間の「あき」の高さの、1 行の高さに対する倍率
        private const float BarWidthRate = 0.2f;            ///< 文字の大きさに対する、グラフの棒 1 本の幅の倍率
        private const float GraphHeightRate = 5.0f;         ///< 文字の大きさに対する、グラフの高さの倍率
        private const float GraphMaxBudgetMultiple = 3.0f;  ///< グラフの一番上が、目標の時間の何倍か
        private const float WarnBudgetMultiple = 2.0f;      ///< 棒が橙色までで済む、目標の時間の倍率
        private const float DashRate = 0.4f;                ///< 文字の大きさに対する、点線の 1 つぶんの長さの倍率
        private const int MinFontSize = 8;                  ///< 文字の大きさの下限
        private const float BackgroundAlpha = 0.6f;         ///< 背景の不透明度
        private const float LineSpacingDivisor = 14.0f;     ///< 文字の大きさをこれで割った値を、行と行の間（px）にする
        private const float FallbackSpaceRate = 0.3f;       ///< 半角スペースの幅が測れないときの、文字の大きさに対する倍率
        private const float LabelExtraWidth = 2.0f;         ///< 文字の欄に足す余白（px）。測った幅ちょうどだと端が欠けることがあるため

        private static readonly Color BackgroundColor = new Color(0.31f, 0.31f, 0.31f, BackgroundAlpha);  ///< 行の背景色
        private static readonly Color BarGoodColor = new Color32(0x63, 0x99, 0x22, 0xFF);  ///< 棒の色（間に合っている）
        private static readonly Color BarWarnColor = new Color32(0xEF, 0x9F, 0x27, 0xFF);  ///< 棒の色（目標の 2 倍まで）。PerfOverlay の文字の注意の色（WarnColor）と同じ値
        private static readonly Color BarBadColor = new Color32(0xE2, 0x4B, 0x4A, 0xFF);   ///< 棒の色（それ以上）
        private static readonly Color TargetLineColor = new Color32(0xF0, 0x95, 0x95, 0xFF);  ///< 目標の点線の色（ピンク）

        private readonly GUIContent _content = new GUIContent();  ///< 文字の幅を測る・描くための入れ物（使い回す）
        private Texture2D _whiteTexture;        ///< 背景・棒に使う 1x1 の白い画像
        private GUIStyle _style;                ///< 文字の見た目
        private int _appliedFontSize;           ///< 今の見た目に使っている文字の大きさ
        private float _spaceWidth;              ///< 半角スペース 1 つの幅
        private float _lineHeight;              ///< 1 行の高さ
        private float _padding;                 ///< 行の左右の余白
        private float _margin;                  ///< 画面の端からの余白
        private float _lineSpacing;             ///< 行と行の間（1px 程度）
        private int _barWidth;                  ///< グラフの棒 1 本の幅

        public float LineHeight => _lineHeight;         ///< 1 行の高さ
        public float Padding => _padding;               ///< 行の左右の余白
        public float Margin => _margin;                 ///< 画面の端からの余白
        public float LineSpacing => _lineSpacing;       ///< 行と行の間
        public float GraphHeight => _appliedFontSize * GraphHeightRate;  ///< グラフの高さ（今の文字の大きさに合わせる）

        /// @brief 白い 1x1 の画像を 1 枚だけ作る。背景と棒の色は GUI.color で変えて使い回す
        /// Texture2D はコンポーネントの生成時ではなく Awake 以降で作る必要があるため、PerfOverlay.Awake から呼ぶ
        public PerfOverlayDrawer()
        {
            _whiteTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _whiteTexture.SetPixel(0, 0, Color.white);
            _whiteTexture.Apply();
            _whiteTexture.hideFlags = HideFlags.HideAndDontSave;
        }

        /// @brief 持っている画像を手放す。PerfOverlay.OnDestroy から呼ぶ
        public void Release()
        {
            if (_whiteTexture != null)
            {
                Object.Destroy(_whiteTexture);
                _whiteTexture = null;
            }
        }

        /// @brief 文字の大きさが変わった（画面の高さが変わった）ら、見た目と寸法を作り直す。作り直したときは true（行の幅を測り直す必要がある）
        public bool EnsureStyle(int baseFontSize)
        {
            int fontSize = Mathf.Max(MinFontSize, Mathf.RoundToInt(baseFontSize * Screen.height / BaseScreenHeight));
            if (_style != null && fontSize == _appliedFontSize)
            {
                return false;
            }

            _appliedFontSize = fontSize;
            _style = new GUIStyle(GUI.skin.label);
            _style.fontSize = fontSize;
            _style.alignment = TextAnchor.MiddleLeft;
            _style.wordWrap = false;
            _style.richText = false;
            _style.clipping = TextClipping.Overflow;
            _style.padding = new RectOffset(0, 0, 0, 0);
            _style.margin = new RectOffset(0, 0, 0, 0);
            _style.normal.textColor = Color.white;

            _lineHeight = Mathf.Ceil(fontSize * LineHeightRate);
            _padding = Mathf.Round(fontSize * PaddingRate);
            _margin = _padding;
            _lineSpacing = Mathf.Max(1.0f, Mathf.Round(fontSize / LineSpacingDivisor));
            _barWidth = Mathf.Max(1, Mathf.RoundToInt(fontSize * BarWidthRate));

            // 半角スペースの幅は、「a a」と「aa」の幅の差から求める
            _content.text = "a a";
            float withSpace = _style.CalcSize(_content).x;
            _content.text = "aa";
            float withoutSpace = _style.CalcSize(_content).x;
            _spaceWidth = withSpace - withoutSpace;
            if (_spaceWidth <= 0.0f)
            {
                _spaceWidth = fontSize * FallbackSpaceRate;
            }

            return true;
        }

        /// @brief 1 行の幅を測る。幅を測る処理は重いので、文字を作り直したときと文字の大きさが変わったときだけ呼ぶ
        public void MeasureLine(TextLine line)
        {
            float total = 0.0f;
            for (int i = 0; i < line.Count; i++)
            {
                _content.text = line.Texts[i];
                line.Widths[i] = _style.CalcSize(_content).x;
                total += line.SpacesBefore[i] * _spaceWidth + line.Widths[i];
            }
            line.TotalWidth = total;
        }

        /// @brief 1 行を描き、次の行の y を返す
        public float DrawLine(TextLine line, float x, float y)
        {
            if (line.IsGap)
            {
                return y + _lineHeight * GapRate;
            }

            GUI.color = BackgroundColor;
            GUI.DrawTexture(new Rect(x, y, line.TotalWidth + _padding * 2.0f, _lineHeight), _whiteTexture);

            float cursorX = x + _padding;
            for (int i = 0; i < line.Count; i++)
            {
                cursorX += line.SpacesBefore[i] * _spaceWidth;
                _content.text = line.Texts[i];
                GUI.color = line.Colors[i];
                GUI.Label(new Rect(cursorX, y, line.Widths[i] + LabelExtraWidth, _lineHeight), _content, _style);
                cursorX += line.Widths[i];
            }

            return y + _lineHeight + _lineSpacing;
        }

        /// @brief フレームごとの棒グラフと、目標の点線を描く
        /// frameMsSamples は輪になった記録で、frameHead が次に書き込む場所（= いちばん古い値）。budgetMs は 1 フレームの目標の時間（ms）
        public void DrawGraph(float left, float top, float height, float[] frameMsSamples, int frameHead, float budgetMs)
        {
            if (frameMsSamples == null)
            {
                return;
            }

            int length = frameMsSamples.Length;
            float width = length * _barWidth;
            float maxMs = budgetMs * GraphMaxBudgetMultiple;
            float bottom = top + height;

            GUI.color = BackgroundColor;
            GUI.DrawTexture(new Rect(left, top, width, height), _whiteTexture);

            for (int i = 0; i < length; i++)
            {
                // 古い順に左から並べる
                float ms = frameMsSamples[(frameHead + i) % length];
                if (ms <= 0.0f)
                {
                    continue;
                }

                float barHeight = Mathf.Min(ms / maxMs, 1.0f) * height;
                if (ms <= budgetMs)
                {
                    GUI.color = BarGoodColor;
                }
                else if (ms <= budgetMs * WarnBudgetMultiple)
                {
                    GUI.color = BarWarnColor;
                }
                else
                {
                    GUI.color = BarBadColor;
                }
                GUI.DrawTexture(new Rect(left + i * _barWidth, bottom - barHeight, _barWidth, barHeight), _whiteTexture);
            }

            // 目標の時間の点線
            float targetY = bottom - height / GraphMaxBudgetMultiple;
            float dashLength = Mathf.Max(2.0f, _appliedFontSize * DashRate);
            GUI.color = TargetLineColor;
            for (float dashX = 0.0f; dashX < width; dashX += dashLength * 2.0f)
            {
                float dashWidth = Mathf.Min(dashLength, width - dashX);
                GUI.DrawTexture(new Rect(left + dashX, targetY, dashWidth, 1.0f), _whiteTexture);
            }
        }
    }
}
