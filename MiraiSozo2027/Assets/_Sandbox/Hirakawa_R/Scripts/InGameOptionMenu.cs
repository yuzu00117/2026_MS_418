using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MiraiSozo.Options
{
    [RequireComponent(typeof(GameOptions))]
    [DefaultExecutionOrder(-300)]
    public sealed class InGameOptionMenu : MonoBehaviour
    {
        private const float PreviewVolume = 0.2f;
        private const int PreviewSampleRate = 22050;
        private const int PreviewDurationSeconds = 2;
        private const float PreviewSEFrequency = 660f;
        private const float PreviewBGMFrequency = 220f;
        private const float PreviewVoiceFrequency = 440f;
        private const float PreviewAmplitude = 0.35f;
        private const float PreviewAttackRate = 30f;
        private const float PreviewReleaseRate = 6f;
        private const float RebindTimeoutSeconds = 10f;
        // フォント未指定時にOSのフォントから作る日本語フォントの基準サイズ。
        private const int RuntimeFontSize = 22;
        private static readonly string[] PreviewNames = { "SE", "BGM", "Voice" };
        private static bool _hasReportedMissingCanvas;
        public static InGameOptionMenu Instance { get; private set; }
        [Tooltip("Sceneに配置済みのオプション用Canvas。未設定時はGameManagerのMenu UI等から取得します。")]
        [SerializeField] private Canvas _canvas;
        [Tooltip("配布先で日本語フォントがない場合、同梱する日本語フォントを指定してください。")]
        [SerializeField] private Font _font;
        [SerializeField] private bool _showTestHud;
        private readonly Dictionary<string, Transform> _views = new Dictionary<string, Transform>();
        private readonly List<(Button Button, Text KeyLabel)> _bindingButtons = new List<(Button, Text)>();
        private readonly List<AudioSource> _previews = new List<AudioSource>();
        private readonly List<AudioClip> _previewClips = new List<AudioClip>();
        private readonly List<Action> _removeViewListeners = new List<Action>();
        private GameObject _testHud;
        private CanvasGroup _pauseGroup, _settingsGroup;
        private CanvasGroup _menuGroup;
        private OptionMenuAnimation _menuAnimation;
        private OptionDescriptionWindow _description;
        private OptionDialogAnimation _dialogAnimation;
        private OptionMenuSound _sound;
        private GameOptions _options;
        private Font _runtimeFont;
        private GameObject _overlay, _pause, _settings, _confirmation;
        private GameObject _confirmationOrigin;
        private EventSystem _ownedEventSystem;
        private InputActionRebindingExtensions.RebindingOperation _rebind;
        private Action _confirmAction;
        private int _tab;
        private int _ignorePauseThroughFrame = -1;
        public Canvas BoundCanvas { get; private set; }
        public bool IsOpen => BoundCanvas != null && _overlay != null && _overlay.activeSelf;
        public bool IsRebinding => _rebind != null;
        public bool IsClosing => _menuAnimation != null && _menuAnimation.IsClosing;
        public int ActiveTab => _tab;

#if UNITY_EDITOR
        public void ConfigureCanvas(Canvas canvas, bool isTestHudVisible = false)
        {
            _canvas = canvas;
            _showTestHud = isTestHudVisible;
            var testHud = canvas.transform.Find("Test HUD");
            if (testHud != null) testHud.gameObject.SetActive(isTestHudVisible);
        }
#endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetMissingCanvasNotice() => _hasReportedMissingCanvas = false;

        private Canvas FindExistingCanvas()
        {
            var menuUI = GameManager.Instance == null ? null : GameManager.Instance.MenuUI;
            if (menuUI != null)
            {
                var configured = menuUI.GetComponentInChildren<Canvas>(true);
                if (configured == null) configured = menuUI.GetComponentInParent<Canvas>(true);
                if (configured != null) return configured;
            }
            var child = GetComponentInChildren<Canvas>(true);
            if (child != null) return child;
            Canvas candidate = null;
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (canvas.transform.Find("Menu Overlay") == null) continue;
                if (candidate != null)
                {
                    Debug.LogWarning("オプション用Canvasの候補が複数あります。CanvasまたはGameManagerのMenu UIで使用する画面を指定してください。", this);
                    return null;
                }
                candidate = canvas;
            }
            if (candidate == null) ReportMissingCanvas();
            return candidate;
        }

        private void ReportMissingCanvas()
        {
            if (_hasReportedMissingCanvas) return;
            _hasReportedMissingCanvas = true;
            // タイトルなどオプションを使わないシーンでも出るため、エラーではなく案内として1回だけ出す。
            Debug.Log("オプション用Canvasが見つかりません。このシーンではオプションメニューを開けません。", this);
        }

        private void SetTestHudVisible(bool visible)
        {
            if (_testHud != null) _testHud.SetActive(visible);
        }

        private void Awake()
        {
            if (GameOptions.Instance == null || GameOptions.Instance.gameObject != gameObject) return;
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _options = GetComponent<GameOptions>();
            RefreshSceneView();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void RefreshSceneView()
        {
            if (TryInitializeView()) EnsureEventSystem();
            else if (_ownedEventSystem != null) _ownedEventSystem.gameObject.SetActive(false);
        }

        private bool TryInitializeView()
        {
            var canvas = _canvas != null ? _canvas : FindExistingCanvas();
            if (canvas != null && canvas == BoundCanvas) return true;
            UnwireControls();
            // オプションを使わないシーンにはCanvasを配置しない。
            if (canvas == null)
            {
                OptionMenuTheme.Use(null);
                return false;
            }
            if (!CacheViews(canvas)) return false;
            ApplyFont(canvas);
            SetTestHudVisible(_showTestHud);
            _overlay.SetActive(false);
            WireControls();
            if (_previews.Count == 0) CreateAudioPreviews();
            Refresh();
            BoundCanvas = canvas;
            return true;
        }

        private bool CacheViews(Canvas canvas)
        {
            _views.Clear();
            foreach (var view in canvas.GetComponentsInChildren<Transform>(true)) _views[view.name] = view;
            if (!ValidateRequiredViews(canvas)) return false;
            _overlay = GetView("Menu Overlay").gameObject;
            _pause = GetView("Pause Panel").gameObject;
            _settings = GetView("Options Panel").gameObject;
            _confirmation = GetView("Confirmation").gameObject;
            var testHud = canvas.transform.Find("Test HUD");
            _testHud = testHud != null ? testHud.gameObject : null;
            _pauseGroup = GetOrAddCanvasGroup(_pause);
            _settingsGroup = GetOrAddCanvasGroup(_settings);
            _menuGroup = GetOrAddCanvasGroup(_overlay);
            _menuAnimation = _overlay.GetComponent<OptionMenuAnimation>();
            // 見た目を整える前に、このシーンのCanvasのテーマへ切り替える（未指定なら標準のテーマに戻す）。
            OptionMenuTheme.Use(_menuAnimation != null ? _menuAnimation.Theme : null);
            OptionSoftStyle.ApplyPanel((RectTransform)_pause.transform, true);
            OptionSoftStyle.ApplyPanel((RectTransform)_settings.transform, false);
            var dialog = FindConfirmationDialog();
            OptionSoftStyle.ApplyPanel(dialog, true);
            _dialogAnimation = OptionDialogAnimation.Attach(dialog);
            OptionAmbientSonar.Attach((RectTransform)_pause.transform, fromLeft: false);
            OptionAmbientSonar.Attach((RectTransform)_settings.transform, fromLeft: true);
            _description = OptionDescriptionWindow.Create((RectTransform)_pause.transform,
                () => _menuAnimation != null && _menuAnimation.IsBusy, () => GetSound().PlayDescription());
            BindDescriptions();
            // 初めて開いたときに音の読み込みで遅れないよう、接続時に準備しておく。
            GetSound();
            OptionInputMode.Attach(_overlay, GetActivePanel, () => IsRebinding || IsSwapPending);
            return true;
        }

        // 確認画面の中で、メッセージとボタンをまとめている枠（なければ確認画面そのもの）。
        private RectTransform FindConfirmationDialog()
        {
            var root = _confirmation.transform;
            var message = GetView("Confirm Message");
            var container = message;
            while (container.parent != null && container.parent != root) container = container.parent;
            bool holdsButtons = GetView("Confirm Accept").IsChildOf(container) && GetView("Confirm Cancel").IsChildOf(container);
            bool isFrame = container != message && holdsButtons && container.GetComponent<Graphic>() != null;
            return (RectTransform)(isFrame ? container : root);
        }

        // キー操作を始めたときに一番上の項目を選ぶ範囲。
        private Transform GetActivePanel()
        {
            if (_confirmation.activeSelf) return _confirmation.transform;
            return _settings.activeSelf ? _settings.transform : _pause.transform;
        }

        // マウスを乗せたときに右側へ表示する説明。見出しにはボタンの表示名を使う。
        private void BindDescriptions()
        {
            _description.Bind(GetView<Button>("Resume"), "メニューを閉じて、中断したところからゲームを再開します。");
            _description.Bind(GetView<Button>("Return Title"), "ゲームを中断して、タイトル画面に戻ります。");
            _description.Bind(GetView<Button>("Return Stage"), "ゲームを中断して、ステージセレクト画面に戻ります。");
            _description.Bind(GetView<Button>("Open Options"), "ゲーム内の音量やカメラ、キー操作などを変えられます。");
            _description.Bind(GetView<Button>("Quit"), "ゲームを終了します。");
        }

        private bool ValidateRequiredViews(Canvas canvas)
        {
            return RequireViews<Transform>(canvas, "Menu Overlay", "Pause Panel", "Options Panel", "Confirmation")
                && RequireViews<Button>(canvas, "Resume", "Open Options", "Back", "Return Title", "Return Stage",
                    "Quit", "Reset Tab", "Confirm Cancel", "Confirm Accept", "FPS Previous", "FPS Next", "Preview Shake")
                && RequireViews<Text>(canvas, "Option Status", "FPS Value", "Confirm Message", "Confirm Accept Text")
                && RequireViews<Toggle>(canvas, "Invert X", "Invert Y", "Aim Assist")
                && RequireViews<GridLayoutGroup>(canvas, "Binding Content")
                && ValidateTabs(canvas)
                && ValidateSliders(canvas)
                && ValidateAudioPreviews(canvas);
        }

        private bool ValidateTabs(Canvas canvas)
        {
            for (int tab = 0; tab < OptionMenuLayout.TabCount; tab++)
            {
                string name = "Tab " + tab;
                if (!RequireViews<Button>(canvas, name) || !RequireViews<Image>(canvas, name)
                    || !RequireViews<Text>(canvas, name + " Text") || !RequireViews<Transform>(canvas, "Page " + tab))
                    return false;
            }
            return true;
        }

        private bool ValidateSliders(Canvas canvas)
        {
            foreach (string name in new[] { "Brightness", "FOV", "Sensitivity X", "Sensitivity Y", "Shake Reduction", "Master", "SE", "BGM", "Voice" })
                if (!RequireViews<Slider>(canvas, name) || !RequireViews<Text>(canvas, name + " Value")) return false;
            return true;
        }

        private bool ValidateAudioPreviews(Canvas canvas)
        {
            foreach (string name in PreviewNames)
                if (!RequireViews<Button>(canvas, "Preview " + name)) return false;
            return true;
        }

        private bool RequireViews<T>(Canvas canvas, params string[] names) where T : Component
        {
            foreach (string name in names)
            {
                if (_views.TryGetValue(name, out var view) && view.TryGetComponent<T>(out _)) continue;
                Debug.LogError($"オプション用Canvasの「{name}」に必要なUIコンポーネント（{typeof(T).Name}）がありません。", canvas);
                return false;
            }
            return true;
        }

        private static CanvasGroup GetOrAddCanvasGroup(GameObject panel)
        {
            var group = panel.GetComponent<CanvasGroup>();
            return group != null ? group : panel.AddComponent<CanvasGroup>();
        }

        private void Update()
        {
            if (!IsOpen || IsClosing || GameManager.Actions == null) return;
            // ESC/StartのPauseはGameManagerが処理する。同じ入力で二段階戻らないよう除外する。
            if (GameManager.Actions.UI.Cancel.triggered && !GameManager.Actions.All.Pause.triggered) HandlePause();
        }

        private void ApplyFont(Canvas canvas)
        {
            if (_font == null && _runtimeFont == null)
                _runtimeFont = Font.CreateDynamicFontFromOSFont(new[] { "Yu Gothic UI", "Meiryo", "Hiragino Sans", "Noto Sans CJK JP", "Arial" }, RuntimeFontSize);
            foreach (var text in canvas.GetComponentsInChildren<Text>(true)) text.font = _font != null ? _font : _runtimeFont;
        }

        private void EnsureEventSystem()
        {
            foreach (var existing in FindObjectsByType<EventSystem>())
            {
                if (existing == _ownedEventSystem || !existing.isActiveAndEnabled) continue;
                if (_ownedEventSystem != null) _ownedEventSystem.gameObject.SetActive(false);
                return;
            }
            if (_ownedEventSystem == null)
            {
                var root = new GameObject("Options EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
                root.transform.SetParent(transform, false);
                _ownedEventSystem = root.GetComponent<EventSystem>();
                root.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            }
            _ownedEventSystem.gameObject.SetActive(true);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            CancelRebind();
            CloseMenu();
            StopPreviews();
            RefreshSceneView();
        }

        private Transform GetView(string name) => _views[name];
        private T GetView<T>(string name) where T : Component => GetView(name).GetComponent<T>();
        private void BindClick(string name, Action action)
        {
            var button = GetView<Button>(name);
            UnityAction listener = () => action();
            button.onClick.AddListener(listener);
            _removeViewListeners.Add(() => { if (button != null) button.onClick.RemoveListener(listener); });
        }

        private void UnwireControls()
        {
            foreach (var remove in _removeViewListeners) remove();
            _removeViewListeners.Clear();
            foreach (var cell in _bindingButtons) if (cell.Button != null) cell.Button.onClick.RemoveAllListeners();
            _bindingButtons.Clear();
            BoundCanvas = null;
        }

        private void WireControls()
        {
            WireNavigation();
            WireTabs();
            WireSystemControls();
            WireCameraControls();
            WireAudioControls();
            WirePreviewControls();
        }

        private void WireNavigation()
        {
            BindClick("Resume", () =>
            {
                if(GameManager.Instance != null) GameManager.Instance.ResumePause();
                else CloseMenu();
            });
            BindClick("Open Options", OpenOptions);
            BindClick("Back", Back);
            BindClick("Return Title", () => ShowConfirmation("タイトル画面へ戻る機能は準備中です。", "閉じる", null));
            BindClick("Return Stage", () => ShowConfirmation("ステージセレクトへ戻る機能は準備中です。", "閉じる", null));
            BindClick("Quit", () => ShowConfirmation("ゲームを閉じますか？", "ゲームを閉じる", Quit));
            BindClick("Reset Tab", () => ShowConfirmation("このタブの設定を初期状態に戻しますか？", "初期化する", ResetActiveTab));
            BindClick("Confirm Cancel", DismissConfirmation);
            BindClick("Confirm Accept", () => { var action = _confirmAction; DismissConfirmation(); action?.Invoke(); });
        }

        private void ResetActiveTab()
        {
            bool saved = _options.ResetTab(_tab);
            Refresh();
            if (_tab == OptionMenuLayout.ControlsTab) BuildBindingRows();
            SetStatus(saved ? "このタブの設定を初期化しました。" : "初期化は反映しましたが、保存できませんでした。");
        }

        private void WireTabs()
        {
            for (int i = 0; i < OptionMenuLayout.TabCount; i++)
            {
                int tab = i;
                BindClick("Tab " + i, () => SwitchTab(tab));
            }
        }

        private void WireSystemControls()
        {
            BindClick("FPS Previous", () => CycleFrameRate(-1));
            BindClick("FPS Next", () => CycleFrameRate(1));
            BindSlider("Brightness", value => _options.Values.brightness = value, OptionSettingsCategory.System);
            BindSlider("FOV", value => _options.Values.fieldOfView = value, OptionSettingsCategory.System);
        }

        private void WireCameraControls()
        {
            BindSlider("Sensitivity X", value => _options.Values.sensitivityX = value, OptionSettingsCategory.Camera);
            BindSlider("Sensitivity Y", value => _options.Values.sensitivityY = value, OptionSettingsCategory.Camera);
            BindSlider("Shake Reduction", value => _options.Values.shakeReduction = value, OptionSettingsCategory.Camera);
            BindToggle("Invert X", value => _options.Values.invertX = value);
            BindToggle("Invert Y", value => _options.Values.invertY = value);
        }

        private void WireAudioControls()
        {
            BindSlider("Master", value => _options.Values.masterVolume = value, OptionSettingsCategory.Audio);
            BindSlider("SE", value => _options.Values.seVolume = value, OptionSettingsCategory.Audio);
            BindSlider("BGM", value => _options.Values.bgmVolume = value, OptionSettingsCategory.Audio);
            BindSlider("Voice", value => _options.Values.voiceVolume = value, OptionSettingsCategory.Audio);
        }

        private void WirePreviewControls()
        {
            BindClick("Preview Shake", PreviewShake);
            for (int i = 0; i < OptionMenuLayout.AudioChannelCount; i++)
            {
                int channel = i;
                BindClick("Preview " + PreviewNames[i], () => PreviewAudio(channel));
            }
        }

        private void PreviewShake()
        {
            var camera = Camera.main;
            if (camera == null) return;
            var shake = camera.GetComponent<OptionCameraShake>();
            if (shake == null) shake = camera.gameObject.AddComponent<OptionCameraShake>();
            shake.Play();
            SetStatus("画面揺れを確認中（軽減100%で揺れなし）");
        }

        private void BindSlider(string name, Action<float> change, OptionSettingsCategory category)
        {
            var slider = GetView<Slider>(name);
            UnityAction<float> listener = value => { change(value); ApplyChange(category); };
            slider.onValueChanged.AddListener(listener);
            _removeViewListeners.Add(() => { if (slider != null) slider.onValueChanged.RemoveListener(listener); });
        }

        private void BindToggle(string name, Action<bool> change)
        {
            var toggle = GetView<Toggle>(name);
            UnityAction<bool> listener = value => { change(value); ApplyChange(OptionSettingsCategory.Camera); };
            toggle.onValueChanged.AddListener(listener);
            _removeViewListeners.Add(() => { if (toggle != null) toggle.onValueChanged.RemoveListener(listener); });
        }

        private void ApplyChange(OptionSettingsCategory category)
        {
            _options.Apply(category);
            Refresh();
            SetStatus("設定を反映しました。変更は自動保存されます。");
        }

        private void CycleFrameRate(int delta)
        {
            var rates = OptionValues.FrameRates;
            int index = 0;
            for (int i = 0; i < rates.Count; i++)
                if (rates[i] == _options.Values.frameRate) { index = i; break; }
            _options.Values.frameRate = rates[(index + delta + rates.Count) % rates.Count];
            ApplyChange(OptionSettingsCategory.System);
        }

        public void HandlePause()
        {
            if (IsRebinding || IsClosing || Time.frameCount <= _ignorePauseThroughFrame) return;
            if (!IsOpen) OpenMenu();
            else Back();
        }

        public void OpenMenu()
        {
            if (BoundCanvas == null || IsOpen) return;
            BoundCanvas.gameObject.SetActive(true);
            SetTestHudVisible(false);
            _overlay.SetActive(true);
            _menuGroup.alpha = 1f;
            _menuGroup.interactable = true;
            _menuGroup.blocksRaycasts = true;
            _pause.SetActive(true);
            _settings.SetActive(false);
            _confirmation.SetActive(false);
            SetDialogInteraction(true);
            SelectControl("Resume");
            if (_menuAnimation != null) _menuAnimation.PlayOpening((RectTransform)_pause.transform);
            GetSound().PlayWindow();
        }

        public void OpenOptions()
        {
            if (BoundCanvas == null || IsClosing || IsSwapPending) return;
            // 閉じた状態から直接開く場合は、OpenMenuの音だけを鳴らす。
            if (!IsOpen) OpenMenu();
            else GetSound().PlayWindow();
            if (_menuAnimation == null || !_pause.activeSelf)
            {
                if (_menuAnimation != null) _menuAnimation.StopOpening();
                ShowOptionsPanel();
                return;
            }
            // 演出の途中（説明ウィンドウがオプション画面の大きさになった瞬間）にShowOptionsPanelが呼ばれる。
            _menuAnimation.PlayOptionsOpening((RectTransform)_pause.transform, (RectTransform)_settings.transform, _description, ShowOptionsPanel);
        }

        private bool IsSwapPending => _menuAnimation != null && _menuAnimation.IsSwapPending;

        private void ShowPausePanel()
        {
            _settings.SetActive(false);
            _pause.SetActive(true);
            SelectControl("Open Options");
        }

        private void ShowOptionsPanel()
        {
            _pause.SetActive(false);
            _settings.SetActive(true);
            SelectTab(_tab);
        }

        // ボタンからのタブ切り替え。実際にタブが変わったときだけ音を鳴らす。
        private void SwitchTab(int tab)
        {
            int previous = _tab;
            SelectTab(tab);
            if (_tab != previous) GetSound().PlayTabSwitch();
        }

        // メニューを閉じた後も鳴り終わるよう、常に有効なこのオブジェクトの下に作る。
        private OptionMenuSound GetSound() => _sound != null ? _sound : (_sound = OptionMenuSound.Create(transform));

        public void SelectTab(int tab)
        {
            bool isOpen = IsOpen;
            if (IsRebinding || IsClosing || (isOpen && _confirmation.activeSelf)) return;
            _tab = Mathf.Clamp(tab, OptionMenuLayout.SystemTab, OptionMenuLayout.ControlsTab);
            // 閉じている間は選択だけを記憶し、次に開いたときに画面へ反映する。
            if (!isOpen) return;
            for (int i = 0; i < OptionMenuLayout.TabCount; i++)
            {
                GetView("Page " + i).gameObject.SetActive(i == _tab);
                var button = GetView<Button>("Tab " + i);
                var colors = button.colors;
                colors.normalColor = i == _tab ? OptionMenuLayout.AccentSurface : OptionMenuLayout.Surface;
                button.colors = colors;
                GetView<Text>("Tab " + i + " Text").color = OptionMenuLayout.TextColor;
            }
            StopPreviews();
            Refresh();
            if (_tab == OptionMenuLayout.ControlsTab) BuildBindingRows();
            _options.Save();
            SetStatus("ESC  メニューに戻る");
            SelectControl("Tab " + _tab);
        }

        public void Back()
        {
            // 画面を入れ替える前の演出中は、戻る操作を受け付けない。
            if (IsClosing || IsSwapPending) return;
            // ポーズメニューのタブ戻り関数（Actions.UI.Cancelで呼び出し予定）
            if (IsRebinding) { CancelRebind(); return; }
            if (_confirmation.activeSelf) { DismissConfirmation(); return; }
            if (_settings.activeSelf)
            {
                StopPreviews();
                _options.Save();
                GetSound().PlayWindow();
                // 演出の途中（オプション画面を畳みきった瞬間）にShowPausePanelが呼ばれる。
                if (_menuAnimation != null) _menuAnimation.PlayOptionsClosing((RectTransform)_settings.transform, (RectTransform)_pause.transform, ShowPausePanel);
                else ShowPausePanel();
            }
            else
            {
                if(GameManager.Instance != null)
                {
                    GameManager.Instance.ResumePause();
                }
                else
                {
                    CloseMenu();
                }
            }
        }

        public void RequestClose(Action onClosed)
        {
            if (IsClosing) return;
            if (!IsOpen) { onClosed?.Invoke(); return; }
            GetSound().PlayWindow();
            CancelRebind();
            StopPreviews();
            _menuGroup.interactable = false;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            Action complete = () =>
            {
                try { CloseMenu(); }
                finally { onClosed?.Invoke(); }
            };
            if (_description != null) _description.Hide();
            var visiblePanel = (RectTransform)(_settings.activeSelf ? _settings : _pause).transform;
            if (_menuAnimation != null) _menuAnimation.PlayClosing(complete, visiblePanel);
            else complete();
        }

        public void CloseMenu()
        {
            // ポーズメニューの閉じる関数（Actions.All.Pauseで呼び出し予定）
            if (!IsOpen) return;
            if (IsClosing) { _menuAnimation.CompleteClosing(); return; }
            try
            {
                CancelRebind();
                StopPreviews();
                _options.Save();
            }
            finally
            {
                _confirmation.SetActive(false);
                if (_description != null) _description.HideImmediate();
                SetDialogInteraction(true);
                _confirmAction = null;
                _confirmationOrigin = null;
                _overlay.SetActive(false);
                SetTestHudVisible(_showTestHud);
                if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            }
        }

        private void Refresh()
        {
            if (_options == null) return;
            var v = _options.Values;
            GetView<Text>("FPS Value").text = v.frameRate < 0 ? "無制限" : v.frameRate + " FPS";
            SetSlider("Brightness", v.brightness, v.brightness.ToString("+0.0;-0.0;0.0"));
            SetSlider("FOV", v.fieldOfView, Mathf.RoundToInt(v.fieldOfView) + "°");
            SetSlider("Sensitivity X", v.sensitivityX, v.sensitivityX.ToString("0.00"));
            SetSlider("Sensitivity Y", v.sensitivityY, v.sensitivityY.ToString("0.00"));
            SetSlider("Shake Reduction", v.shakeReduction, FormatPercent(v.shakeReduction));
            SetSlider("Master", v.masterVolume, FormatPercent(v.masterVolume));
            SetSlider("SE", v.seVolume, FormatPercent(v.seVolume));
            SetSlider("BGM", v.bgmVolume, FormatPercent(v.bgmVolume));
            SetSlider("Voice", v.voiceVolume, FormatPercent(v.voiceVolume));
            GetView<Toggle>("Invert X").SetIsOnWithoutNotify(v.invertX);
            GetView<Toggle>("Invert Y").SetIsOnWithoutNotify(v.invertY);
        }

        private void SetSlider(string name, float value, string display)
        {
            GetView<Slider>(name).SetValueWithoutNotify(value);
            GetView<Text>(name + " Value").text = display;
        }
        private static string FormatPercent(float value) => Mathf.RoundToInt(value * 100) + "%";
        private void SetStatus(string text) => GetView<Text>("Option Status").text = text;
        private void SelectControl(string name)
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(GetView(name).gameObject);
        }

        private void ShowConfirmation(string text, string caption, Action action)
        {
            _confirmationOrigin = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            _confirmAction = action;
            SetDialogInteraction(false);
            GetView<Text>("Confirm Message").text = text;
            GetView<Text>("Confirm Accept Text").text = caption;
            _confirmation.SetActive(true);
            if (_dialogAnimation != null) _dialogAnimation.PlayOpen();
            GetSound().PlayWindow();
            SelectControl("Confirm Cancel");
        }

        private void DismissConfirmation()
        {
            if (_dialogAnimation != null && _dialogAnimation.IsClosing) return;
            _confirmAction = null;
            GetSound().PlayWindow();
            var origin = _confirmationOrigin;
            if (_dialogAnimation != null) _dialogAnimation.PlayClose(() => FinishDismiss(origin));
            else FinishDismiss(origin);
        }

        private void FinishDismiss(GameObject origin)
        {
            _confirmation.SetActive(false);
            SetDialogInteraction(true);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(origin);
        }

        private void SetDialogInteraction(bool interactable)
        {
            _pauseGroup.interactable = interactable;
            _settingsGroup.interactable = interactable;
        }

        private void Quit()
        {
            _options.Save();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void BuildBindingRows()
        {
            var grid = GetView<GridLayoutGroup>("Binding Content");
            var content = (RectTransform)grid.transform;
            foreach (Transform child in content) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            _bindingButtons.Clear();
            var entries = OptionRebinding.GetEntries(GameManager.Actions.Player.Get());
            int slotCount = Mathf.Max(OptionMenuLayout.MinimumBindingSlots, entries.Count);
            var font = _font != null ? _font : _runtimeFont;
            OptionMenuLayout.FitBindingGrid(grid, slotCount);
            foreach (var entry in entries) AddBindingButton(content, entry, font);
            for (int index = entries.Count; index < slotCount; index++)
                OptionMenuLayout.CreateReservedBindingSlot(content, font);
            LayoutRebuilder.ForceRebuildLayoutImmediate(content);
        }

        private void AddBindingButton(Transform content, OptionRebinding.Entry entry, Font font)
        {
            string display = entry.Action.GetBindingDisplayString(entry.Index);
            var button = OptionMenuLayout.CreateBindingButton(content, entry.Label, display, font, out var keyLabel);
            button.name = "Binding " + _bindingButtons.Count;
            button.onClick.AddListener(() => BeginRebind(entry, button));
            _bindingButtons.Add((button, keyLabel));
        }

        public void BeginRebind(OptionRebinding.Entry entry, Button button = null)
        {
            if (IsRebinding || !IsOpen || !_settings.activeSelf) return;
            _ignorePauseThroughFrame = Time.frameCount + 1;
            foreach (var selectable in _settings.GetComponentsInChildren<Selectable>(true)) selectable.interactable = false;
            SetStatus("「" + entry.Label + "」：キーまたはマウスボタンを押してください（ESCでキャンセル／"
                + Mathf.RoundToInt(RebindTimeoutSeconds) + "秒で自動キャンセル）");
            bool applied = false;
            string result = "キー変更をキャンセルしました。";
            _rebind = entry.Action.PerformInteractiveRebinding(entry.Index)
                .WithExpectedControlType("Button")
                .WithControlsHavingToMatchPath("<Keyboard>")
                .WithControlsHavingToMatchPath("<Mouse>")
                .WithCancelingThrough("<Keyboard>/escape")
                .WithTimeout(RebindTimeoutSeconds)
                .OnApplyBinding((operation, path) => { applied = OptionRebinding.TryApply(entry, path, out result); })
                .OnComplete(operation => CompleteRebind(applied, result, button))
                .OnCancel(operation => FinishRebind("キー変更をキャンセルしました。", button));
            _rebind.Start();
        }

        private void CompleteRebind(bool applied, string message, Button origin)
        {
            try
            {
                if (applied && !_options.SaveBindings()) message = "キー変更は反映しましたが、保存できませんでした。";
            }
            finally { FinishRebind(message, origin); }
        }

        public void CancelRebind() { _rebind?.Cancel(); }

        private void FinishRebind(string message, Button origin)
        {
            var operation = _rebind;
            _rebind = null;
            operation?.Dispose();
            _ignorePauseThroughFrame = Time.frameCount + 1;
            if (_settings == null) return;
            foreach (var selectable in _settings.GetComponentsInChildren<Selectable>(true)) selectable.interactable = true;
            GetView<Toggle>("Aim Assist").interactable = false;
            // コールバックの最中にボタンを破棄せず、表示文字列だけを更新する。
            var entries = OptionRebinding.GetEntries(GameManager.Actions.Player.Get());
            for (int i = 0; i < Mathf.Min(entries.Count, _bindingButtons.Count); i++)
                _bindingButtons[i].KeyLabel.text = entries[i].Action.GetBindingDisplayString(entries[i].Index);
            SetStatus(message);
            if (EventSystem.current != null && origin != null) EventSystem.current.SetSelectedGameObject(origin.gameObject);
        }

        private void CreateAudioPreviews()
        {
            for (int i = 0; i < OptionMenuLayout.AudioChannelCount; i++) CreateAudioPreview((OptionAudioCategory)i);
        }

        private void CreateAudioPreview(OptionAudioCategory category)
        {
            var root = new GameObject("Preview " + category);
            root.transform.SetParent(transform, false);
            var source = root.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.ignoreListenerPause = true;
            source.volume = PreviewVolume;
            root.AddComponent<OptionAudioChannel>().Category = category;
            var clip = CreatePreviewClip(category);
            source.clip = clip;
            _previews.Add(source);
            _previewClips.Add(clip);
        }

        private static AudioClip CreatePreviewClip(OptionAudioCategory category)
        {
            int length = PreviewSampleRate * PreviewDurationSeconds;
            var samples = new float[length];
            float frequency = GetPreviewFrequency(category);
            for (int sample = 0; sample < length; sample++)
            {
                float time = (float)sample / PreviewSampleRate;
                float envelope = Mathf.Clamp01(time * PreviewAttackRate) * Mathf.Clamp01((PreviewDurationSeconds - time) * PreviewReleaseRate);
                samples[sample] = Mathf.Sin(time * frequency * Mathf.PI * 2f) * envelope * PreviewAmplitude;
            }
            var clip = AudioClip.Create("Option " + category + " Test Tone", length, 1, PreviewSampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        private static float GetPreviewFrequency(OptionAudioCategory category)
        {
            switch (category)
            {
                case OptionAudioCategory.SE: return PreviewSEFrequency;
                case OptionAudioCategory.BGM: return PreviewBGMFrequency;
                default: return PreviewVoiceFrequency;
            }
        }

        private void PreviewAudio(int index)
        {
            bool wasPlaying = _previews[index].isPlaying;
            StopPreviews();
            if (!wasPlaying) _previews[index].Play();
            SetStatus(wasPlaying ? "試聴を停止しました。" : "試聴中です。");
        }
        private void StopPreviews() { foreach (var source in _previews) if (source != null) source.Stop(); }
        private void OnApplicationFocus(bool focused) { if (!focused && _options != null) _options.Save(); }
        private void OnDisable() { if (Instance == this) CloseMenu(); }
        private void OnDestroy()
        {
            if (Instance != this) return;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            CancelRebind();
            UnwireControls();
            foreach (var clip in _previewClips) if (clip != null) Destroy(clip);
            if (_runtimeFont != null) Destroy(_runtimeFont);
            Instance = null;
        }
    }
}
