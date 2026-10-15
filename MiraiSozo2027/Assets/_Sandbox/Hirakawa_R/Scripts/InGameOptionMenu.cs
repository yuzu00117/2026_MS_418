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
        public static InGameOptionMenu Instance { get; private set; }
        [SerializeField] private Canvas _canvas;
        [Tooltip("配布先で日本語フォントがない場合、同梱する日本語フォントを指定してください。")]
        [SerializeField] private Font _font;
        [SerializeField] private bool _showTestHud;
        private readonly Dictionary<string, Transform> _views = new Dictionary<string, Transform>();
        private readonly List<Button> _bindingButtons = new List<Button>();
        private readonly List<AudioSource> _previews = new List<AudioSource>();
        private readonly List<AudioClip> _previewClips = new List<AudioClip>();
        private readonly int[] _frameRates = { 30, 60, 120, 144, -1 };
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
            _overlay = View("Menu Overlay").gameObject;
            _pause = View("Pause Panel").gameObject;
            _settings = View("Options Panel").gameObject;
            _confirmation = View("Confirmation").gameObject;
            View("Test HUD").gameObject.SetActive(_showTestHud);
            _overlay.SetActive(false);
            EnsureEventSystem();
            WireControls();
            CreateAudioPreviews();
            Refresh();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void ApplyFont()
        {
            if (_font == null)
                _runtimeFont = Font.CreateDynamicFontFromOSFont(new[] { "Yu Gothic UI", "Meiryo", "Hiragino Sans", "Noto Sans CJK JP", "Arial" }, 22);
            foreach (var text in _canvas.GetComponentsInChildren<Text>(true)) text.font = _font != null ? _font : _runtimeFont;
        }

        private void EnsureEventSystem()
        {
            EventSystem system = null;
            foreach (var existing in FindObjectsByType<EventSystem>())
            {
                if (existing == _ownedEventSystem || !existing.isActiveAndEnabled) continue;
                system = existing;
                if (existing == EventSystem.current) break;
            }
            if (system != null && _ownedEventSystem != null)
                _ownedEventSystem.gameObject.SetActive(false);
            if (system == null)
            {
                if (_ownedEventSystem == null)
                {
                    var root = new GameObject("Options EventSystem", typeof(EventSystem));
                    root.transform.SetParent(transform, false);
                    _ownedEventSystem = root.GetComponent<EventSystem>();
                }
                _ownedEventSystem.gameObject.SetActive(true);
                system = _ownedEventSystem;
            }
            if (system.GetComponent<InputSystemUIInputModule>() != null) return;
            foreach (var module in system.GetComponents<BaseInputModule>()) module.enabled = false;
            system.gameObject.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            CloseMenu();
            EnsureEventSystem();
        }

        private Transform View(string name) => _views[name];
        private T View<T>(string name) where T : Component => View(name).GetComponent<T>();
        private void Click(string name, Action action) => View<Button>(name).onClick.AddListener(() => action());

        private void WireControls()
        {
            Click("Resume", CloseMenu);
            Click("Open Options", OpenOptions);
            Click("Back", Back);
            Click("Return Title", () => View<Text>("Pause Status").text = "タイトルへの接続は後日追加予定です。");
            Click("Return Stage", () => View<Text>("Pause Status").text = "ステージセレクトへの接続は後日追加予定です。");
            Click("Quit", () => Confirm("ゲームを閉じますか？", "ゲームを閉じる", Quit));
            Click("Reset Tab", () => Confirm("このタブの設定を初期状態に戻しますか？", "初期化する", () =>
            {
                _options.ResetTab(_tab);
                Refresh();
                if (_tab == 3) BuildBindingRows();
                Status("このタブの設定を初期化しました。");
            }));
            Click("Confirm Cancel", DismissConfirmation);
            Click("Confirm Accept", () => { var action = _confirmAction; DismissConfirmation(); action?.Invoke(); });
            for (int i = 0; i < 4; i++) { int tab = i; Click("Tab " + i, () => SelectTab(tab)); }
            Click("FPS Previous", () => CycleFrameRate(-1));
            Click("FPS Next", () => CycleFrameRate(1));
            BindSlider("Brightness", value => _options.Values.brightness = value);
            BindSlider("FOV", value => _options.Values.fieldOfView = value);
            BindSlider("Sensitivity X", value => _options.Values.sensitivityX = value);
            BindSlider("Sensitivity Y", value => _options.Values.sensitivityY = value);
            BindSlider("Shake Reduction", value => _options.Values.shakeReduction = value);
            BindSlider("Master", value => _options.Values.masterVolume = value);
            BindSlider("SE", value => _options.Values.seVolume = value);
            BindSlider("BGM", value => _options.Values.bgmVolume = value);
            BindSlider("Voice", value => _options.Values.voiceVolume = value);
            View<Toggle>("Invert X").onValueChanged.AddListener(value => { _options.Values.invertX = value; ApplyChange(); });
            View<Toggle>("Invert Y").onValueChanged.AddListener(value => { _options.Values.invertY = value; ApplyChange(); });
            Click("Preview Shake", () =>
            {
                var camera = Camera.main;
                if (camera == null) return;
                var shake = camera.GetComponent<OptionCameraShake>();
                if (shake == null) shake = camera.gameObject.AddComponent<OptionCameraShake>();
                shake.Play();
                Status("画面揺れを確認中（軽減100%で揺れなし）");
            });
            for (int i = 0; i < 3; i++)
            {
                int channel = i;
                Click("Preview " + new[] { "SE", "BGM", "Voice" }[i], () => PreviewAudio(channel));
            }
        }

        private void BindSlider(string name, Action<float> change)
        {
            View<Slider>(name).onValueChanged.AddListener(value => { change(value); ApplyChange(); });
        }

        private void ApplyChange()
        {
            _options.Apply();
            // スライダー操作のたびにディスク同期せず、閉じる・タブ移動・フォーカス喪失時に確定。
            PlayerPrefs.SetString(GameOptions.SettingsKey, JsonUtility.ToJson(_options.Values));
            Refresh();
            Status("設定を反映しました。変更は自動保存されます。");
        }

        private void CycleFrameRate(int delta)
        {
            int index = Array.IndexOf(_frameRates, _options.Values.frameRate);
            _options.Values.frameRate = _frameRates[(index + delta + _frameRates.Length) % _frameRates.Length];
            ApplyChange();
        }

        public void HandlePause()
        {
            if (IsRebinding || Time.frameCount <= _ignorePauseThroughFrame) return;
            if (!IsOpen) OpenMenu();
            else Back();
        }

        public void OpenMenu()
        {
            if (IsOpen) return;
            OptionGameManager.Instance.SetPaused(true);
            View("Test HUD").gameObject.SetActive(false);
            _overlay.SetActive(true);
            _pause.SetActive(true);
            _settings.SetActive(false);
            _confirmation.SetActive(false);
            View<Text>("Pause Status").text = "ESC  ゲームに戻る";
            Focus("Open Options");
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
            _tab = Mathf.Clamp(tab, 0, 3);
            for (int i = 0; i < 4; i++)
            {
                View("Page " + i).gameObject.SetActive(i == _tab);
                View<Image>("Tab " + i).color = i == _tab ? OptionMenuLayout.Accent : OptionMenuLayout.Surface;
                View<Text>("Tab " + i + " Text").color = i == _tab ? OptionMenuLayout.Background : Color.white;
            }
            StopPreviews();
            Refresh();
            if (_tab == 3) BuildBindingRows();
            _options.Save();
            Status("ESC  メニューに戻る");
            Focus("Tab " + _tab);
        }

        public void Back()
        {
            if (IsRebinding) { CancelRebind(); return; }
            if (_confirmation.activeSelf) { DismissConfirmation(); return; }
            if (_settings.activeSelf)
            {
                StopPreviews();
                _options.Save();
                _settings.SetActive(false);
                _pause.SetActive(true);
                Focus("Open Options");
            }
            else CloseMenu();
        }

        public void CloseMenu()
        {
            if (!IsOpen) return;
            CancelRebind();
            StopPreviews();
            _options.Save();
            _overlay.SetActive(false);
            View("Test HUD").gameObject.SetActive(_showTestHud);
            if (OptionGameManager.Instance != null) OptionGameManager.Instance.SetPaused(false);
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
        }

        private void Refresh()
        {
            if (_options == null) return;
            var v = _options.Values;
            View<Text>("FPS Value").text = v.frameRate < 0 ? "無制限" : v.frameRate + " FPS";
            SetSlider("Brightness", v.brightness, v.brightness.ToString("+0.0;-0.0;0.0") + " EV");
            SetSlider("FOV", v.fieldOfView, Mathf.RoundToInt(v.fieldOfView) + "°");
            SetSlider("Sensitivity X", v.sensitivityX, v.sensitivityX.ToString("0.00"));
            SetSlider("Sensitivity Y", v.sensitivityY, v.sensitivityY.ToString("0.00"));
            SetSlider("Shake Reduction", v.shakeReduction, Percent(v.shakeReduction));
            SetSlider("Master", v.masterVolume, Percent(v.masterVolume));
            SetSlider("SE", v.seVolume, Percent(v.seVolume));
            SetSlider("BGM", v.bgmVolume, Percent(v.bgmVolume));
            SetSlider("Voice", v.voiceVolume, Percent(v.voiceVolume));
            View<Toggle>("Invert X").SetIsOnWithoutNotify(v.invertX);
            View<Toggle>("Invert Y").SetIsOnWithoutNotify(v.invertY);
        }

        private void SetSlider(string name, float value, string display)
        {
            View<Slider>(name).SetValueWithoutNotify(value);
            View<Text>(name + " Value").text = display;
        }
        private static string Percent(float value) => Mathf.RoundToInt(value * 100) + "%";
        private void Status(string text) => View<Text>("Option Status").text = text;
        private void Focus(string name)
        {
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(View(name).gameObject);
        }

        private void Confirm(string text, string caption, Action action)
        {
            _confirmationOrigin = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            _confirmAction = action;
            SetDialogInteraction(false);
            View<Text>("Confirm Message").text = text;
            View<Text>("Confirm Accept Text").text = caption;
            _confirmation.SetActive(true);
            Focus("Confirm Cancel");
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
            var content = View("Binding Content");
            foreach (Transform child in content) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            _bindingButtons.Clear();
            var entries = OptionRebinding.Entries(OptionGameManager.Actions.Player.Get());
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                string display = entry.Action.GetBindingDisplayString(entry.Index);
                var button = OptionMenuLayout.Button("Binding " + i, content, entry.Label + "     [ " + display + " ]", Vector2.zero, new Vector2(978, 42));
                button.gameObject.AddComponent<LayoutElement>().preferredHeight = 42;
                button.GetComponentInChildren<Text>().font = _font != null ? _font : _runtimeFont;
                button.GetComponentInChildren<Text>().fontSize = 18;
                button.onClick.AddListener(() => BeginRebind(entry, button));
                _bindingButtons.Add(button);
            }
            Canvas.ForceUpdateCanvases();
            View<ScrollRect>("Binding Viewport").verticalNormalizedPosition = 1f;
        }

        public void BeginRebind(OptionRebinding.Entry entry, Button button = null)
        {
            if (IsRebinding || !IsOpen || !_settings.activeSelf) return;
            _ignorePauseThroughFrame = Time.frameCount + 1;
            foreach (var selectable in _settings.GetComponentsInChildren<Selectable>(true)) selectable.interactable = false;
            Status("「" + entry.Label + "」: キーを押してください（ESCでキャンセル／10秒で終了）");
            bool applied = false;
            string result = "キー変更をキャンセルしました。";
            _rebind = entry.Action.PerformInteractiveRebinding(entry.Index)
                .WithExpectedControlType("Button")
                .WithControlsHavingToMatchPath("<Keyboard>")
                .WithControlsHavingToMatchPath("<Mouse>")
                .WithCancelingThrough("<Keyboard>/escape")
                .WithTimeout(10f)
                .OnApplyBinding((operation, path) => { applied = OptionRebinding.TryApply(entry, path, out result); })
                .OnComplete(operation => { if (applied) _options.SaveBindings(); FinishRebind(result, button); })
                .OnCancel(operation => FinishRebind("キー変更をキャンセルしました。", button));
            _rebind.Start();
        }

        public void CancelRebind() { _rebind?.Cancel(); }

        private void FinishRebind(string message, Button origin)
        {
            var operation = _rebind;
            _rebind = null;
            operation?.Dispose();
            _ignorePauseThroughFrame = Time.frameCount + 1;
            foreach (var selectable in _settings.GetComponentsInChildren<Selectable>(true)) selectable.interactable = true;
            View<Toggle>("Aim Assist").interactable = false;
            // コールバックの最中にボタンを破棄せず、表示文字列だけを更新する。
            var entries = OptionRebinding.Entries(OptionGameManager.Actions.Player.Get());
            for (int i = 0; i < Mathf.Min(entries.Count, _bindingButtons.Count); i++)
                _bindingButtons[i].GetComponentInChildren<Text>().text = entries[i].Label + "     [ " + entries[i].Action.GetBindingDisplayString(entries[i].Index) + " ]";
            Status(message);
            if (EventSystem.current != null && origin != null) EventSystem.current.SetSelectedGameObject(origin.gameObject);
        }

        private void CreateAudioPreviews()
        {
            for (int i = 0; i < 3; i++)
            {
                var root = new GameObject("Preview " + (OptionAudioCategory)i);
                root.transform.SetParent(transform, false);
                var source = root.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.ignoreListenerPause = true;
                source.volume = 0.2f;
                root.AddComponent<OptionAudioChannel>().Category = (OptionAudioCategory)i;
                int sampleRate = 22050;
                int length = sampleRate * 2;
                var samples = new float[length];
                for (int s = 0; s < length; s++)
                {
                    float t = (float)s / sampleRate;
                    float envelope = Mathf.Clamp01(t * 30f) * Mathf.Clamp01((2f - t) * 6f);
                    float frequency = i == 0 ? 660f : i == 1 ? 220f : 440f;
                    samples[s] = Mathf.Sin(t * frequency * Mathf.PI * 2f) * envelope * 0.35f;
                }
                var clip = AudioClip.Create("Option " + (OptionAudioCategory)i + " Test Tone", length, 1, sampleRate, false);
                clip.SetData(samples, 0);
                source.clip = clip;
                _previews.Add(source);
                _previewClips.Add(clip);
            }
        }

        private void PreviewAudio(int index)
        {
            bool wasPlaying = _previews[index].isPlaying;
            StopPreviews();
            if (!wasPlaying) _previews[index].Play();
            Status(wasPlaying ? "試聴を停止しました。" : "選んだカテゴリーのテスト音を再生中");
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
