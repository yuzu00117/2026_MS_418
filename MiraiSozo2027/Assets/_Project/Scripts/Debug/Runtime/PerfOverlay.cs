using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Aura.Inspector;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Aura.DebugTools
{
    /// @brief 画面に、動きの重さ（性能）を文字とグラフで出す。開発用（Editor と Development Build だけ。自動で作られる）
    /// F2 で全体を出す・消す。Shift+F2 でグラフだけを出す・消す（グラフは始めは出ている）
    /// 左上の各行の意味
    ///   FPS        : 1 秒に何回、画面を描き直せているか。「58 / 60」は、上限 60 のところ 58 出ている、の意味（約 0.5 秒の平均）
    ///   CPU        : 1 フレームのうち、メインスレッド（ゲームの処理の本体）が使った時間（ms）。
    ///                % は、1 フレームに使ってよい時間（上限 FPS から計算）の何割を使ったか。100% を超えると橙色になり、間に合っていない
    ///                max は、直近 約 1 秒の最大値。たまに重くなる所を見つける目安
    ///   GPU        : 1 フレームのうち、GPU（絵を描く専用の部品）が使った時間（ms）。取れない環境では「N/A」と出る
    ///   Draw Calls : 1 フレームで GPU に「描いて」と頼んだ回数。多いほど CPU が重くなる
    ///   GC Alloc   : 1 フレームで新しく確保したメモリ。多いとカクつきの原因（あとでまとめて掃除する GC が走るため）。0 に近いほど良い
    ///   [名前] ... : 他の機能が PerfOverlayLines で足した行（薄い紫色）
    /// 右上の各行の意味
    ///   Memory     : 使っているメモリ / PC のメモリ全体
    ///   CPU / GPU  : この PC の CPU と GPU の名前
    ///   Resolution : 画面の大きさと、画面を書き換える速さ（Hz）
    ///   VSync      : 画面の書き換えに合わせて描く設定。オンだと上限 FPS は「書き換える速さ ÷ 数」になる
    ///   Target FPS : Application.targetFrameRate で決めた、FPS の上限（- は決めていない）
    ///   Unity      : Unity のバージョン
    /// 左下のグラフ: 直近のフレームごとに 1 本の棒。高いほどそのフレームに時間がかかった。
    ///   緑 = 上限 FPS に間に合っている／橙 = 目標の 2 倍まで／赤 = それ以上。ピンクの点線が目標の時間
    /// 上限 FPS は毎フレーム調べ直す（VSync または targetFrameRate が決まっていて、実行中に変わってもよい）。
    /// どちらも決まっていないときは「Unlimited」と出て、画面の書き換える速さを目標の基準にする
    /// 開発用ビルド以外では、自動では作られず何も出ない
    [DisallowMultipleComponent]
    public sealed class PerfOverlay : MonoBehaviour
    {
        private const int MaxSegments = 4;                  ///< 1 行を作る、色分けした文字の部品の最大数
        private const int HistoryLength = 1024;             ///< CPU・GPU の最大値を調べるために覚えておく、フレームの数
        private const float MaxWindowSeconds = 1.0f;        ///< max を調べる期間（秒）
        private const float FpsWindowSeconds = 0.5f;        ///< FPS を平均する期間（秒）
        private const float BaseScreenHeight = 1080.0f;     ///< 文字の大きさの基準にする画面の高さ
        private const double DefaultRefreshRate = 60.0;     ///< 画面の書き換える速さが取れないときに使う値（Hz）
        private const float NanosecondsToMilliseconds = 0.000001f;  ///< ns を ms にする倍率
        private const float BytesPerKilo = 1024.0f;         ///< 1KB のバイト数
        private const float BytesPerMega = 1048576.0f;      ///< 1MB のバイト数
        private const float LineHeightRate = 1.4f;          ///< 文字の大きさに対する、1 行の高さの倍率
        private const float PaddingRate = 0.35f;            ///< 文字の大きさに対する、行の左右の余白の倍率
        private const float GapRate = 0.6f;                 ///< 行と行の間の「あき」の高さの、1 行の高さに対する倍率
        private const float BarWidthRate = 0.2f;            ///< 文字の大きさに対する、グラフの棒 1 本の幅の倍率
        private const float GraphHeightRate = 5.0f;         ///< 文字の大きさに対する、グラフの高さの倍率
        private const float GraphMaxBudgetMultiple = 3.0f;  ///< グラフの一番上が、目標の時間の何倍か
        private const float WarnBudgetMultiple = 2.0f;      ///< 棒が橙色までで済む、目標の時間の倍率
        private const float DashRate = 0.4f;                ///< 文字の大きさに対する、点線の 1 つぶんの長さの倍率
        private const float OverBudgetPercent = 100.0f;     ///< これを超えると % を橙色にする
        private const int MinFontSize = 8;                  ///< 文字の大きさの下限
        private const float BackgroundAlpha = 0.6f;         ///< 背景の不透明度
        private const float LineSpacingDivisor = 14.0f;     ///< 文字の大きさをこれで割った値を、行と行の間（px）にする
        private const float FallbackSpaceRate = 0.3f;       ///< 半角スペースの幅が測れないときの、文字の大きさに対する倍率
        private const float LabelExtraWidth = 2.0f;         ///< 文字の欄に足す余白（px）。測った幅ちょうどだと端が欠けることがあるため

        private static readonly CultureInfo Culture = CultureInfo.InvariantCulture;  ///< 数字を文字にする書式（国によって小数点の記号が変わらないようにする）

        private static readonly Color BackgroundColor = new Color(0.31f, 0.31f, 0.31f, BackgroundAlpha);  ///< 行の背景色
        private static readonly Color TextColor = Color.white;                    ///< ふつうの文字の色
        private static readonly Color SecondaryColor = new Color(0.7f, 0.7f, 0.7f, 1.0f);  ///< 補足の文字の色（灰色）
        private static readonly Color WarnColor = new Color32(0xEF, 0x9F, 0x27, 0xFF);     ///< 注意の色（橙）
        private static readonly Color PluginColor = new Color32(0xAF, 0xA9, 0xEC, 0xFF);   ///< 他の機能が足した行の色（薄い紫）
        private static readonly Color BarGoodColor = new Color32(0x63, 0x99, 0x22, 0xFF);  ///< 棒の色（間に合っている）
        private static readonly Color BarWarnColor = WarnColor;                            ///< 棒の色（目標の 2 倍まで）。注意の色と同じ
        private static readonly Color BarBadColor = new Color32(0xE2, 0x4B, 0x4A, 0xFF);   ///< 棒の色（それ以上）
        private static readonly Color TargetLineColor = new Color32(0xF0, 0x95, 0x95, 0xFF);  ///< 目標の点線の色（ピンク）

        [InspectorLabel("文字の更新間隔（秒）")]
        [Tooltip("文字を作り直す間隔（秒）。0.25 なら 1 秒に 4 回。小さいほど細かく変わるが、少し重くなる")]
        [SerializeField, Min(0.05f)] private float _textRefreshSeconds = 0.25f;  ///< 文字を作り直す間隔（秒）

        [InspectorLabel("グラフのフレーム数")]
        [Tooltip("グラフに出す、直近のフレームの数（棒の本数）")]
        [SerializeField, Min(2)] private int _graphFrameCount = 120;  ///< グラフの棒の本数

        [InspectorLabel("文字の大きさ（1080p のとき）")]
        [Tooltip("画面の高さが 1080 のときの文字の大きさ（px）。画面の高さに合わせて自動で拡大・縮小される")]
        [SerializeField, Min(6)] private int _baseFontSize = 20;  ///< 画面の高さが 1080 のときの文字の大きさ

        [InspectorLabel("全体を出す・消すキー")]
        [Tooltip("押すたびに、全体を出す・消すを切り替えるキー。Shift と一緒に押すと、グラフだけを切り替える")]
        [SerializeField] private Key _toggleKey = Key.F2;  ///< 出す・消すを切り替えるキー

        /// @brief 色分けした文字の部品を並べた、1 行ぶんの文字
        private sealed class TextLine
        {
            public string[] Texts { get; } = new string[MaxSegments];   ///< 部品の文字
            public Color[] Colors { get; } = new Color[MaxSegments];    ///< 部品の色
            public int[] SpacesBefore { get; } = new int[MaxSegments];  ///< 部品の前に空ける、半角スペースの数
            public float[] Widths { get; } = new float[MaxSegments];    ///< 部品の幅（描く前に測る）
            public int Count { get; private set; }  ///< 部品の数
            public bool IsGap { get; set; }         ///< 文字がなく、あきだけの行か
            public float TotalWidth { get; set; }   ///< 部品を並べた全体の幅（背景の余白は含まない）

            /// 中身を空にして、使い回せる状態にする
            public void Reset()
            {
                Count = 0;
                IsGap = false;
                TotalWidth = 0.0f;
            }

            /// 部品を 1 つ足す
            public void Add(string text, Color color, int spaces = 0)
            {
                if (Count >= MaxSegments)
                {
                    return;
                }

                Texts[Count] = text;
                Colors[Count] = color;
                SpacesBefore[Count] = spaces;
                Count++;
            }
        }

        private readonly List<TextLine> _leftLines = new List<TextLine>();    ///< 左上の行（使い回す）
        private readonly List<TextLine> _rightLines = new List<TextLine>();   ///< 右上の行（使い回す）
        private readonly TextLine _graphHintLine = new TextLine();            ///< グラフの上の「Shift+F2: Hide Graph」
        private readonly TextLine _graphCaptionLine = new TextLine();         ///< グラフの下の説明
        private readonly TextLine _showGraphLine = new TextLine();            ///< グラフが消えているときの「Shift+F2: Show Graph」
        private readonly List<string> _pluginTexts = new List<string>();      ///< 他の機能が足した行の文字（使い回す）
        private readonly StringBuilder _builder = new StringBuilder(128);     ///< 文字を作る入れ物（使い回す）
        private readonly GUIContent _content = new GUIContent();              ///< 文字の幅を測る・描くための入れ物（使い回す）
        private readonly FrameTiming[] _frameTimings = new FrameTiming[1];    ///< GPU の時間を受け取る入れ物（使い回す）
        private int _leftCount;    ///< 左上の行のうち、今使っている数
        private int _rightCount;   ///< 右上の行のうち、今使っている数
        private Key _hintKey = Key.None;  ///< キーの案内の文字を作ったときのキー（変わったときだけ作り直す）
        private string _hideText;         ///< 左上の「F2: Hide」

        private ProfilerRecorder _cpuRecorder;        ///< メインスレッドの時間（ns）
        private ProfilerRecorder _drawCallRecorder;   ///< 描く回数
        private ProfilerRecorder _gcAllocRecorder;    ///< 1 フレームで確保したメモリ（バイト）
        private ProfilerRecorder _memoryRecorder;     ///< 使っているメモリの合計（バイト）

        private readonly float[] _historyTimes = new float[HistoryLength];  ///< 覚えておいた時刻
        private readonly float[] _cpuHistory = new float[HistoryLength];    ///< 覚えておいた CPU の時間（ms）。取れなかったら -1
        private readonly float[] _gpuHistory = new float[HistoryLength];    ///< 覚えておいた GPU の時間（ms）。取れなかったら -1
        private int _historyHead;      ///< 次に書き込む場所

        private float[] _frameMsSamples;  ///< グラフ用の、フレームごとの時間（ms）
        private int _frameHead;           ///< グラフ用の、次に書き込む場所

        private bool _isVisible = true;         ///< 全体を出しているか
        private bool _isGraphVisible = true;    ///< グラフを出しているか
        private float _textTimer = float.MaxValue;  ///< 前に文字を作り直してからの時間（最初はすぐ作り直す）
        private int _fpsFrames;                 ///< FPS を平均するための、数えたフレーム数
        private float _fpsTime;                 ///< FPS を平均するための、たまった時間
        private float _fps;                     ///< 今の FPS
        private double _capFps;                 ///< FPS の上限（Unlimited のときは、画面の書き換える速さ）
        private double _refreshHz;              ///< 画面の書き換える速さ（Hz）
        private bool _isUnlimited;              ///< 上限が決まっていないか
        private float _budgetMs;                ///< 1 フレームに使ってよい時間（ms）
        private float _cpuSum;                  ///< 前に文字を作り直してからの、CPU の時間の合計
        private int _cpuCount;                  ///< その回数
        private float _gpuSum;                  ///< 前に文字を作り直してからの、GPU の時間の合計
        private int _gpuCount;                  ///< その回数

        private Texture2D _whiteTexture;        ///< 背景・棒に使う 1x1 の白い画像
        private GUIStyle _style;                ///< 文字の見た目
        private int _appliedFontSize;           ///< 今の見た目に使っている文字の大きさ
        private bool _needsMeasure = true;      ///< 文字の幅を測り直す必要があるか
        private float _spaceWidth;              ///< 半角スペース 1 つの幅
        private float _lineHeight;              ///< 1 行の高さ
        private float _padding;                 ///< 行の左右の余白
        private float _margin;                  ///< 画面の端からの余白
        private float _lineSpacing;             ///< 行と行の間（1px 程度）
        private int _barWidth;                  ///< グラフの棒 1 本の幅

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        /// シーンを読み込んだあとに、性能表示を 1 つ自動で作る。シーンが変わっても残す
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateOnLoad()
        {
            if (FindFirstObjectByType<PerfOverlay>() != null)
            {
                return;
            }

            GameObject overlayObject = new GameObject("PerfOverlay");
            overlayObject.AddComponent<PerfOverlay>();
            DontDestroyOnLoad(overlayObject);
        }
#endif

        private void Awake()
        {
            // 白い 1x1 の画像を 1 枚だけ作り、背景と棒の色は GUI.color で変えて使い回す
            _whiteTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _whiteTexture.SetPixel(0, 0, Color.white);
            _whiteTexture.Apply();
            _whiteTexture.hideFlags = HideFlags.HideAndDontSave;

            for (int i = 0; i < HistoryLength; i++)
            {
                _cpuHistory[i] = -1.0f;
                _gpuHistory[i] = -1.0f;
                _historyTimes[i] = float.NegativeInfinity;
            }
        }

        private void OnEnable()
        {
            StopRecorders();
            _cpuRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread");
            _drawCallRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");
            _gcAllocRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            _memoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory");
            _textTimer = float.MaxValue;
        }

        private void OnDisable()
        {
            StopRecorders();
        }

        private void OnDestroy()
        {
            StopRecorders();
            if (_whiteTexture != null)
            {
                Destroy(_whiteTexture);
                _whiteTexture = null;
            }
        }

        private void Update()
        {
            HandleInput();
            if (!_isVisible)
            {
                return;
            }

            float deltaTime = Time.unscaledDeltaTime;
            UpdateFps(deltaTime);
            UpdateCap();
            PushFrameSample(deltaTime);
            SampleCpuAndGpu(Time.unscaledTime);

            _textTimer += deltaTime;
            if (_textTimer >= _textRefreshSeconds)
            {
                _textTimer = 0.0f;
                RebuildText();
            }
        }

        /// 計測を止めて、持っているものを手放す
        private void StopRecorders()
        {
            DisposeRecorder(ref _cpuRecorder);
            DisposeRecorder(ref _drawCallRecorder);
            DisposeRecorder(ref _gcAllocRecorder);
            DisposeRecorder(ref _memoryRecorder);
        }

        private static void DisposeRecorder(ref ProfilerRecorder recorder)
        {
            if (recorder.Valid)
            {
                recorder.Dispose();
            }
            recorder = default;
        }

        /// 計測した値を取る。まだ値がない・計測できないときは false
        private static bool TryGetValue(ProfilerRecorder recorder, out long value)
        {
            if (recorder.Valid && recorder.Count > 0)
            {
                value = recorder.LastValue;
                return true;
            }

            value = 0;
            return false;
        }

        /// キーを見て、全体・グラフの出す・消すを切り替える
        private void HandleInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null || _toggleKey == Key.None)
            {
                return;
            }

            if (!keyboard[_toggleKey].wasPressedThisFrame)
            {
                return;
            }

            // Shift を押しているときは、全体は切り替えずにグラフだけ切り替える
            bool isShiftHeld = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
            if (isShiftHeld)
            {
                _isGraphVisible = !_isGraphVisible;
            }
            else
            {
                // 消している間の古い値が、出し直したときの平均に混ざらないようにする
                _isVisible = !_isVisible;
                ResetTimingAverages();
                if (_isVisible)
                {
                    ResetFrameSamples();
                }
            }

            // 切り替えたら、すぐに文字を作り直す
            _textTimer = float.MaxValue;
        }

        /// CPU・GPU の平均を出すための合計と回数を 0 に戻す
        private void ResetTimingAverages()
        {
            _cpuSum = 0.0f;
            _cpuCount = 0;
            _gpuSum = 0.0f;
            _gpuCount = 0;
        }

        /// グラフと FPS の記録を空にする。
        /// 消している間は記録しないので、残っているのは消す前の古い値だけ。出し直したときに今の値と混ざらないようにする
        private void ResetFrameSamples()
        {
            if (_frameMsSamples != null)
            {
                System.Array.Clear(_frameMsSamples, 0, _frameMsSamples.Length);
            }
            _frameHead = 0;
            _fpsFrames = 0;
            _fpsTime = 0.0f;
            _fps = 0.0f;
        }

        /// FPS を、約 0.5 秒ぶんの平均で出す
        private void UpdateFps(float deltaTime)
        {
            _fpsFrames++;
            _fpsTime += deltaTime;
            if (_fpsTime >= FpsWindowSeconds)
            {
                _fps = _fpsFrames / _fpsTime;
                _fpsFrames = 0;
                _fpsTime = 0.0f;
            }
        }

        /// FPS の上限と、1 フレームに使ってよい時間を決める。他のコードが実行中に変えるので、毎フレーム調べ直す
        private void UpdateCap()
        {
            double refreshRate = Screen.currentResolution.refreshRateRatio.value;
            if (refreshRate <= 0.0)
            {
                refreshRate = DefaultRefreshRate;
            }
            _refreshHz = refreshRate;

            int vSyncCount = QualitySettings.vSyncCount;
            if (vSyncCount > 0)
            {
                _capFps = refreshRate / vSyncCount;
                _isUnlimited = false;
            }
            else if (Application.targetFrameRate > 0)
            {
                _capFps = Application.targetFrameRate;
                _isUnlimited = false;
            }
            else
            {
                _capFps = refreshRate;
                _isUnlimited = true;
            }

            _budgetMs = (float)(1000.0 / _capFps);
        }

        /// グラフ用に、このフレームの時間（ms）を覚える
        private void PushFrameSample(float deltaTime)
        {
            int length = Mathf.Max(2, _graphFrameCount);
            if (_frameMsSamples == null || _frameMsSamples.Length != length)
            {
                _frameMsSamples = new float[length];
                _frameHead = 0;
            }

            _frameMsSamples[_frameHead] = deltaTime * 1000.0f;
            _frameHead = (_frameHead + 1) % length;
        }

        /// CPU と GPU の時間を取って、平均と max を出すために覚える
        private void SampleCpuAndGpu(float now)
        {
            float cpuMs = -1.0f;
            if (TryGetValue(_cpuRecorder, out long cpuNanoseconds))
            {
                cpuMs = cpuNanoseconds * NanosecondsToMilliseconds;
                _cpuSum += cpuMs;
                _cpuCount++;
            }

            // GPU の時間は、取れない環境（0 が返る）では覚えない
            float gpuMs = -1.0f;
            FrameTimingManager.CaptureFrameTimings();
            uint timingCount = FrameTimingManager.GetLatestTimings(1, _frameTimings);
            if (timingCount > 0 && _frameTimings[0].gpuFrameTime > 0.0)
            {
                gpuMs = (float)_frameTimings[0].gpuFrameTime;
                _gpuSum += gpuMs;
                _gpuCount++;
            }

            _historyTimes[_historyHead] = now;
            _cpuHistory[_historyHead] = cpuMs;
            _gpuHistory[_historyHead] = gpuMs;
            _historyHead = (_historyHead + 1) % HistoryLength;
        }

        /// 直近 約 1 秒の最大値を返す。1 つも値がなければ -1
        private float GetRecentMax(float[] history)
        {
            float threshold = Time.unscaledTime - MaxWindowSeconds;
            float max = -1.0f;
            for (int i = 0; i < HistoryLength; i++)
            {
                if (_historyTimes[i] >= threshold && history[i] > max)
                {
                    max = history[i];
                }
            }
            return max;
        }

        private TextLine BeginLeft()
        {
            return Acquire(_leftLines, _leftCount++);
        }

        private TextLine BeginRight()
        {
            return Acquire(_rightLines, _rightCount++);
        }

        private static TextLine Acquire(List<TextLine> lines, int index)
        {
            while (lines.Count <= index)
            {
                lines.Add(new TextLine());
            }

            TextLine line = lines[index];
            line.Reset();
            return line;
        }

        /// 表示する文字を全部作り直す（1 秒に数回だけ呼ぶ）
        private void RebuildText()
        {
            UpdateKeyHints();
            BuildLeftLines();
            BuildRightLines();

            // グラフの下の説明（目標の時間は実行中に変わるので毎回作る）
            _builder.Clear();
            _builder.Append("Frame Time (dashed = ").Append(_budgetMs.ToString("F1", Culture)).Append("ms target)");
            _graphCaptionLine.Reset();
            _graphCaptionLine.Add(_builder.ToString(), SecondaryColor);

            _needsMeasure = true;
        }

        /// キーの案内の文字を作る。中身はキーで決まるので、キーが変わったときだけ作り直す
        private void UpdateKeyHints()
        {
            if (_hintKey == _toggleKey && _hideText != null)
            {
                return;
            }

            _hintKey = _toggleKey;
            string keyName = _toggleKey.ToString();
            _hideText = keyName + ": Hide";
            _graphHintLine.Reset();
            _graphHintLine.Add("Shift+" + keyName + ": Hide Graph", SecondaryColor);
            _showGraphLine.Reset();
            _showGraphLine.Add("Shift+" + keyName + ": Show Graph", SecondaryColor);
        }

        /// 左上の行を作る
        private void BuildLeftLines()
        {
            _leftCount = 0;

            // FPS
            float fps = _fps > 0.0f ? _fps : 1.0f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f);
            _builder.Clear();
            _builder.Append(Mathf.RoundToInt(fps)).Append(" / ");
            if (_isUnlimited)
            {
                _builder.Append("Unlimited");
            }
            else
            {
                _builder.Append(Mathf.RoundToInt((float)_capFps));
            }
            _builder.Append(" FPS");
            BeginLeft().Add(_builder.ToString(), TextColor);

            // CPU・GPU
            bool hasCpu = _cpuCount > 0;
            AddTimingLine("CPU", hasCpu, hasCpu ? _cpuSum / _cpuCount : 0.0f, GetRecentMax(_cpuHistory));
            float gpuMax = GetRecentMax(_gpuHistory);
            bool hasGpu = _gpuCount > 0 && gpuMax >= 0.0f;
            AddTimingLine("GPU", hasGpu, hasGpu ? _gpuSum / _gpuCount : 0.0f, gpuMax);
            ResetTimingAverages();

            // 描く回数
            _builder.Clear();
            _builder.Append("Draw Calls ");
            if (TryGetValue(_drawCallRecorder, out long drawCalls))
            {
                _builder.Append(drawCalls.ToString(Culture)).Append("/frame");
            }
            else
            {
                _builder.Append("N/A");
            }
            BeginLeft().Add(_builder.ToString(), TextColor);

            BeginLeft().IsGap = true;

            // 1 フレームで確保したメモリ
            _builder.Clear();
            _builder.Append("GC Alloc ");
            if (TryGetValue(_gcAllocRecorder, out long gcBytes))
            {
                AppendBytes(_builder, gcBytes);
                _builder.Append("/frame");
            }
            else
            {
                _builder.Append("N/A");
            }
            BeginLeft().Add(_builder.ToString(), TextColor);

            // 他の機能が足した行
            PerfOverlayLines.Collect(_pluginTexts);
            if (_pluginTexts.Count > 0)
            {
                BeginLeft().IsGap = true;
                for (int i = 0; i < _pluginTexts.Count; i++)
                {
                    BeginLeft().Add(_pluginTexts[i], PluginColor);
                }
            }

            BeginLeft().IsGap = true;
            BeginLeft().Add(_hideText, SecondaryColor);
        }

        /// 右上の行を作る
        private void BuildRightLines()
        {
            _rightCount = 0;

            _builder.Clear();
            if (TryGetValue(_memoryRecorder, out long usedBytes))
            {
                _builder.Append(Mathf.RoundToInt(usedBytes / BytesPerMega)).Append("MB");
            }
            else
            {
                _builder.Append("N/A");
            }
            _builder.Append(" / ").Append(SystemInfo.systemMemorySize).Append("MB");
            AddRightLine("Memory", _builder.ToString());
            AddRightLine("CPU", SystemInfo.processorType);
            AddRightLine("GPU", SystemInfo.graphicsDeviceName);

            _builder.Clear();
            _builder.Append(Screen.width).Append('x').Append(Screen.height).Append(" @")
                .Append(Mathf.RoundToInt((float)_refreshHz)).Append("Hz");
            AddRightLine("Resolution", _builder.ToString());

            int vSyncCount = QualitySettings.vSyncCount;
            TextLine vSyncLine = BeginRight();
            vSyncLine.Add("VSync", SecondaryColor);
            if (vSyncCount > 0)
            {
                _builder.Clear();
                _builder.Append("On (").Append(vSyncCount.ToString(Culture)).Append(')');
                vSyncLine.Add(_builder.ToString(), TextColor, 1);
            }
            else
            {
                vSyncLine.Add("Off", TextColor, 1);
            }
            vSyncLine.Add("Target FPS", SecondaryColor, 3);
            vSyncLine.Add(Application.targetFrameRate > 0 ? Application.targetFrameRate.ToString(Culture) : "-", TextColor, 1);

            AddRightLine("Unity", Application.unityVersion);
        }

        /// 「CPU 12.4ms/frame (74% of 16.7ms)  max 31.8ms」の形の行を 1 つ足す
        private void AddTimingLine(string label, bool hasValue, float averageMs, float maxMs)
        {
            TextLine line = BeginLeft();
            _builder.Clear();
            _builder.Append(label).Append(' ');
            if (!hasValue)
            {
                _builder.Append("N/A");
                line.Add(_builder.ToString(), TextColor);
                return;
            }

            _builder.Append(averageMs.ToString("F1", Culture)).Append("ms/frame");
            line.Add(_builder.ToString(), TextColor);
            line.Add("(", SecondaryColor, 1);

            float percent = averageMs / _budgetMs * 100.0f;
            _builder.Clear();
            _builder.Append(Mathf.RoundToInt(percent)).Append('%');
            line.Add(_builder.ToString(), percent > OverBudgetPercent ? WarnColor : TextColor);

            _builder.Clear();
            _builder.Append("of ").Append(_budgetMs.ToString("F1", Culture)).Append("ms");
            if (_isUnlimited)
            {
                // 上限がないときは、何を基準にした時間なのかを示す
                _builder.Append(" @").Append(Mathf.RoundToInt((float)_refreshHz)).Append("Hz");
            }
            _builder.Append(")  max ").Append(maxMs.ToString("F1", Culture)).Append("ms");
            line.Add(_builder.ToString(), SecondaryColor, 1);
        }

        /// 「名前 値」の形の行を、右上に 1 つ足す
        private void AddRightLine(string label, string value)
        {
            TextLine line = BeginRight();
            line.Add(label, SecondaryColor);
            line.Add(value, TextColor, 1);
        }

        /// バイト数を B / KB / MB で読みやすくして足す
        private static void AppendBytes(StringBuilder builder, long bytes)
        {
            if (bytes < BytesPerKilo)
            {
                builder.Append(bytes.ToString(Culture)).Append("B");
            }
            else if (bytes < BytesPerMega)
            {
                builder.Append((bytes / BytesPerKilo).ToString("F1", Culture)).Append("KB");
            }
            else
            {
                builder.Append((bytes / BytesPerMega).ToString("F1", Culture)).Append("MB");
            }
        }

        private void OnGUI()
        {
            if (!_isVisible)
            {
                return;
            }

            // 描くのは Repaint のときだけ。Layout などでは何もしない
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }

            EnsureStyle();
            if (_needsMeasure)
            {
                MeasureAll();
                _needsMeasure = false;
            }

            Color previousColor = GUI.color;

            // 左上
            float y = _margin;
            for (int i = 0; i < _leftCount; i++)
            {
                y = DrawLine(_leftLines[i], _margin, y);
            }

            // 右上
            y = _margin;
            for (int i = 0; i < _rightCount; i++)
            {
                TextLine line = _rightLines[i];
                float x = Screen.width - _margin - (line.TotalWidth + _padding * 2.0f);
                y = DrawLine(line, x, y);
            }

            // 左下
            float bottomY = Screen.height - _margin - _lineHeight;
            if (_isGraphVisible)
            {
                DrawLine(_graphCaptionLine, _margin, bottomY);
                float graphHeight = _appliedFontSize * GraphHeightRate;
                float graphBottom = bottomY - _lineSpacing;
                float graphTop = graphBottom - graphHeight;
                DrawLine(_graphHintLine, _margin, graphTop - _lineSpacing - _lineHeight);
                DrawGraph(_margin, graphTop, graphHeight);
            }
            else
            {
                DrawLine(_showGraphLine, _margin, bottomY);
            }

            GUI.color = previousColor;
        }

        /// 文字の大きさが変わった（画面の高さが変わった）ら、見た目と寸法を作り直す
        private void EnsureStyle()
        {
            int fontSize = Mathf.Max(MinFontSize, Mathf.RoundToInt(_baseFontSize * Screen.height / BaseScreenHeight));
            if (_style != null && fontSize == _appliedFontSize)
            {
                return;
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

            _needsMeasure = true;
        }

        /// 全部の行の幅を測る
        private void MeasureAll()
        {
            for (int i = 0; i < _leftCount; i++)
            {
                MeasureLine(_leftLines[i]);
            }
            for (int i = 0; i < _rightCount; i++)
            {
                MeasureLine(_rightLines[i]);
            }
            MeasureLine(_graphHintLine);
            MeasureLine(_graphCaptionLine);
            MeasureLine(_showGraphLine);
        }

        private void MeasureLine(TextLine line)
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

        /// 1 行を描き、次の行の y を返す
        private float DrawLine(TextLine line, float x, float y)
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

        /// フレームごとの棒グラフと、目標の点線を描く
        private void DrawGraph(float left, float top, float height)
        {
            if (_frameMsSamples == null)
            {
                return;
            }

            int length = _frameMsSamples.Length;
            float width = length * _barWidth;
            float maxMs = _budgetMs * GraphMaxBudgetMultiple;
            float bottom = top + height;

            GUI.color = BackgroundColor;
            GUI.DrawTexture(new Rect(left, top, width, height), _whiteTexture);

            for (int i = 0; i < length; i++)
            {
                // 古い順に左から並べる
                float ms = _frameMsSamples[(_frameHead + i) % length];
                if (ms <= 0.0f)
                {
                    continue;
                }

                float barHeight = Mathf.Min(ms / maxMs, 1.0f) * height;
                if (ms <= _budgetMs)
                {
                    GUI.color = BarGoodColor;
                }
                else if (ms <= _budgetMs * WarnBudgetMultiple)
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
