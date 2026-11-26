using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MiraiSozo.Options
{
    /// <summary>Canvas側の演出とメニューの開閉を接続する。</summary>
    [RequireComponent(typeof(CanvasGroup))]
    public sealed class OptionMenuAnimation : MonoBehaviour
    {
        private const string RevealShaderPath = "OptionMenuReveal";
        private const float MaxFrameStep = 1f / 30f;
        // 畳むパネルの大きさがこの割合を下回ると薄くしていく。
        private const float CollapseFadeShare = 0.4f;
        private static readonly int RevealOriginId = Shader.PropertyToID("_OptionRevealOrigin");
        private static readonly int RevealRadiusId = Shader.PropertyToID("_OptionRevealRadius");
        private static readonly int RevealSoftnessId = Shader.PropertyToID("_OptionRevealSoftness");
        [Tooltip("色・時間・波紋などの設定。未指定ならResources/OptionMenuThemeを使います。")]
        [SerializeField] private OptionMenuTheme _theme;
        [Tooltip("有効にすると、波紋を枠の内側だけに描画します。")]
        [SerializeField] private bool _clipToPanel = true;
        [Tooltip("閉じる演出の最後からCompleteClosingを呼ぶ場合に有効にします。")]
        [SerializeField] private bool _hasClosingAnimation;
        [SerializeField] private UnityEvent _onOpening = new UnityEvent();
        [SerializeField] private UnityEvent _onClosing = new UnityEvent();
        [Tooltip("オプション画面を開いたときに呼ばれます。演出の追加先です。")]
        [SerializeField] private UnityEvent _onOptionsOpening = new UnityEvent();
        // 時間や波紋の設定はOptionMenuThemeから読む。
        private static OptionMenuTheme Theme => OptionMenuTheme.Current;
        private static float ExpandDuration => Theme.OpenDuration;
        private static float CloseDuration => Theme.CloseDuration;
        private static float WaveDuration => Theme.WaveDuration;
        // 枠の少し外側を波紋の発生点にする。
        private static float OriginOffset => Theme.OriginOffset;
        // 後ろを追う尾まで枠を抜けきってから消えるよう、終点の半径に足す余白。
        private static float WaveTail => Theme.TailLength;
        // 表示範囲の境目をぼかす幅。
        private static float RevealSoftness => Theme.RevealSoftness;
        // 画面を移るとき、元の画面を畳みきるまでの割合。次の画面はその後に現れ、2つが重ならないようにする。
        private static float CollapseShare => Theme.CollapseShare;
        private Action _onClosed;
        private Coroutine _opening;
        private Coroutine _transition;
        private Coroutine _closing;
        private OptionDescriptionWindow _morphWindow;
        // 画面を入れ替える処理と、入れ替え後に表示するパネル。入れ替え前の間だけ設定されている。
        private Action _swap;
        private RectTransform _swapTarget;
        private OptionDeployLine _deployLine;
        private RectTransform _sonarRoot;
        private RectMask2D _sonarMask;
        private OptionMenuSonar _sonar;
        private Material _revealMaterial;
        private Transform _canvasRoot;
        private CanvasGroup _panelGroup;
        private bool _panelBlocksRaycasts;
        private float _panelAlpha;
        private RectTransform _panel;
        private Vector3 _panelScale;
        private Vector3 _panelPosition;
        private Vector2 _waveOrigin;
        private float _waveStart;
        private float _waveEnd;
        private readonly List<OptionMenuRevealItem> _items = new List<OptionMenuRevealItem>();
        private readonly List<Graphic> _graphics = new List<Graphic>();
        private readonly Vector3[] _corners = new Vector3[4];

        public bool IsClosing => _onClosed != null;

        private void Awake()
        {
            if (_theme != null) OptionMenuTheme.Current = _theme;
        }
        /// <summary>ポーズ画面とオプション画面の間を移る演出の再生中か。</summary>
        public bool IsTransitioning => _transition != null;
        /// <summary>開閉や画面の移動など、いずれかの演出を再生中か。</summary>
        public bool IsBusy => _opening != null || _transition != null || _closing != null;
        /// <summary>移る演出のうち、まだ画面を入れ替えていない段階か。この間は次の操作を受け付けない。</summary>
        public bool IsSwapPending => _swap != null;

        public void PlayOpening(RectTransform panel)
        {
            StopOpening();
            CancelClosing();
            _onOpening.Invoke();
            bool hasAnimation = ExpandDuration > 0f || WaveDuration > 0f;
            if (!hasAnimation || !isActiveAndEnabled || !panel.gameObject.activeInHierarchy) return;
            _panel = panel;
            _panelScale = panel.localScale;
            _panelPosition = panel.localPosition;
            HidePanelContents(panel);
            // 最初の描画で全体が一瞬見えないよう、開始前に畳んだ状態を反映する。
            SetPanelExpansion(0f);
            _opening = StartCoroutine(PlaySequence());
        }

        private IEnumerator PlaySequence()
        {
            for (float elapsed = 0f; elapsed < ExpandDuration;)
            {
                yield return null;
                elapsed += GetFrameStep();
                SetPanelExpansion(Mathf.Clamp01(elapsed / ExpandDuration));
            }
            RestorePanelTransform();
            BeginWave(fromLeft: false);
            for (float elapsed = 0f; elapsed < WaveDuration;)
            {
                yield return null;
                elapsed += GetFrameStep();
                UpdateWave(Mathf.Clamp01(elapsed / WaveDuration));
            }
            _opening = null;
            FinishOpening();
        }

        private void UpdateWave(float progress) => SetWave(_waveOrigin, progress);

        // パネルの中身（枠以外）を非表示にする。表示は波紋で戻す。
        private void HidePanelContents(RectTransform panel)
        {
            var canvas = panel.GetComponentInParent<Canvas>();
            _canvasRoot = canvas != null ? canvas.rootCanvas.transform : null;
            HideContents(panel);
            Shader.SetGlobalFloat(RevealRadiusId, -RevealSoftness);
            Shader.SetGlobalFloat(RevealSoftnessId, RevealSoftness);
        }

        // 一時停止直後などの重いフレームで演出が飛ばないよう、進める時間に上限を設ける。
        private static float GetFrameStep() => Mathf.Min(Time.unscaledDeltaTime, MaxFrameStep);

        // 枠の中心を固定したまま、中央の点から展開する（progressを戻せば中央へ畳む）。
        private void SetPanelExpansion(float progress)
        {
            float line = OptionPanelExpansion.Apply(_panel, _panelScale, _panelPosition, _panelGroup, _panelAlpha, progress);
            UpdateDeployLine(line);
        }

        // パネルの中央を横切る光のライン。パネルが細い帯の間だけ見え、上下に開くと消える。
        private void UpdateDeployLine(float opacity)
        {
            if (_deployLine == null) _deployLine = new OptionDeployLine(gameObject.layer);
            _deployLine.Show(_panel, opacity);
        }

        private void HideDeployLine() => _deployLine?.Hide();

        private void RestorePanelTransform()
        {
            if (_panel == null) return;
            _panel.localScale = _panelScale;
            _panel.localPosition = _panelPosition;
            if (_panelGroup != null) _panelGroup.alpha = _panelAlpha;
            HideDeployLine();
        }

        // 広がりきった枠の位置で、波紋の発生点と終点を決めて描画を始める。
        // ポーズ画面は上端中央の外側から下へ、オプション画面は左端中央の外側から右へ広げる。
        private void BeginWave(bool fromLeft)
        {
            PrepareSonar(_panel);
            Canvas.ForceUpdateCanvases();
            var rect = _panel.rect;
            var start = fromLeft ? new Vector2(rect.xMin - OriginOffset, rect.center.y) : new Vector2(rect.center.x, rect.yMax + OriginOffset);
            _waveOrigin = ConvertToSonarSpace(_panel.TransformPoint(start));
            GetBounds(_panel, out var min, out var max);
            _waveStart = GetNearestDistance(_waveOrigin, min, max);
            _waveEnd = GetFarthestDistance(_waveOrigin, min, max) + WaveTail;
            SetWave(_waveOrigin, 0f);
        }

        private void PrepareSonar(RectTransform panel)
        {
            if (_sonarRoot == null) CreateSonar();
            _sonarRoot.SetParent(panel.parent, false);
            // 枠と項目の上に重ねるため、パネルの直後へ置く。
            _sonarRoot.SetAsLastSibling();
            _sonarRoot.SetSiblingIndex(panel.GetSiblingIndex() + 1);
            if (_clipToPanel) CopyRect(panel, _sonarRoot);
            else Stretch(_sonarRoot);
            _sonarMask.enabled = _clipToPanel;
            _sonarRoot.gameObject.SetActive(true);
        }

        private void CreateSonar()
        {
            var root = new GameObject("Opening Sonar Mask", typeof(RectTransform), typeof(RectMask2D), typeof(LayoutElement));
            root.layer = gameObject.layer;
            _sonarRoot = (RectTransform)root.transform;
            _sonarMask = root.GetComponent<RectMask2D>();
            // 親にLayoutGroupがあっても配置を崩さない。
            root.GetComponent<LayoutElement>().ignoreLayout = true;
            var wave = new GameObject("Opening Sonar", typeof(RectTransform));
            wave.layer = gameObject.layer;
            var rect = (RectTransform)wave.transform;
            rect.SetParent(_sonarRoot, false);
            Stretch(rect);
            _sonar = wave.AddComponent<OptionMenuSonar>();
            _sonar.raycastTarget = false;
        }

        private static void CopyRect(RectTransform source, RectTransform target)
        {
            target.anchorMin = source.anchorMin;
            target.anchorMax = source.anchorMax;
            target.pivot = source.pivot;
            target.anchoredPosition3D = source.anchoredPosition3D;
            target.sizeDelta = source.sizeDelta;
            target.localRotation = source.localRotation;
            target.localScale = source.localScale;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private Vector2 ConvertToSonarSpace(Vector3 world) => _sonar.rectTransform.InverseTransformPoint(world);

        private void GetBounds(RectTransform rect, out Vector2 min, out Vector2 max)
        {
            rect.GetWorldCorners(_corners);
            min = Vector2.positiveInfinity;
            max = Vector2.negativeInfinity;
            foreach (var corner in _corners)
            {
                var point = ConvertToSonarSpace(corner);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }
        }

        private static float GetNearestDistance(Vector2 origin, Vector2 min, Vector2 max)
        {
            var nearest = new Vector2(Mathf.Clamp(origin.x, min.x, max.x), Mathf.Clamp(origin.y, min.y, max.y));
            return Vector2.Distance(origin, nearest);
        }

        private static float GetFarthestDistance(Vector2 origin, Vector2 min, Vector2 max)
        {
            float x = Mathf.Max(Mathf.Abs(origin.x - min.x), Mathf.Abs(origin.x - max.x));
            float y = Mathf.Max(Mathf.Abs(origin.y - min.y), Mathf.Abs(origin.y - max.y));
            return new Vector2(x, y).magnitude;
        }

        // 枠自身と枠装飾は開いた瞬間から見せ、中の項目だけを波紋の内側に限定する。
        private void HideContents(RectTransform panel)
        {
            _panelGroup = panel.GetComponent<CanvasGroup>();
            if (_panelGroup != null)
            {
                // 見えていない項目を押せないよう、演出中はマウス操作を止める。
                _panelBlocksRaycasts = _panelGroup.blocksRaycasts;
                _panelAlpha = _panelGroup.alpha;
                _panelGroup.blocksRaycasts = false;
            }
            var material = GetRevealMaterial();
            if (material == null || _canvasRoot == null) return;
            panel.GetComponentsInChildren(false, _graphics);
            foreach (var graphic in _graphics)
            {
                if (graphic.transform == panel || graphic is OptionSoftFrame) continue;
                if (OptionMenuRevealItem.CanReveal(graphic)) _items.Add(new OptionMenuRevealItem(graphic, material));
            }
            _graphics.Clear();
        }

        private Material GetRevealMaterial()
        {
            if (_revealMaterial != null) return _revealMaterial;
            var shader = Resources.Load<Shader>(RevealShaderPath);
            // シェーダーが使えない環境では、項目を最初から表示して波紋だけを再生する。
            if (shader == null || !shader.isSupported) return null;
            _revealMaterial = new Material(shader) { name = "Option Menu Reveal", hideFlags = HideFlags.DontSave };
            return _revealMaterial;
        }

        // 波紋の描画と項目の表示範囲に同じ半径を渡し、最初の輪が通過した場所から見えるようにする。
        private void SetWave(Vector2 origin, float progress)
        {
            _sonar.SetPulse(origin, _waveStart, _waveEnd, progress);
            if (_canvasRoot == null) return;
            float radius = OptionMenuSonar.GetLeadRadius(_waveStart, _waveEnd, progress);
            Vector3 center = _canvasRoot.InverseTransformPoint(_sonar.rectTransform.TransformPoint(origin));
            Vector3 edge = _canvasRoot.InverseTransformPoint(_sonar.rectTransform.TransformPoint(origin + Vector2.right * radius));
            Shader.SetGlobalVector(RevealOriginId, center);
            Shader.SetGlobalFloat(RevealRadiusId, Vector2.Distance(center, edge));
            Shader.SetGlobalFloat(RevealSoftnessId, RevealSoftness);
        }

        public void StopOpening()
        {
            if (_opening != null) StopCoroutine(_opening);
            _opening = null;
            FinishOpening();
        }

        // 途中で止めた場合も、全項目と操作を元の状態へ戻す。
        private void FinishOpening()
        {
            foreach (var item in _items) item.Restore();
            _items.Clear();
            RestorePanelTransform();
            _panel = null;
            if (_panelGroup != null) _panelGroup.blocksRaycasts = _panelBlocksRaycasts;
            _panelGroup = null;
            if (_sonarRoot != null) _sonarRoot.gameObject.SetActive(false);
        }

        /// <summary>
        /// ポーズ画面からオプション画面へ移る演出。文字を消してメイン画面を左下へ畳み、
        /// 説明ウィンドウをオプション画面の大きさへ変形させてから入れ替え、左端からの波紋で項目を表示する。
        /// showOptionsは入れ替えの瞬間に呼ばれ、ポーズ画面を閉じてオプション画面を表示する処理を担う。
        /// </summary>
        public void PlayOptionsOpening(RectTransform pausePanel, RectTransform optionsPanel, OptionDescriptionWindow window, Action showOptions)
        {
            _onOptionsOpening.Invoke();
            if (!BeginTransition(pausePanel, optionsPanel, showOptions)) return;
            _morphWindow = window;
            if (_morphWindow != null) _morphWindow.BeginMorph(optionsPanel);
            _transition = StartCoroutine(PlayOptionsOpeningSequence());
        }

        /// <summary>
        /// オプション画面からポーズ画面へ戻る演出。文字を消してオプション画面を右上へ畳み、
        /// メイン画面を左下から広げてから、上端からの波紋で項目を表示する。showPauseは入れ替えの瞬間に呼ばれる。
        /// </summary>
        public void PlayOptionsClosing(RectTransform optionsPanel, RectTransform pausePanel, Action showPause)
        {
            if (!BeginTransition(optionsPanel, pausePanel, showPause)) return;
            _transition = StartCoroutine(PlayOptionsClosingSequence());
        }

        // 移る演出を始め、元のパネルの文字を消す。始められない場合はすぐに入れ替えてfalseを返す。
        private bool BeginTransition(RectTransform from, RectTransform to, Action swap)
        {
            StopOpening();
            StopTransition();
            if (!isActiveAndEnabled || !from.gameObject.activeInHierarchy)
            {
                swap?.Invoke();
                return false;
            }
            _swap = swap;
            _swapTarget = to;
            CapturePanel(from);
            HidePanelContents(from);
            return true;
        }

        private void CapturePanel(RectTransform panel)
        {
            _panel = panel;
            _panelScale = panel.localScale;
            _panelPosition = panel.localPosition;
        }

        // メイン画面を畳みきってから説明ウィンドウを変形させ、入れ替えて左端から波紋を出す。
        private IEnumerator PlayOptionsOpeningSequence()
        {
            for (float elapsed = 0f; elapsed < ExpandDuration;)
            {
                yield return null;
                elapsed += GetFrameStep();
                float progress = Mathf.Clamp01(elapsed / ExpandDuration);
                float collapse = Mathf.Clamp01(progress / CollapseShare);
                SetPanelCorner(1f - collapse * collapse, toUpperRight: false);
                if (_morphWindow != null) _morphWindow.SetMorph(Mathf.Clamp01((progress - CollapseShare) / (1f - CollapseShare)));
            }
            SwapPanels();
            if (_panel != null) BeginWave(fromLeft: true);
            for (float elapsed = 0f; _panel != null && elapsed < WaveDuration;)
            {
                yield return null;
                elapsed += GetFrameStep();
                UpdateWave(Mathf.Clamp01(elapsed / WaveDuration));
            }
            _transition = null;
            FinishOpening();
        }

        // オプション画面を右上へ畳みきってから入れ替え、メイン画面を左下から広げて上端から波紋を出す。
        // 2つの画面は同じ付近に表示されるため、同時には動かさず重ならないようにする。
        private IEnumerator PlayOptionsClosingSequence()
        {
            for (float elapsed = 0f; elapsed < ExpandDuration;)
            {
                yield return null;
                elapsed += GetFrameStep();
                float progress = Mathf.Clamp01(elapsed / ExpandDuration);
                if (progress < CollapseShare)
                {
                    float collapse = progress / CollapseShare;
                    SetPanelCorner(1f - collapse * collapse, toUpperRight: true);
                    continue;
                }
                if (_swap != null) SwapPanels();
                if (_panel == null) continue;
                float grow = (progress - CollapseShare) / (1f - CollapseShare);
                SetPanelCorner(1f - (1f - grow) * (1f - grow), toUpperRight: false);
            }
            if (_swap != null) SwapPanels();
            RestorePanelTransform();
            if (_panel != null) BeginWave(fromLeft: false);
            for (float elapsed = 0f; _panel != null && elapsed < WaveDuration;)
            {
                yield return null;
                elapsed += GetFrameStep();
                UpdateWave(Mathf.Clamp01(elapsed / WaveDuration));
            }
            _transition = null;
            FinishOpening();
        }

        // 角を固定したままパネルを拡縮する。scaleが1で元の大きさ、0で角の一点に消える。
        private void SetPanelCorner(float scale, bool toUpperRight)
        {
            scale = Mathf.Clamp01(scale);
            _panel.localScale = Vector3.Scale(_panelScale, new Vector3(scale, scale, 1f));
            var rect = _panel.rect;
            var corner = Vector2.Scale(toUpperRight ? rect.max : rect.min, _panelScale);
            _panel.localPosition = _panelPosition + (Vector3)(corner * (1f - scale));
            if (_panelGroup != null) _panelGroup.alpha = _panelAlpha * Mathf.SmoothStep(0f, 1f, scale / CollapseFadeShare);
        }

        // 畳んだパネルと次のパネルを同じフレームで入れ替え、次のパネルの文字を消した状態で引き継ぐ。
        private void SwapPanels()
        {
            FinishOpening();
            if (_morphWindow != null) _morphWindow.EndMorph();
            _morphWindow = null;
            var swap = _swap;
            var target = _swapTarget;
            _swap = null;
            _swapTarget = null;
            swap?.Invoke();
            if (target == null || !target.gameObject.activeInHierarchy) return;
            CapturePanel(target);
            HidePanelContents(target);
        }

        /// <summary>移る演出を止め、表示を最終状態へ戻す。</summary>
        public void StopTransition()
        {
            if (_transition != null) StopCoroutine(_transition);
            _transition = null;
            if (_morphWindow != null) _morphWindow.EndMorph();
            _morphWindow = null;
            _swap = null;
            _swapTarget = null;
            FinishOpening();
        }

        /// <summary>再生中の演出をすべて最後まで進める。自動検証のように結果をすぐ確かめたい場合に使う。</summary>
        public void CompleteAnimations()
        {
            StopOpening();
            if (_transition != null)
            {
                StopCoroutine(_transition);
                _transition = null;
                if (_swap != null) SwapPanels();
                FinishOpening();
            }
            if (IsClosing && !_hasClosingAnimation) CompleteClosing();
        }

        /// <summary>閉じる演出を再生し、終わったらonClosedを呼ぶ。panelを渡すと中央へ縮めてから閉じる。</summary>
        public void PlayClosing(Action onClosed, RectTransform panel = null)
        {
            if (IsClosing) return;
            StopOpening();
            StopTransition();
            _onClosed = onClosed;
            try { _onClosing.Invoke(); }
            catch
            {
                CompleteClosing();
                throw;
            }
            if (_hasClosingAnimation) return;
            bool canShrink = panel != null && CloseDuration > 0f && isActiveAndEnabled && panel.gameObject.activeInHierarchy;
            if (canShrink) _closing = StartCoroutine(ShrinkPanel(panel));
            else CompleteClosing();
        }

        private IEnumerator ShrinkPanel(RectTransform panel)
        {
            _panel = panel;
            _panelScale = panel.localScale;
            _panelPosition = panel.localPosition;
            _panelGroup = panel.GetComponent<CanvasGroup>();
            _panelAlpha = _panelGroup != null ? _panelGroup.alpha : 1f;
            for (float elapsed = 0f; elapsed < CloseDuration;)
            {
                yield return null;
                elapsed += GetFrameStep();
                SetPanelExpansion(1f - Mathf.Clamp01(elapsed / CloseDuration));
            }
            _closing = null;
            CompleteClosing();
        }

        public void CompleteClosing()
        {
            StopShrinking();
            var onClosed = _onClosed;
            _onClosed = null;
            onClosed?.Invoke();
        }

        // 縮めたパネルを元の大きさへ戻す。閉じた後に戻すので、画面には映らない。
        private void StopShrinking()
        {
            if (_closing != null) StopCoroutine(_closing);
            _closing = null;
            if (_onClosed == null && _panel == null) return;
            RestorePanelTransform();
            _panel = null;
            _panelGroup = null;
        }

        public void CancelClosing()
        {
            StopShrinking();
            _onClosed = null;
        }

        private void OnDisable()
        {
            StopOpening();
            StopShrinking();
            StopTransition();
        }

        private void OnDestroy()
        {
            if (_revealMaterial != null) Destroy(_revealMaterial);
        }
    }
}
