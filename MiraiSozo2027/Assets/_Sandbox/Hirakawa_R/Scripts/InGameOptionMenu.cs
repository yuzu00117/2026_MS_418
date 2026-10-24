using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
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
        private static readonly string[] PreviewNames = { "SE", "BGM", "Voice" };
        public static InGameOptionMenu Instance { get; private set; }
        [SerializeField] private Canvas _canvas;
        [Tooltip("配布先で日本語フォントがない場合、同梱する日本語フォントを指定してください。")]
        [SerializeField] private Font _font;
        [SerializeField] private bool _showTestHud;
        private readonly Dictionary<string, Transform> _views = new Dictionary<string, Transform>();
        private readonly List<Button> _bindingButtons = new List<Button>();
        private readonly List<AudioSource> _previews = new List<AudioSource>();
        private readonly List<AudioClip> _previewClips = new List<AudioClip>();
        private GameOptions _options;
        private Font _runtimeFont;
        private GameObject _overlay, _pause, _settings, _confirmation;
        private GameObject _confirmationOrigin;
        private EventSystem _ownedEventSystem;
        private InputActionRebindingExtensions.RebindingOperation _rebind;
        private Action _confirmAction;
        private int _tab;
        private int _ignorePauseThroughFrame = -1;
        public bool IsOpen => _overlay != null && _overlay.activeSelf;
        public bool IsRebinding => _rebind != null;
        public int ActiveTab => _tab;

        public void BuildView(bool showTestHud = false)
        {
            _showTestHud = showTestHud;
            if (_canvas == null) _canvas = OptionMenuLayout.Build(transform);
            var testHud = _canvas.transform.Find("Test HUD");
            if (testHud != null) testHud.gameObject.SetActive(showTestHud);
        }

        private void Awake()
        {
            if (GameOptions.Instance == null || GameOptions.Instance.gameObject != gameObject) return;
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _options = GetComponent<GameOptions>();
            BuildView(_showTestHud);
            foreach (var view in _canvas.GetComponentsInChildren<Transform>(true)) _views[view.name] = view;
            ApplyFont();
            _overlay = GetView("Menu Overlay").gameObject;
            _pause = GetView("Pause Panel").gameObject;
            _settings = GetView("Options Panel").gameObject;
            _confirmation = GetView("Confirmation").gameObject;
            GetView("Test HUD").gameObject.SetActive(_showTestHud);
            _overlay.SetActive(false);
            EnsureEventSystem();
            WireControls();
            CreateAudioPreviews();
            Refresh();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void Update()
        {
            // メニューが開いており、キーリバインド中でなければ、Cancelを検知
            if(IsOpen && !IsRebinding && GameManager.Actions != null && GameManager.Actions.UI.Cancel.triggered)
            {
                Back();
            }
            // if(IsOpen && GameManager.Actions != null && GameManager.Actions.All.Pause.triggered)
            // {
            //     GameManager.Instance.ResumePause();
            // }
        }

        private void ApplyFont()
        {
            if (_font == null)
                _runtimeFont = Font.CreateDynamicFontFromOSFont(new[] { "Yu Gothic UI", "Meiryo", "Hiragino Sans", "Noto Sans CJK JP", "Arial" }, 22);
            foreach (var text in _canvas.GetComponentsInChildren<Text>(true)) text.font = _font != null ? _font : _runtimeFont;
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
            CloseMenu();
            EnsureEventSystem();
        }

        private Transform GetView(string name) => _views[name];
        private T GetView<T>(string name) where T : Component => GetView(name).GetComponent<T>();
        private void BindClick(string name, Action action) => GetView<Button>(name).onClick.AddListener(() => action());

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
            BindClick("Return Title", () => GetView<Text>("Pause Status").text = "タイトルへの接続は後日追加予定です。");
            BindClick("Return Stage", () => GetView<Text>("Pause Status").text = "ステージセレクトへの接続は後日追加予定です。");
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
                BindClick("Tab " + i, () => SelectTab(tab));
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
            GetView<Toggle>("Invert X").onValueChanged.AddListener(value => { _options.Values.invertX = value; ApplyChange(OptionSettingsCategory.Camera); });
            GetView<Toggle>("Invert Y").onValueChanged.AddListener(value => { _options.Values.invertY = value; ApplyChange(OptionSettingsCategory.Camera); });
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
            GetView<Slider>(name).onValueChanged.AddListener(value => { change(value); ApplyChange(category); });
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
            if (IsRebinding || Time.frameCount <= _ignorePauseThroughFrame) return;
            if (!IsOpen) OpenMenu();
            else CloseMenu();
            // else Back();
        }

        public void OpenMenu()
        {
            if (IsOpen) return;
            // OptionGameManager.Instance.SetPaused(true); //ポーズの切り替えはGameManager側で行う
            GetView("Test HUD").gameObject.SetActive(false);
            _overlay.SetActive(true);
            _pause.SetActive(true);
            _settings.SetActive(false);
            _confirmation.SetActive(false);
            SetDialogInteraction(true);
            GetView<Text>("Pause Status").text = "ESC  ゲームに戻る";
            SelectControl("Open Options");
        }

        public void OpenOptions()
        {
            if (!IsOpen) OpenMenu();
            _pause.SetActive(false);
            _settings.SetActive(true);
            SelectTab(_tab);
        }

        public void SelectTab(int tab)
        {
            if (IsRebinding || _confirmation.activeSelf) return;
            _tab = Mathf.Clamp(tab, OptionMenuLayout.SystemTab, OptionMenuLayout.ControlsTab);
            for (int i = 0; i < OptionMenuLayout.TabCount; i++)
            {
                GetView("Page " + i).gameObject.SetActive(i == _tab);
                GetView<Image>("Tab " + i).color = i == _tab ? OptionMenuLayout.Accent : OptionMenuLayout.Surface;
                GetView<Text>("Tab " + i + " Text").color = i == _tab ? OptionMenuLayout.Background : Color.white;
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
            // ポーズメニューのタブ戻り関数（Actions.UI.Cancelで呼び出し予定）
            if (IsRebinding) { CancelRebind(); return; }
            if (_confirmation.activeSelf) { DismissConfirmation(); return; }
            if (_settings.activeSelf)
            {
                StopPreviews();
                _options.Save();
                _settings.SetActive(false);
                _pause.SetActive(true);
                SelectControl("Open Options");
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

        public void CloseMenu()
        {
            // ポーズメニューの閉じる関数（Actions.All.Pauseで呼び出し予定）
            if (!IsOpen) return;
            try
            {
                CancelRebind();
                StopPreviews();
                _options.Save();
            }
            finally
            {
                _confirmation.SetActive(false);
                SetDialogInteraction(true);
                _confirmAction = null;
                _confirmationOrigin = null;
                _overlay.SetActive(false);
                GetView("Test HUD").gameObject.SetActive(_showTestHud);
                //if (OptionGameManager.Instance != null) OptionGameManager.Instance.SetPaused(false); //ポーズの切り替えはGameManager側で行う
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
            SelectControl("Confirm Cancel");
        }

        private void DismissConfirmation()
        {
            _confirmation.SetActive(false);
            SetDialogInteraction(true);
            _confirmAction = null;
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(_confirmationOrigin);
        }

        private void SetDialogInteraction(bool interactable)
        {
            foreach (var panel in new[] { _pause, _settings })
            {
                var group = panel.GetComponent<CanvasGroup>();
                if (group == null) group = panel.AddComponent<CanvasGroup>();
                group.interactable = interactable;
            }
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
            var content = GetView("Binding Content");
            foreach (Transform child in content) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            _bindingButtons.Clear();
            var entries = OptionRebinding.GetEntries(GameManager.Actions.Player.Get());
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                string display = entry.Action.GetBindingDisplayString(entry.Index);
                var button = OptionMenuLayout.CreateButton("Binding " + i, content, entry.Label + "     [ " + display + " ]", Vector2.zero, new Vector2(OptionMenuLayout.BindingWidth, OptionMenuLayout.BindingHeight));
                button.gameObject.AddComponent<LayoutElement>().preferredHeight = OptionMenuLayout.BindingHeight;
                button.GetComponentInChildren<Text>().font = _font != null ? _font : _runtimeFont;
                button.GetComponentInChildren<Text>().fontSize = OptionMenuLayout.BindingFontSize;
                button.onClick.AddListener(() => BeginRebind(entry, button));
                _bindingButtons.Add(button);
            }
            Canvas.ForceUpdateCanvases();
            GetView<ScrollRect>("Binding Viewport").verticalNormalizedPosition = 1f;
        }

        public void BeginRebind(OptionRebinding.Entry entry, Button button = null)
        {
            if (IsRebinding || !IsOpen || !_settings.activeSelf) return;
            _ignorePauseThroughFrame = Time.frameCount + 1;
            foreach (var selectable in _settings.GetComponentsInChildren<Selectable>(true)) selectable.interactable = false;
            SetStatus("「" + entry.Label + "」：キーを押してください（ESCでキャンセル／10秒で終了）");
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
            foreach (var selectable in _settings.GetComponentsInChildren<Selectable>(true)) selectable.interactable = true;
            GetView<Toggle>("Aim Assist").interactable = false;
            // コールバックの最中にボタンを破棄せず、表示文字列だけを更新する。
            var entries = OptionRebinding.GetEntries(GameManager.Actions.Player.Get());
            for (int i = 0; i < Mathf.Min(entries.Count, _bindingButtons.Count); i++)
                _bindingButtons[i].GetComponentInChildren<Text>().text = entries[i].Label + "     [ " + entries[i].Action.GetBindingDisplayString(entries[i].Index) + " ]";
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
            foreach (var clip in _previewClips) if (clip != null) Destroy(clip);
            if (_runtimeFont != null) Destroy(_runtimeFont);
            Instance = null;
        }
    }
}
