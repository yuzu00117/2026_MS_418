// 「一回だけ」のストック数と回復の進み具合を画面の隅に表示する。表示用のCanvasは自前で作る。
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Echo.Echolocation
{
    /// <summary>一回だけのスキャンのストックを画面右下などに表示する。シーン内のどのGameObjectにも付けられる。</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Echo/Echo Stock Display")]
    public sealed class EchoStockDisplay : MonoBehaviour
    {
        private const float BarHeight = 6;
        private const float BarSpacing = 4;
        private const int SortingOrder = 100;
        // 画面サイズが変わっても同じ見た目の大きさにするための基準。幅と高さを半々で合わせる。
        private static readonly Vector2 ReferenceResolution = new Vector2(1920, 1080);
        private const float WidthHeightBalance = .5f;

        [SerializeField, Tooltip("表示するController。未設定ならシーン内から探します。")]
        private EchoController _controller;
        [SerializeField, Tooltip("表示する画面の隅。")]
        private TextAnchor _corner = TextAnchor.LowerRight;
        [SerializeField, Tooltip("画面の端からの距離（ピクセル）。")]
        private Vector2 _margin = new Vector2(32, 32);
        [SerializeField, Tooltip("{0}が現在のストック、{1}が最大数。標準フォントは英数字のみ対応しています。")]
        private string _format = "ECHO {0} / {1}";
        [SerializeField, Min(1)]
        private float _fontSize = 36;
        [SerializeField]
        private Color _textColor = new Color(.6f, .95f, 1, 1);
        [SerializeField, Tooltip("ストックが0の時の文字色。")]
        private Color _emptyColor = new Color(1, .45f, .3f, 1);
        [SerializeField, Tooltip("次のストックが回復するまでのゲージを表示します。")]
        private bool _isRechargeBarVisible = true;
        [SerializeField]
        private Color _barColor = new Color(.05f, .8f, 1, .9f);
        [SerializeField]
        private Color _barBackgroundColor = new Color(0, 0, 0, .45f);
        [SerializeField, Tooltip("スキャン方式が「一回だけ」以外の時は表示しません。")]
        private bool _isHiddenOutsideOnceMode = true;
        [SerializeField, Tooltip("TextMeshProのフォント。未設定なら標準フォントを使います。")]
        private TMP_FontAsset _font;

        private Canvas _canvas;
        private TextMeshProUGUI _text;
        private RectTransform _barFill;
        private GameObject _barRoot;
        private int _shownStock = -1;
        private int _shownCapacity = -1;

        /// <summary>表示対象のController。実行中に差し替えられる。</summary>
        public EchoController Controller
        {
            get => _controller;
            set
            {
                Unsubscribe();
                _controller = value;
                Subscribe();
                _shownStock = -1;
            }
        }

        private void Awake()
        {
            if (!_controller) _controller = FindAnyObjectByType<EchoController>();
            Build();
        }

        private void OnEnable()
        {
            Subscribe();
            _shownStock = -1;
            if (_canvas) _canvas.enabled = true;
        }

        private void OnDisable()
        {
            Unsubscribe();
            if (_canvas) _canvas.enabled = false;
        }

        private void OnDestroy()
        {
            if (_canvas) Destroy(_canvas.gameObject);
        }

        private void Subscribe()
        {
            if (_controller) _controller.OnceStockChanged += HandleStockChanged;
        }

        private void Unsubscribe()
        {
            if (_controller) _controller.OnceStockChanged -= HandleStockChanged;
        }

        private void HandleStockChanged(int stock) => _shownStock = -1;

        private void LateUpdate()
        {
            bool isVisible = _controller && _controller.isActiveAndEnabled &&
                (!_isHiddenOutsideOnceMode || _controller.Settings.ScanMode == EchoScanMode.Once);
            _canvas.enabled = isVisible;
            if (!isVisible) return;

            int stock = _controller.OnceStock, capacity = _controller.Settings.OnceStockCapacity;
            // 文字列の生成は数が変わった時だけにして、毎フレームの割り当てを避ける。
            if (stock != _shownStock || capacity != _shownCapacity)
            {
                _shownStock = stock;
                _shownCapacity = capacity;
                _text.text = string.Format(_format, stock, capacity);
                _text.color = stock > 0 ? _textColor : _emptyColor;
            }

            bool isBarVisible = _isRechargeBarVisible && stock < capacity;
            _barRoot.SetActive(isBarVisible);
            if (isBarVisible)
                _barFill.anchorMax = new Vector2(_controller.OnceRechargeProgress, 1);
        }

        // Prefabやシーンに表示用の階層を保存しないよう、実行時に組み立てる。
        private void Build()
        {
            var canvasObject = new GameObject("Echo stock display", typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false);
            _canvas = canvasObject.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = SortingOrder;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.matchWidthOrHeight = WidthHeightBalance;

            var pivot = GetPivot(_corner);
            var panel = CreateRect("Panel", canvasObject.transform);
            panel.anchorMin = panel.anchorMax = panel.pivot = pivot;
            panel.anchoredPosition = new Vector2((pivot.x - .5f) * -2 * _margin.x, (pivot.y - .5f) * -2 * _margin.y);
            var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.childAlignment = _corner;
            layout.spacing = BarSpacing;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            panel.gameObject.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            panel.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            _text = CreateRect("Stock", panel).gameObject.AddComponent<TextMeshProUGUI>();
            if (_font) _text.font = _font;
            _text.fontSize = _fontSize;
            _text.alignment = GetTextAlignment(_corner);
            _text.raycastTarget = false;

            var bar = CreateRect("Recharge", panel);
            _barRoot = bar.gameObject;
            bar.gameObject.AddComponent<LayoutElement>().preferredHeight = BarHeight;
            var background = bar.gameObject.AddComponent<Image>();
            background.color = _barBackgroundColor;
            background.raycastTarget = false;
            _barFill = CreateRect("Fill", bar);
            _barFill.anchorMin = Vector2.zero;
            _barFill.anchorMax = new Vector2(0, 1);
            _barFill.offsetMin = _barFill.offsetMax = Vector2.zero;
            var fill = _barFill.gameObject.AddComponent<Image>();
            fill.color = _barColor;
            fill.raycastTarget = false;
        }

        private static RectTransform CreateRect(string name, Transform parent)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            return rect;
        }

        private static Vector2 GetPivot(TextAnchor corner)
        {
            int index = (int)corner;
            return new Vector2(index % 3 * .5f, 1 - index / 3 * .5f);
        }

        private static TextAlignmentOptions GetTextAlignment(TextAnchor corner)
        {
            int column = (int)corner % 3;
            return column == 0 ? TextAlignmentOptions.Left : column == 1 ? TextAlignmentOptions.Center : TextAlignmentOptions.Right;
        }
    }
}
