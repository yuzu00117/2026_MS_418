using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MiraiSozo.Options
{
    /// <summary>マウスを乗せたボタンの説明を、パネルの右側にメニューと同じ見た目の縦長ウィンドウで表示する。</summary>
    public sealed class OptionDescriptionWindow : MonoBehaviour
    {
        private const string WindowName = "Description Window";
        // 幅・高さ・文字の大きさ・色はOptionMenuThemeで調整する。開閉はメニューと同じ機械的な展開を使う。
        // 画面端との最低限の余白。外側の淡い光が切れない幅にする。
        private const float ScreenMargin = 24f;
        private const float Padding = 26f;
        private const float TitleHeight = 40f;
        private const int MinimumTitleFontSize = 18;
        private const float BodyLineSpacing = 1.25f;
        private const float DividerOffset = 12f;
        private const float DividerThickness = 2f;
        // 文字は板が上下へ開ききる直前から表示する。
        private const float ContentStart = 0.8f;
        // 表示中に別のボタンへ移ったとき、文字を入れ替えるフェードの時間。
        private const float SwapDuration = 0.12f;
        // 隣のボタンへ移る間に一瞬閉じかけないよう、離れてから閉じ始めるまでの猶予。
        private const float HideDelay = 0.08f;
        // 時間に0秒が設定されても0で割らないための下限。
        private const float MinimumDuration = 0.0001f;
        // 変形の動きの強さ。出だしから素早く動き、到着の手前で減速する。
        private const float MorphEasePower = 3f;
        private const string BuiltinFontName = "LegacyRuntime.ttf";
        // 変形開始時に非表示だった場合、板が現れきるまでの割合。
        private const float MorphFadeInShare = 0.3f;
        private readonly Dictionary<OptionSoftButton, string> _descriptions = new Dictionary<OptionSoftButton, string>();
        private readonly Vector3[] _corners = new Vector3[4];
        private RectTransform _rect;
        private RectTransform _panel;
        private RectTransform _backdrop;
        private CanvasGroup _backdropGroup;
        private Image _background;
        private Image _divider;
        private Image _deployLine;
        private CanvasGroup _contentGroup;
        private Text _title;
        private Text _body;
        private OptionSoftButton _current;
        private Vector2 _size;
        private float _progress;
        private float _swap = 1f;
        // 最後に見た目へ反映した値。開ききって止まっている間は、同じ値を毎フレーム設定し直さない。
        private float _appliedProgress = -1f;
        private float _appliedSwap = -1f;
        private float _hideTimer;
        // オプション画面への変形中は、マウスの出入りや通常の開閉で表示を変えない。
        private bool _isMorphing;
        // メニューの演出中など、開くのを待つ条件。
        private Func<bool> _isBlocked;
        // 閉じた状態から開き始めたときに呼ぶ（効果音など）。
        private Action _onOpening;
        private bool _morphFadesIn;
        private Vector3 _morphStartPosition;
        private Vector3 _morphEndPosition;
        private Vector2 _morphStartSize;
        private Vector2 _morphEndSize;

        /// <summary>isBlockedがtrueの間は、項目を選んでも開くのを待つ（メニューの開閉演出の最中など）。</summary>
        public static OptionDescriptionWindow Create(RectTransform panel, Func<bool> isBlocked, Action onOpening)
        {
            var existing = panel.parent.Find(WindowName);
            if (existing != null && existing.TryGetComponent(out OptionDescriptionWindow found))
            {
                found._isBlocked = isBlocked;
                found._onOpening = onOpening;
                return found;
            }
            var root = new GameObject(WindowName, typeof(RectTransform), typeof(CanvasGroup), typeof(LayoutElement));
            root.layer = panel.gameObject.layer;
            root.GetComponent<LayoutElement>().ignoreLayout = true;
            var window = root.AddComponent<OptionDescriptionWindow>();
            window._isBlocked = isBlocked;
            window._onOpening = onOpening;
            window.Build(panel);
            root.SetActive(false);
            return window;
        }

        private void Build(RectTransform panel)
        {
            _panel = panel;
            _rect = (RectTransform)transform;
            _rect.SetParent(panel.parent, false);
            _rect.anchorMin = _rect.anchorMax = _rect.pivot = new Vector2(0.5f, 0.5f);
            var group = GetComponent<CanvasGroup>();
            // 説明を表示してもボタンのマウスオンが途切れないよう、入力は受けない。
            group.blocksRaycasts = false;
            group.interactable = false;
            BuildBackdrop();
            BuildDeployLine();
            BuildContent();
        }

        // 中心から広がる板。拡大ではなくサイズを変えるので、角丸や枠線は潰れない。
        private void BuildBackdrop()
        {
            var root = new GameObject("Backdrop", typeof(RectTransform), typeof(CanvasGroup), typeof(Image));
            root.layer = gameObject.layer;
            _backdrop = (RectTransform)root.transform;
            _backdrop.SetParent(_rect, false);
            _backdrop.anchorMin = _backdrop.anchorMax = _backdrop.pivot = new Vector2(0.5f, 0.5f);
            _backdropGroup = root.GetComponent<CanvasGroup>();
            _background = root.GetComponent<Image>();
            _background.raycastTarget = false;
            OptionSoftStyle.ApplyPanel(_backdrop, false);
        }

        // 板が細い帯の間だけ中央に光るライン。
        private void BuildDeployLine()
        {
            var root = new GameObject("Deploy Line", typeof(RectTransform));
            root.layer = gameObject.layer;
            _deployLine = root.AddComponent<Image>();
            _deployLine.raycastTarget = false;
            var rect = _deployLine.rectTransform;
            rect.SetParent(_rect, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        }

        private void BuildContent()
        {
            var root = new GameObject("Content", typeof(RectTransform), typeof(CanvasGroup));
            root.layer = gameObject.layer;
            var content = (RectTransform)root.transform;
            content.SetParent(_rect, false);
            content.anchorMin = Vector2.zero;
            content.anchorMax = Vector2.one;
            content.offsetMin = Vector2.one * Padding;
            content.offsetMax = -Vector2.one * Padding;
            _contentGroup = root.GetComponent<CanvasGroup>();
            _title = CreateText(content, "Title", FontStyle.Bold);
            _title.resizeTextForBestFit = true;
            _title.resizeTextMinSize = MinimumTitleFontSize;
            SetArea(_title.rectTransform, 0f, TitleHeight);
            _divider = new GameObject("Divider", typeof(RectTransform)).AddComponent<Image>();
            _divider.rectTransform.SetParent(content, false);
            _divider.raycastTarget = false;
            SetArea(_divider.rectTransform, TitleHeight + DividerOffset, TitleHeight + DividerOffset + DividerThickness);
            _body = CreateText(content, "Body", FontStyle.Normal);
            _body.lineSpacing = BodyLineSpacing;
            SetArea(_body.rectTransform, TitleHeight + DividerOffset * 2f + DividerThickness, -1f);
        }

        private static Text CreateText(Transform parent, string name, FontStyle style)
        {
            var text = new GameObject(name, typeof(RectTransform)).AddComponent<Text>();
            text.rectTransform.SetParent(parent, false);
            // フォントは後からメニューの日本語フォントに置き換わる。
            text.font = Resources.GetBuiltinResource<Font>(BuiltinFontName);
            text.fontStyle = style;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        // 余白の内側の上端からの距離で範囲を指定する。bottomが負なら下端まで伸ばす。
        private static void SetArea(RectTransform rect, float top, float bottom)
        {
            bool stretch = bottom < 0f;
            rect.anchorMin = new Vector2(0f, stretch ? 0f : 1f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.offsetMin = new Vector2(0f, stretch ? 0f : -bottom);
            rect.offsetMax = new Vector2(0f, -top);
        }

        private void OnEnable()
        {
            OptionMenuTheme.Changed += ApplyTheme;
            ApplyTheme();
        }

        private void OnDisable() => OptionMenuTheme.Changed -= ApplyTheme;

        // 色と文字の大きさをOptionMenuThemeから反映する。
        private void ApplyTheme()
        {
            if (_background == null) return;
            var theme = OptionMenuTheme.Current;
            _background.color = theme.Surface;
            _divider.color = theme.DescriptionDividerColor;
            _title.color = theme.Text;
            _title.resizeTextMaxSize = theme.DescriptionTitleSize;
            _title.fontSize = theme.DescriptionTitleSize;
            _body.color = theme.DescriptionBodyColor;
            _body.fontSize = theme.DescriptionBodySize;
            _appliedProgress = -1f;
        }

        /// <summary>ボタンに説明文を結び付ける。キー操作で選んだときとマウスを乗せたときに表示し、見出しにはボタンの表示名を使う。</summary>
        public void Bind(Button button, string description)
        {
            if (button == null || !button.TryGetComponent(out OptionSoftButton soft)) return;
            if (!_descriptions.ContainsKey(soft)) soft.FocusChanged += OnFocusChanged;
            _descriptions[soft] = description;
        }

        private void OnFocusChanged(OptionSoftButton button, bool isFocused, bool immediate)
        {
            if (_isMorphing) return;
            if (isFocused) { Show(button); return; }
            if (button != _current) return;
            _current = null;
            if (immediate) HideImmediate();
            else _hideTimer = HideDelay;
        }

        private void Show(OptionSoftButton button)
        {
            bool isVisible = gameObject.activeSelf && _progress > 0f;
            if (isVisible && button != _current) _swap = 0f;
            _current = button;
            _hideTimer = 0f;
            var label = button.GetComponentInChildren<Text>(true);
            _title.text = label != null ? label.text : button.name;
            if (label != null) _title.font = _body.font = label.font;
            _body.text = _descriptions[button];
            gameObject.SetActive(true);
            _rect.SetAsLastSibling();
            // 次のUpdateより先に描画されても、前回の大きさのまま一瞬見えないようにする。
            ApplyProgress();
        }

        /// <summary>マウスが離れたときと同じく、中心へ縮んで閉じる。</summary>
        public void Hide()
        {
            _current = null;
            _hideTimer = 0f;
        }

        public void HideImmediate()
        {
            _current = null;
            _hideTimer = 0f;
            _progress = 0f;
            gameObject.SetActive(false);
        }

        private void Update()
        {
            if (_isMorphing) return;
            float step = Time.unscaledDeltaTime;
            if (_hideTimer > 0f) _hideTimer -= step;
            bool wantsOpen = _current != null || _hideTimer > 0f;
            bool isOpen = wantsOpen && (_isBlocked == null || !_isBlocked());
            // 閉じた状態から開き始めるときに、パネルの現在の位置で置き場所を決める。
            if (isOpen && _progress <= 0f)
            {
                PlaceWindow();
                _onOpening?.Invoke();
            }
            float duration = Mathf.Max(MinimumDuration, isOpen ? OptionPanelExpansion.OpenDuration : OptionPanelExpansion.CloseDuration);
            _progress = Mathf.MoveTowards(_progress, isOpen ? 1f : 0f, step / duration);
            _swap = Mathf.MoveTowards(_swap, 1f, step / SwapDuration);
            if (_progress != _appliedProgress || _swap != _appliedSwap) ApplyProgress();
            if (_progress <= 0f && !wantsOpen) gameObject.SetActive(false);
        }

        // 板を中心から機械的に展開し（拡大ではなくサイズを変えるので角丸や枠線は潰れない）、文字は開ききる直前から表示する。
        private void ApplyProgress()
        {
            _appliedProgress = _progress;
            _appliedSwap = _swap;
            var scale = OptionPanelExpansion.GetScale(_progress, _size.y, out float opacity, out float line);
            _backdrop.sizeDelta = Vector2.Scale(_size, scale);
            _backdropGroup.alpha = opacity;
            SetDeployLine(line);
            float content = Mathf.InverseLerp(ContentStart, 1f, _progress);
            _contentGroup.alpha = Mathf.SmoothStep(0f, 1f, content) * Mathf.SmoothStep(0f, 1f, _swap);
        }

        private void SetDeployLine(float opacity)
        {
            var theme = OptionMenuTheme.Current;
            _deployLine.rectTransform.sizeDelta = new Vector2(_backdrop.sizeDelta.x, theme.DeployLineThickness);
            var color = theme.DeployLineColor;
            color.a *= opacity;
            _deployLine.color = color;
            _deployLine.enabled = opacity > 0f;
        }

        /// <summary>文字を消し、現在の位置と大きさから対象パネルの位置と大きさへ変形を始める。</summary>
        public void BeginMorph(RectTransform target)
        {
            bool isVisible = gameObject.activeSelf && _progress > 0f;
            if (!isVisible) PlaceWindow();
            _isMorphing = true;
            _current = null;
            _hideTimer = 0f;
            _morphFadesIn = !isVisible;
            _morphStartPosition = _rect.localPosition;
            // 非表示だった場合は、普段の表示位置の中心から現れる。
            _morphStartSize = isVisible ? _backdrop.sizeDelta : Vector2.zero;
            GetInactiveBounds(target, out var min, out var max);
            _morphEndPosition = new Vector3((min.x + max.x) * 0.5f, (min.y + max.y) * 0.5f, _morphStartPosition.z);
            _morphEndSize = max - min;
            gameObject.SetActive(true);
            _rect.SetAsLastSibling();
            SetMorph(0f);
        }

        /// <summary>0で変形開始時、1で対象パネルと同じ位置と大きさ。</summary>
        public void SetMorph(float progress)
        {
            // 出だしから素早く動き、到着の手前で減速させてもたつきを感じさせない。
            float eased = 1f - Mathf.Pow(1f - progress, MorphEasePower);
            _rect.localPosition = Vector3.Lerp(_morphStartPosition, _morphEndPosition, eased);
            _backdrop.sizeDelta = Vector2.Lerp(_morphStartSize, _morphEndSize, eased);
            _backdropGroup.alpha = _morphFadesIn ? Mathf.SmoothStep(0f, 1f, progress / MorphFadeInShare) : 1f;
            _contentGroup.alpha = 0f;
            SetDeployLine(0f);
        }

        /// <summary>変形を終え、入れ替わった対象パネルに表示を任せて閉じる。</summary>
        public void EndMorph()
        {
            if (!_isMorphing) return;
            _isMorphing = false;
            HideImmediate();
        }

        // 非表示のパネルも位置を測れるよう、同じフレーム内で一時的に表示して戻す。
        private void GetInactiveBounds(RectTransform target, out Vector2 min, out Vector2 max)
        {
            bool wasActive = target.gameObject.activeSelf;
            if (!wasActive) target.gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            GetBounds(target, out min, out max);
            if (!wasActive) target.gameObject.SetActive(false);
        }

        // パネルの右側に、パネルと同じ高さ（最低DescriptionMinimumHeight）で置き、画面からはみ出す場合は内側へ寄せる。
        private void PlaceWindow()
        {
            GetBounds(_panel, out var panelMin, out var panelMax);
            var canvas = GetComponentInParent<Canvas>(true);
            var screen = canvas != null ? (RectTransform)canvas.rootCanvas.transform : (RectTransform)_rect.parent;
            GetBounds(screen, out var screenMin, out var screenMax);
            var available = screenMax - screenMin - Vector2.one * (ScreenMargin * 2f);
            var theme = OptionMenuTheme.Current;
            float width = Mathf.Min(theme.DescriptionWidth, available.x);
            float height = Mathf.Min(Mathf.Max(panelMax.y - panelMin.y, theme.DescriptionMinimumHeight), available.y);
            float x = Mathf.Min(panelMax.x + theme.DescriptionGap + width * 0.5f, screenMax.x - ScreenMargin - width * 0.5f);
            float y = Mathf.Clamp((panelMin.y + panelMax.y) * 0.5f, screenMin.y + ScreenMargin + height * 0.5f, screenMax.y - ScreenMargin - height * 0.5f);
            _size = new Vector2(width, height);
            _rect.sizeDelta = _size;
            // 大きさが変わったので、次のUpdateで必ず反映し直す。
            _appliedProgress = -1f;
            _rect.localPosition = new Vector3(x, y, 0f);
        }

        // 親の座標系での矩形の範囲。
        private void GetBounds(RectTransform target, out Vector2 min, out Vector2 max)
        {
            target.GetWorldCorners(_corners);
            var parent = _rect.parent;
            min = parent.InverseTransformPoint(_corners[0]);
            max = parent.InverseTransformPoint(_corners[2]);
        }
    }
}
