using System;
using UnityEngine;

namespace MiraiSozo.Options
{
    /// <summary>
    /// メニューの色・枠・開閉アニメーション・波紋・説明ウィンドウの設定。
    /// Resources/OptionMenuTheme を自動で読み込む（OptionMenuAnimationのThemeに指定すれば差し替えられる）。
    /// Play中にInspectorで変更すると、その場で見た目に反映される（角の丸みは次のPlayから）。
    /// </summary>
    [CreateAssetMenu(fileName = "OptionMenuTheme", menuName = "MiraiSozo/Option Menu Theme")]
    public sealed class OptionMenuTheme : ScriptableObject
    {
        private const string ResourcePath = "OptionMenuTheme";
        private static OptionMenuTheme _current;
        private static OptionMenuTheme _default;

        /// <summary>設定が変わったとき（Inspectorでの変更や差し替え）に通知する。</summary>
        public static event Action Changed;

        public static OptionMenuTheme Current => _current != null ? _current : (_current = GetDefault());

        /// <summary>
        /// 使用するテーマを切り替える。nullならResources/OptionMenuThemeに戻す。
        /// シーンごとに指定するため、テーマ未指定のシーンでは前のシーンのテーマを引き継がない。
        /// </summary>
        public static void Use(OptionMenuTheme theme)
        {
            var next = theme != null ? theme : GetDefault();
            if (next == _current) return;
            _current = next;
            Changed?.Invoke();
        }

        private static OptionMenuTheme GetDefault()
        {
            if (_default != null) return _default;
            _default = Resources.Load<OptionMenuTheme>(ResourcePath);
            // アセットが見つからない場合も、初期値で動くようにする。
            if (_default == null)
            {
                _default = CreateInstance<OptionMenuTheme>();
                _default.hideFlags = HideFlags.DontSave;
            }
            return _default;
        }

        private void OnValidate()
        {
            if (this == _current) Changed?.Invoke();
        }

        [field: Header("基本色")]
        [field: SerializeField, Tooltip("アクセント色。枠の光・表示灯・波紋・選択中のタブなど。")]
        public Color Accent { get; private set; } = new Color32(0, 191, 255, 255);
        [field: SerializeField, Tooltip("明るい基調色。波紋の芯など。")]
        public Color Light { get; private set; } = new Color32(224, 255, 255, 255);
        [field: SerializeField, Tooltip("パネル・説明ウィンドウ・キー設定の枠の背景色（半透明）。")]
        public Color Surface { get; private set; } = new Color32(224, 255, 255, 166);
        [field: SerializeField, Tooltip("文字の色。")]
        public Color Text { get; private set; } = new Color32(13, 23, 35, 255);

        [field: Header("枠")]
        [field: SerializeField, Min(0f), Tooltip("パネルの角の丸み。")]
        public float PanelRadius { get; private set; } = 22f;
        [field: SerializeField, Min(0f), Tooltip("ボタンの角の丸み。")]
        public float ButtonRadius { get; private set; } = 14f;
        [field: SerializeField, Min(0f), Tooltip("キー設定の枠の角の丸み。")]
        public float SlotRadius { get; private set; } = 10f;
        [field: SerializeField, Tooltip("外周の細い枠線の色。")]
        public Color OutlineColor { get; private set; } = new Color(0f, 0f, 0f, 0.85f);
        [field: SerializeField, Min(0f)]
        public float OutlineWidth { get; private set; } = 1.5f;
        [field: SerializeField, Tooltip("外側へ溶ける光の色（外へ向かって透明になる）。")]
        public Color HaloColor { get; private set; } = new Color(0f, 0.75f, 1f, 0.28f);
        [field: SerializeField, Min(0f)]
        public float HaloWidth { get; private set; } = 16f;
        [field: SerializeField, Tooltip("枠線の内側の縁取りの色。")]
        public Color RimColor { get; private set; } = new Color(1f, 1f, 1f, 0.75f);
        [field: SerializeField, Min(0f)]
        public float RimWidth { get; private set; } = 2f;
        [field: SerializeField, Tooltip("さらに内側の細い線の色。")]
        public Color InnerLineColor { get; private set; } = new Color(0f, 0.75f, 1f, 0.3f);
        [field: SerializeField, Min(0f), Tooltip("外周から内側の細い線までの距離。")]
        public float InnerLineOffset { get; private set; } = 7f;
        [field: SerializeField, Min(0f)]
        public float InnerLineWidth { get; private set; } = 1f;

        [field: Header("ボタン装飾（通常 / 選択中）")]
        [field: SerializeField, Tooltip("選択中にボタンの周りに出る光の色。")]
        public Color ButtonGlowColor { get; private set; } = new Color(0f, 0.75f, 1f, 0.4f);
        [field: SerializeField]
        public Color IndicatorColor { get; private set; } = new Color(0f, 0.75f, 1f, 0.45f);
        [field: SerializeField]
        public Color IndicatorActiveColor { get; private set; } = new Color(0f, 0.75f, 1f, 1f);
        [field: SerializeField]
        public Color UnderlineColor { get; private set; } = new Color(0f, 0.75f, 1f, 0.25f);
        [field: SerializeField]
        public Color UnderlineActiveColor { get; private set; } = new Color(0f, 0.75f, 1f, 0.8f);
        [field: SerializeField]
        public Color DotColor { get; private set; } = new Color(0f, 0.75f, 1f, 0.35f);
        [field: SerializeField]
        public Color DotActiveColor { get; private set; } = new Color(0f, 0.75f, 1f, 1f);
        [field: SerializeField, Min(0f), Tooltip("選択・マウスオンで点灯するまでの秒数。")]
        public float HighlightDuration { get; private set; } = 0.14f;

        [field: Header("開閉アニメーション")]
        [field: SerializeField, Min(0f), Tooltip("メニュー・説明ウィンドウ・確認画面が開くまでの秒数。画面の移動にも使う。")]
        public float OpenDuration { get; private set; } = 0.36f;
        [field: SerializeField, Min(0f), Tooltip("閉じるまでの秒数。")]
        public float CloseDuration { get; private set; } = 0.26f;
        [field: SerializeField, Range(0.05f, 0.9f), Tooltip("全体のうち、ラインが横へ伸びる段階の割合。")]
        public float WidthShare { get; private set; } = 0.4f;
        [field: SerializeField, Range(0f, 0.5f), Tooltip("全体のうち、ラインのまま止まる段階の割合。")]
        public float HoldShare { get; private set; } = 0.1f;
        [field: SerializeField, Range(0f, 1f), Tooltip("横へ伸び始めるときの幅（元の幅に対する割合）。")]
        public float StartWidth { get; private set; } = 0.02f;
        [field: SerializeField, Min(0f), Tooltip("ラインの段階でのパネルの厚み。")]
        public float LineThickness { get; private set; } = 6f;
        [field: SerializeField, Min(0f), Tooltip("上下に開ききる直前の行き過ぎ。0で行き過ぎなし。")]
        public float Overshoot { get; private set; } = 1.4f;
        [field: SerializeField, Tooltip("開閉の途中に中央を横切る光のラインの色。")]
        public Color DeployLineColor { get; private set; } = new Color32(0, 191, 255, 255);
        [field: SerializeField, Min(0f)]
        public float DeployLineThickness { get; private set; } = 3f;
        [field: SerializeField, Range(0.05f, 0.95f), Tooltip("画面の移動で、元の画面を畳みきるまでの割合。残りで次の画面が現れる。")]
        public float CollapseShare { get; private set; } = 0.4f;

        [field: Header("波紋（開くとき）")]
        [field: SerializeField, Min(0f), Tooltip("波紋が広がりきるまでの秒数。")]
        public float WaveDuration { get; private set; } = 0.45f;
        [field: SerializeField, Tooltip("波紋の後ろへ伸びる尾の色（波面に近いほどこの色になる）。")]
        public Color WaveTailColor { get; private set; } = new Color(0f, 0.75f, 1f, 0.38f);
        [field: SerializeField, Tooltip("波面の縁の色。")]
        public Color WaveCrestColor { get; private set; } = new Color(0f, 0.75f, 1f, 0.95f);
        [field: SerializeField, Tooltip("波面の芯の色。")]
        public Color WaveCoreColor { get; private set; } = new Color(0.88f, 1f, 1f, 0.9f);
        [field: SerializeField, Tooltip("最初の輪が通過した範囲に残す色。")]
        public Color ScannedColor { get; private set; } = new Color(0f, 0.75f, 1f, 0.07f);
        [field: SerializeField, Range(1, 6), Tooltip("時間差で発信する輪の数。")]
        public int RingCount { get; private set; } = 3;
        [field: SerializeField, Range(0f, 0.5f), Tooltip("輪どうしの時間差（演出全体に対する割合）。")]
        public float RingDelay { get; private set; } = 0.17f;
        [field: SerializeField, Range(0f, 1f), Tooltip("後の輪ほど淡くする倍率。")]
        public float RingFalloff { get; private set; } = 0.5f;
        [field: SerializeField, Min(0f)]
        public float TailLength { get; private set; } = 64f;
        [field: SerializeField, Min(0f)]
        public float CrestWidth { get; private set; } = 3f;
        [field: SerializeField, Min(0f), Tooltip("波面の前側のにじみの幅。")]
        public float FrontFeather { get; private set; } = 5f;
        [field: SerializeField, Min(0f), Tooltip("項目が現れる境目のぼかし幅。")]
        public float RevealSoftness { get; private set; } = 16f;
        [field: SerializeField, Min(0f), Tooltip("枠の外側の、波紋の発信点までの距離。")]
        public float OriginOffset { get; private set; } = 36f;

        [field: Header("背景の波紋")]
        [field: SerializeField]
        public bool AmbientEnabled { get; private set; } = true;
        [field: SerializeField, Min(0.1f), Tooltip("発信の間隔（秒）。")]
        public float AmbientInterval { get; private set; } = 3f;
        [field: SerializeField, Min(0.1f), Tooltip("1回の波紋が広がりきるまでの秒数。")]
        public float AmbientDuration { get; private set; } = 1.6f;
        [field: SerializeField, Range(0f, 1f), Tooltip("開くときの波紋に対する濃さ。")]
        public float AmbientIntensity { get; private set; } = 0.22f;

        [field: Header("効果音")]
        [field: SerializeField, Tooltip("オプションのタブを切り替えたときの音。")]
        public AudioClip TabSwitchSound { get; private set; }
        [field: SerializeField, Tooltip("メニュー・オプション画面・確認画面の開閉と、画面の切り替えの音。")]
        public AudioClip WindowSound { get; private set; }
        [field: SerializeField, Range(0f, 1f), Tooltip("効果音の音量。さらにオプションのSE音量が掛かる。")]
        public float SoundVolume { get; private set; } = 1f;
        [field: SerializeField, Range(0f, 1f), Tooltip("説明ウィンドウが現れたときの音の大きさ（ウィンドウの音に対する倍率）。")]
        public float DescriptionSoundVolume { get; private set; } = 0.35f;

        [field: Header("説明ウィンドウ")]
        [field: SerializeField, Min(0f)]
        public float DescriptionWidth { get; private set; } = 340f;
        [field: SerializeField, Min(0f), Tooltip("高さはパネルに揃えるが、これより低くはしない。")]
        public float DescriptionMinimumHeight { get; private set; } = 400f;
        [field: SerializeField, Min(0f), Tooltip("パネルとの間隔。")]
        public float DescriptionGap { get; private set; } = 28f;
        [field: SerializeField, Min(1)]
        public int DescriptionTitleSize { get; private set; } = 28;
        [field: SerializeField, Min(1)]
        public int DescriptionBodySize { get; private set; } = 19;
        [field: SerializeField]
        public Color DescriptionBodyColor { get; private set; } = new Color(13f / 255f, 23f / 255f, 35f / 255f, 0.85f);
        [field: SerializeField]
        public Color DescriptionDividerColor { get; private set; } = new Color(0f, 0.75f, 1f, 0.5f);
    }
}
