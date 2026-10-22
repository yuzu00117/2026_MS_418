using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace MiraiSozo.Options.Editor
{
    /// <summary>実シーンとInput Systemイベントを通すPlayMode回帰チェック。個人の保存設定は復元する。</summary>
    [InitializeOnLoad]
    public static class OptionPlayValidation
    {
        private const string Pending = "Hirakawa.OptionValidation.Pending";
        private const string Success = "Hirakawa.OptionValidation.Success";
        private const string Output = "Logs/OptionValidation";
        private const double ValidationTimeoutSeconds = 90;
        private const int StartupFrameCount = 5;
        private const int StepFrameDelay = 3;
        private const double DefaultStepDelaySeconds = 0.25;
        private const double RebindDelaySeconds = 0.7;
        private const double CaptureDelaySeconds = 0.5;
        private const int CaptureDepthBits = 24;
        private const float CapturePlaneDistance = 1f;
        private const float CaptureScaleFactor = 1f;
        private const int SuccessExitCode = 0;
        private const int FailureExitCode = 1;

        // 検証入力と期待値。製品側の定数と独立させ、仕様値の意図しない変更も検出する。
        private static class TestValues
        {
            public const int ExpectedSingletonCount = 1;
            public const int MinimumBindingCount = 15;
            public const float PausedTimeScale = 0f;
            public const float PauseTimeScale = 0.4f;
            public const float DefaultTimeScale = 1f;
            public const float FieldOfView = 82f;
            public const float Brightness = 1.2f;
            public const int FrameRateAfterNext = 120;
            public const int ExpectedVSyncCount = 0;
            public const float SensitivityX = 0.64f;
            public const float SensitivityY = 0.38f;
            public const float UnrelatedFieldOfView = 75f;
            public const int UnrelatedFrameRate = 77;
            public const float MasterVolume = 0.35f;
            public const float SEVolume = 0.25f;
            public const float BGMVolume = 0.45f;
            public const float VoiceVolume = 0.65f;
            public const float ResetVolume = 1f;
            public const int InvalidFrameRate = 17;
            public const float InvalidSensitivity = -4f;
            public const float InvalidMasterVolume = 8f;
            public const int SanitizedFrameRate = 60;
            public const float SanitizedFieldOfView = 60f;
            public const float SanitizedSensitivity = 0.02f;
            public const float ExternalBaseVolume = 0.8f;
            public const float MutedVolume = 0f;
        }

        private static readonly List<string> Checks = new List<string>();
        private static Keyboard _keyboard;
        private static int _step;
        private static int _afterFrame;
        private static double _afterTime;
        private static double _started;
        private static bool _finishing;
        private static OptionRebinding.Entry _jump;
        private static Key[] _pendingKeys;
        private static InputSettings.BackgroundBehavior _previousBackground;
        private static InputSettings.EditorInputBehaviorInPlayMode _previousEditorInput;
        private static GameObject _legacyMenu;
        private static GameObject _portableRoot;
        private const string FixtureKey = "Hirakawa.OptionValidation.Fixture";

        static OptionPlayValidation()
        {
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
        }

        [MenuItem("Tools/Hirakawa R/Options/Run PlayMode Validation")]
        public static void Run()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("PlayModeを終了してから実行してください。");
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            CreatePrefabFixture();
            EditorSceneManager.OpenScene(OptionSceneSetup.ScenePath);
            _step = _afterFrame = 0;
            _started = _afterTime = 0;
            _finishing = false;
            Checks.Clear();
            Backup(GameOptions.SettingsKey);
            Backup(GameOptions.BindingsKey);
            try
            {
                PlayerPrefs.DeleteKey(GameOptions.SettingsKey);
                PlayerPrefs.DeleteKey(GameOptions.BindingsKey);
                PlayerPrefs.Save();
                SessionState.SetBool(Pending, true);
                SessionState.SetBool(Success, false);
                Directory.CreateDirectory(Output);
                EditorApplication.isPlaying = true;
            }
            catch
            {
                RestorePlayerPrefs();
                throw;
            }
        }

        private static void Backup(string key)
        {
            SessionState.SetBool(key + ".HadValue", PlayerPrefs.HasKey(key));
            SessionState.SetString(key + ".Backup", PlayerPrefs.GetString(key));
        }

        private static void Restore(string key)
        {
            if (SessionState.GetBool(key + ".HadValue", false)) PlayerPrefs.SetString(key, SessionState.GetString(key + ".Backup", ""));
            else PlayerPrefs.DeleteKey(key);
        }

        private static void OnPlayModeChanged(PlayModeStateChange change)
        {
            if (change != PlayModeStateChange.EnteredEditMode || !SessionState.GetBool(Pending, false)) return;
            RestorePlayerPrefs();
            var fixturePath = SessionState.GetString(FixtureKey, "");
            if (fixturePath.StartsWith(OptionSceneSetup.Root + "/Scenes/Scene_OptionPrefabValidation_", StringComparison.Ordinal))
                AssetDatabase.DeleteAsset(fixturePath);
            SessionState.EraseString(FixtureKey);
            bool success = SessionState.GetBool(Success, false);
            // バッチの成否はログ文字列でなく終了コードで通知する。
            if (Application.isBatchMode) EditorApplication.Exit(success ? SuccessExitCode : FailureExitCode);
        }

        private static void RestorePlayerPrefs()
        {
            Restore(GameOptions.SettingsKey);
            Restore(GameOptions.BindingsKey);
            PlayerPrefs.Save();
            SessionState.SetBool(Pending, false);
        }

        // 各ステップは元のフレーム待ち・入力投入順を保って実行する。
        private static readonly Action<InGameOptionMenu, GameOptions>[] ValidationSteps =
        {
            (menu, options) => InitializeInput(menu),
            ValidateOptionSettings,
            (menu, options) => StartJumpRebind(menu),
            (menu, options) => ValidateJumpRebind(menu),
            (menu, options) => PressEscape(),
            (menu, options) => ValidateRebindCancellation(menu),
            (menu, options) => CaptureSystemTab(menu),
            (menu, options) => CaptureAudioTab(menu),
            (menu, options) => CaptureControlsTab(menu),
            (menu, options) => PressEscape(),
            (menu, options) => ValidatePauseMenu(menu),
            (menu, options) => PressEscape(),
            (menu, options) => ValidatePauseRestoration(menu),
            ValidatePrefabScene,
            (menu, options) => ReturnToTestScene(menu),
            (menu, options) => ValidatePersistentMenu(menu),
            (menu, options) => MoveWithConfirmation(menu),
            ValidateSceneCleanup,
        };

        private static void Tick()
        {
            if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || _finishing) return;
            if (_started == 0) _started = EditorApplication.timeSinceStartup;
            if (EditorApplication.timeSinceStartup - _started > ValidationTimeoutSeconds) { Finish(false, "PlayMode validation timeout"); return; }
            if (Time.frameCount < _afterFrame || EditorApplication.timeSinceStartup < _afterTime) return;
            var menu = InGameOptionMenu.Instance;
            var options = GameOptions.Instance;
            if (menu == null || options == null || Time.frameCount < StartupFrameCount) return;
            try
            {
                ValidationSteps[_step](menu, options);
            }
            catch (Exception e) { Finish(false, e.ToString()); }
        }

        private static void InitializeInput(InGameOptionMenu menu)
        {
            _previousBackground = InputSystem.settings.backgroundBehavior;
            _previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            _keyboard = InputSystem.AddDevice<Keyboard>("Option validation keyboard");
            InputSystem.onBeforeUpdate += FeedKeys;
            Assert(!menu.IsOpen && OptionGameManager.Actions.Player.enabled, "開始時はメニュー非表示・ゲーム入力有効");
            Assert(menu.GetComponentsInChildren<Canvas>(true).Length == TestValues.ExpectedSingletonCount, "PrefabからCanvasを重複生成しない");
            Time.timeScale = TestValues.PauseTimeScale;
            OptionGameManager.Actions.UI.Disable();
            _legacyMenu = new GameObject("Legacy Menu Probe");
            _legacyMenu.SetActive(false);
            var manager = new SerializedObject(OptionGameManager.Instance);
            manager.FindProperty("_menuUI").objectReferenceValue = _legacyMenu;
            manager.ApplyModifiedPropertiesWithoutUndo();
            QueueKeys(Key.Escape);
            AdvanceStep();
        }

        private static void ValidateSystemOptions(InGameOptionMenu menu)
        {
            menu.SelectTab(OptionMenuLayout.SystemTab);
            FindMenuComponent<Slider>("FOV").value = TestValues.FieldOfView;
            FindMenuComponent<Slider>("Brightness").value = TestValues.Brightness;
            FindMenuComponent<Button>("FPS Next").onClick.Invoke();
            Assert(Mathf.Approximately(Camera.main.fieldOfView, TestValues.FieldOfView), "視野角をメインカメラへ反映");
            Assert(Application.targetFrameRate == TestValues.FrameRateAfterNext && QualitySettings.vSyncCount == TestValues.ExpectedVSyncCount, "FPSと垂直同期を反映");
            var volume = UnityEngine.Object.FindObjectsByType<Volume>().Single(v => v.name == "Option Brightness (Exposure)");
            Assert(volume.sharedProfile.TryGet(out ColorAdjustments exposure) && Mathf.Approximately(exposure.postExposure.value, TestValues.Brightness), "明度をURP露出へ反映");
        }

        private static void ValidateCameraOptions(InGameOptionMenu menu)
        {
            menu.SelectTab(OptionMenuLayout.CameraTab);
            FindMenuComponent<Slider>("Sensitivity X").value = TestValues.SensitivityX;
            FindMenuComponent<Slider>("Sensitivity Y").value = TestValues.SensitivityY;
            FindMenuComponent<Toggle>("Invert X").isOn = true;
            FindMenuComponent<Toggle>("Invert Y").isOn = true;
            var cameraBehavior = Camera.main.GetComponent<OptionCameraBehavior>();
            Assert(Mathf.Approximately(cameraBehavior.SensitivityX, TestValues.SensitivityX) && Mathf.Approximately(cameraBehavior.SensitivityY, TestValues.SensitivityY) && cameraBehavior.InvertX && cameraBehavior.InvertY, "感度・左右上下反転をCameraBehaviorへ反映");
        }

        private static void ValidateAudioOptions(InGameOptionMenu menu)
        {
            menu.SelectTab(OptionMenuLayout.AudioTab);
            Camera.main.fieldOfView = TestValues.UnrelatedFieldOfView;
            Application.targetFrameRate = TestValues.UnrelatedFrameRate;
            FindMenuComponent<Slider>("Master").value = TestValues.MasterVolume;
            FindMenuComponent<Slider>("SE").value = TestValues.SEVolume;
            FindMenuComponent<Slider>("BGM").value = TestValues.BGMVolume;
            FindMenuComponent<Slider>("Voice").value = TestValues.VoiceVolume;
            Assert(Mathf.Approximately(Camera.main.fieldOfView, TestValues.UnrelatedFieldOfView) && Application.targetFrameRate == TestValues.UnrelatedFrameRate, "音量変更で視野角・FPSを再適用しない");
            Camera.main.fieldOfView = TestValues.FieldOfView;
            Application.targetFrameRate = TestValues.FrameRateAfterNext;
            Assert(Mathf.Approximately(AudioListener.volume, TestValues.MasterVolume), "マスター音量の反映");
            foreach (var channel in menu.GetComponentsInChildren<OptionAudioChannel>())
            {
                float expected = channel.Category == OptionAudioCategory.SE ? TestValues.SEVolume : channel.Category == OptionAudioCategory.BGM ? TestValues.BGMVolume : TestValues.VoiceVolume;
                Assert(Mathf.Approximately(channel.GetComponent<AudioSource>().volume, channel.BaseVolume * expected), "カテゴリー音量の独立反映 " + channel.Category);
            }
        }

        private static void ValidateSavedOptions(GameOptions options)
        {
            options.Save();
            var saved = JsonUtility.FromJson<OptionValues>(PlayerPrefs.GetString(GameOptions.SettingsKey));
            Assert(Mathf.Approximately(saved.sensitivityX, TestValues.SensitivityX) && Mathf.Approximately(saved.masterVolume, TestValues.MasterVolume), "変更値をPlayerPrefsへ保存");
            options.ResetTab(OptionMenuLayout.AudioTab);
            Assert(Mathf.Approximately(options.Values.masterVolume, TestValues.ResetVolume) && Mathf.Approximately(options.Values.sensitivityX, TestValues.SensitivityX), "タブ初期化が他カテゴリーを保持");
        }

        private static void ValidateControlsList(InGameOptionMenu menu)
        {
            menu.SelectTab(OptionMenuLayout.ControlsTab);
            Assert(!FindMenuComponent<Toggle>("Aim Assist").interactable, "未対応エイムアシストを明示");
            var entries = OptionRebinding.GetEntries(OptionGameManager.Actions.Player.Get());
            Assert(entries.Count >= TestValues.MinimumBindingCount && entries.Count == FindMenuComponent<Transform>("Binding Content").Cast<Transform>().Count(child => child.gameObject.activeSelf), "キー設定一覧に移動の複合バインドとゲーム操作を表示");
            _jump = entries.First(e => e.Action.name == "Jump");
        }

        private static void ValidateOptionSettings(InGameOptionMenu menu, GameOptions options)
        {
            Assert(menu.IsOpen && OptionGameManager.Instance.IsPaused && Time.timeScale == TestValues.PausedTimeScale, "ESC入力でメニュー表示・ゲーム停止 (open=" + menu.IsOpen + ", paused=" + OptionGameManager.Instance.IsPaused + ", timeScale=" + Time.timeScale + ")");
            Assert(!OptionGameManager.Actions.Player.enabled && Cursor.lockState == CursorLockMode.None, "ポーズ中はプレイヤー入力停止・カーソル解放");
            Assert(!_legacyMenu.activeSelf, "既存のポーズUIと新メニューを二重表示しない");
            QueueKeys();
            menu.OpenOptions();
            for (int i = 0; i < OptionMenuLayout.TabCount; i++)
            {
                menu.SelectTab(i);
                Assert(FindMenuComponent<Transform>("Page " + i).gameObject.activeInHierarchy, "タブ切り替え " + i);
            }
            ValidateSystemOptions(menu);
            ValidateCameraOptions(menu);
            ValidateAudioOptions(menu);
            ValidateSavedOptions(options);
            ValidateControlsList(menu);
            AdvanceStep();
        }

        private static void StartJumpRebind(InGameOptionMenu menu)
        {
            menu.BeginRebind(_jump);
            Assert(menu.IsRebinding, "Input Systemのリバインド開始");
            QueueKeys(Key.J);
            AdvanceStep(RebindDelaySeconds);
        }

        private static void ValidateJumpRebind(InGameOptionMenu menu)
        {
            Assert(!menu.IsRebinding && _jump.Action.bindings[_jump.Index].effectivePath == "<Keyboard>/j", "実際のキー入力でジャンプをJに変更 (rebinding=" + menu.IsRebinding + ", path=" + _jump.Action.bindings[_jump.Index].effectivePath + ", status=" + FindMenuComponent<Text>("Option Status").text + ")");
            Assert(!OptionGameManager.Actions.Player.enabled, "キー変更後もゲーム入力を停止");
            using (var actions = new InputSystem_Actions())
            {
                actions.asset.LoadBindingOverridesFromJson(PlayerPrefs.GetString(GameOptions.BindingsKey));
                Assert(actions.Player.Jump.bindings.Any(b => b.effectivePath == "<Keyboard>/j"), "キー変更を別のInputActionAssetへ復元");
            }
            Assert(!OptionRebinding.TryApply(_jump, "<Keyboard>/w", out _), "他の操作と重複するキーを拒否");
            Assert(!OptionRebinding.TryApply(_jump, "<Keyboard>/escape", out _), "ESCをゲーム操作へ割り当てない");
            QueueKeys();
            menu.BeginRebind(_jump);
            AdvanceStep();
        }

        private static void PressEscape()
        {
            QueueKeys(Key.Escape);
            AdvanceStep();
        }

        private static void ValidateRebindCancellation(InGameOptionMenu menu)
        {
            Assert(!menu.IsRebinding && menu.IsOpen && FindMenuComponent<Transform>("Options Panel").gameObject.activeInHierarchy, "リバインド中のESCはキャンセルだけを実行");
            Assert(_jump.Action.bindings[_jump.Index].effectivePath == "<Keyboard>/j", "キャンセルは割り当てを保持");
            QueueKeys();
            menu.SelectTab(OptionMenuLayout.CameraTab);
            Capture(Output + "/options-camera.png");
            AdvanceStep(CaptureDelaySeconds);
        }

        private static void CaptureSystemTab(InGameOptionMenu menu)
        {
            menu.SelectTab(OptionMenuLayout.SystemTab);
            Capture(Output + "/options-system.png");
            AdvanceStep(CaptureDelaySeconds);
        }

        private static void CaptureAudioTab(InGameOptionMenu menu)
        {
            menu.SelectTab(OptionMenuLayout.AudioTab);
            Capture(Output + "/options-audio.png");
            AdvanceStep(CaptureDelaySeconds);
        }

        private static void CaptureControlsTab(InGameOptionMenu menu)
        {
            menu.SelectTab(OptionMenuLayout.ControlsTab);
            Capture(Output + "/options-controls.png");
            AdvanceStep(CaptureDelaySeconds);
        }

        private static void ValidatePauseMenu(InGameOptionMenu menu)
        {
            Assert(menu.IsOpen && FindMenuComponent<Transform>("Pause Panel").gameObject.activeInHierarchy, "オプション中のESCでメニューへ戻る");
            QueueKeys();
            string scene = SceneManager.GetActiveScene().path;
            FindMenuComponent<Button>("Return Title").onClick.Invoke();
            FindMenuComponent<Button>("Return Stage").onClick.Invoke();
            Assert(SceneManager.GetActiveScene().path == scene, "タイトル・ステージセレクトはシーン遷移しない");
            FindMenuComponent<Button>("Quit").onClick.Invoke();
            Assert(FindMenuComponent<Transform>("Confirmation").gameObject.activeInHierarchy, "終了ボタンは確認画面を表示");
            FindMenuComponent<Button>("Confirm Cancel").onClick.Invoke();
            Assert(!FindMenuComponent<Transform>("Confirmation").gameObject.activeSelf && menu.IsOpen, "終了キャンセルでゲームを保持");
            FindMenuComponent<Button>("Quit").onClick.Invoke();
            menu.CloseMenu();
            menu.OpenMenu();
            Assert(!FindMenuComponent<Transform>("Confirmation").gameObject.activeSelf && FindMenuComponent<Button>("Open Options").IsInteractable(), "確認画面の途中で閉じても次回のメニューを操作できる");
            Capture(Output + "/pause-menu.png");
            AdvanceStep(CaptureDelaySeconds);
        }

        private static void ValidatePauseRestoration(InGameOptionMenu menu)
        {
            Assert(!menu.IsOpen && !OptionGameManager.Instance.IsPaused && Mathf.Approximately(Time.timeScale, TestValues.PauseTimeScale), "ESCで閉じて元の時間倍率を復元");
            Assert(OptionGameManager.Actions.Player.enabled && !OptionGameManager.Actions.UI.enabled, "元のアクションマップ状態を復元");
            QueueKeys();
            OptionGameManager.Actions.Player.Disable();
            menu.OpenMenu();
            menu.CloseMenu();
            Assert(!OptionGameManager.Actions.Player.enabled, "もともと停止していたプレイヤー入力は停止状態に復元");
            OptionGameManager.Actions.Player.Enable();
            Time.timeScale = TestValues.DefaultTimeScale;
            var malformed = new OptionValues { frameRate = TestValues.InvalidFrameRate, fieldOfView = float.NaN, sensitivityX = TestValues.InvalidSensitivity, masterVolume = TestValues.InvalidMasterVolume };
            malformed.Sanitize();
            Assert(malformed.frameRate == TestValues.SanitizedFrameRate && malformed.fieldOfView == TestValues.SanitizedFieldOfView && malformed.sensitivityX == TestValues.SanitizedSensitivity && malformed.masterVolume == TestValues.ResetVolume, "範囲外・非数の保存値を補正");
            UnityEngine.Object.DestroyImmediate(menu.gameObject);
            LoadFixture();
            AdvanceStep(CaptureDelaySeconds);
        }

        private static void ValidatePrefabScene(InGameOptionMenu menu, GameOptions options)
        {
            _portableRoot = menu.gameObject;
            Assert(SceneManager.GetActiveScene().path == SessionState.GetString(FixtureKey, ""), "別Sceneに共通Prefabを配置して起動");
            Assert(!menu.IsOpen && !FindMenuComponent<Transform>("Test HUD").gameObject.activeSelf, "共通PrefabはメニューとテストHUDが初期非表示");
            Assert(OptionGameManager.Instance.transform.IsChildOf(menu.transform), "自動生成したゲーム管理もPrefab内に保持");
            Assert(UnityEngine.Object.FindObjectsByType<EventSystem>().Length == TestValues.ExpectedSingletonCount && !EventSystem.current.transform.IsChildOf(menu.transform), "既存のEventSystemを再利用");
            ValidateExternalEventSystem();
            Assert(Mathf.Approximately(Camera.main.fieldOfView, options.Values.fieldOfView), "別SceneのMainCameraへ保存した視野角を反映");
            Assert(OptionGameManager.Actions.Player.Jump.bindings.Any(b => b.effectivePath == "<Keyboard>/j"), "別Sceneでも保存したキー配置を復元");
            QueueKeys(Key.Escape);
            AdvanceStep();
        }

        private static void ReturnToTestScene(InGameOptionMenu menu)
        {
            Assert(menu.IsOpen && Time.timeScale == TestValues.PausedTimeScale, "別SceneでもESCでメニューを開く");
            QueueKeys();
            menu.CloseMenu();
            EditorSceneManager.LoadSceneInPlayMode(OptionSceneSetup.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
            AdvanceStep(CaptureDelaySeconds);
        }

        private static void ValidatePersistentMenu(InGameOptionMenu menu)
        {
            Assert(menu.gameObject == _portableRoot && UnityEngine.Object.FindObjectsByType<InGameOptionMenu>().Length == TestValues.ExpectedSingletonCount, "SceneごとのPrefab配置でもメニューを重複させない");
            Assert(UnityEngine.Object.FindObjectsByType<EventSystem>().Length == TestValues.ExpectedSingletonCount && EventSystem.current.transform.IsChildOf(menu.transform), "EventSystemのない移動先では自動生成");
            Assert(!FindMenuComponent<Transform>("Test HUD").gameObject.activeSelf, "Scene移動後も共通PrefabのHUD設定を保持");
            QueueKeys(Key.Escape);
            AdvanceStep();
        }

        private static void MoveWithConfirmation(InGameOptionMenu menu)
        {
            Assert(menu.IsOpen && Time.timeScale == TestValues.PausedTimeScale, "移動先でもESCメニューが機能");
            QueueKeys();
            menu.OpenOptions();
            FindMenuComponent<Button>("Reset Tab").onClick.Invoke();
            Assert(FindMenuComponent<Transform>("Confirmation").gameObject.activeSelf, "確認画面を開いた状態でScene移動を開始");
            LoadFixture();
            AdvanceStep(CaptureDelaySeconds);
        }

        private static void ValidateSceneCleanup(InGameOptionMenu menu, GameOptions options)
        {
            Assert(!menu.IsOpen && !OptionGameManager.Instance.IsPaused && Mathf.Approximately(Time.timeScale, TestValues.DefaultTimeScale), "メニュー表示中のScene移動でポーズを解除");
            Assert(UnityEngine.Object.FindObjectsByType<EventSystem>().Length == TestValues.ExpectedSingletonCount && !EventSystem.current.transform.IsChildOf(menu.transform), "再移動先のEventSystemに切り替えて重複を防止");
            Assert(menu.gameObject == _portableRoot && UnityEngine.Object.FindObjectsByType<InGameOptionMenu>().Length == TestValues.ExpectedSingletonCount, "往復後もPrefabを1つだけ保持");
            menu.OpenMenu();
            Assert(!FindMenuComponent<Transform>("Confirmation").gameObject.activeSelf && FindMenuComponent<Button>("Open Options").IsInteractable(), "確認画面からScene移動しても次回のメニューを操作できる");
            menu.CloseMenu();
            ValidateSaveFailure(menu);
            ValidateMenuFailureCleanup(menu);
            ValidateManagerRecreation(menu);
            ValidateAudioLifecycle(menu, options);
            Finish(true, "OPTION_PLAY_CHECKS_OK");
        }

        private static T FindMenuComponent<T>(string name) where T : Component
        {
            return InGameOptionMenu.Instance.GetComponentsInChildren<T>(true).First(v => v.name == name);
        }

        private static void ValidateExternalEventSystem()
        {
            var system = UnityEngine.Object.FindObjectsByType<EventSystem>().Single(s => s.name == "Scene EventSystem");
            var modules = system.GetComponents<BaseInputModule>();
            Assert(modules.Length == TestValues.ExpectedSingletonCount && modules[0] is StandaloneInputModule && !modules[0].enabled,
                "既存EventSystemのモジュールを追加・有効化・無効化しない");
        }

        private static void ValidateSaveFailure(InGameOptionMenu menu)
        {
            var save = typeof(GameOptions).GetMethod("TrySave", BindingFlags.NonPublic | BindingFlags.Static);
            Func<string> fail = () => throw new IOException("Option validation: simulated save failure");
            bool result = (bool)save.Invoke(null, new object[] { GameOptions.SettingsKey, fail });
            Assert(!result, "保存処理の例外を呼び出し元へ伝播しない");
            menu.OpenMenu();
            menu.CloseMenu();
            Assert(!menu.IsOpen && !OptionGameManager.Instance.IsPaused && Mathf.Approximately(Time.timeScale, TestValues.DefaultTimeScale),
                "保存失敗後もメニューを閉じてポーズを解除できる");
        }

        private static void ValidateMenuFailureCleanup(InGameOptionMenu menu)
        {
            menu.OpenOptions();
            menu.SelectTab(OptionMenuLayout.ControlsTab);
            var entry = OptionRebinding.GetEntries(OptionGameManager.Actions.Player.Get()).First(e => e.Action.name == "Jump");
            menu.BeginRebind(entry);
            InvokeWithUnavailableSettings(menu, () => typeof(InGameOptionMenu)
                .GetMethod("CompleteRebind", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(menu, new object[] { true, "検証用", null }));
            Assert(!menu.IsRebinding && FindMenuComponent<Button>("Back").interactable && FindMenuComponent<Button>("Binding 0").interactable,
                "キー保存中の例外でもリバインドを終了して操作を復元");
            InvokeWithUnavailableSettings(menu, menu.CloseMenu);
            Assert(!menu.IsOpen && !OptionGameManager.Instance.IsPaused && Mathf.Approximately(Time.timeScale, TestValues.DefaultTimeScale),
                "保存呼び出しが例外でもメニューを閉じてポーズを解除");
        }

        private static void InvokeWithUnavailableSettings(InGameOptionMenu menu, Action action)
        {
            // 保存呼び出し自体が例外になる状態を作り、finallyによるUI復旧を確認する。
            var field = typeof(InGameOptionMenu).GetField("_options", BindingFlags.Instance | BindingFlags.NonPublic);
            var previous = field.GetValue(menu);
            bool failed = false;
            try
            {
                field.SetValue(menu, null);
                try { action(); }
                catch (NullReferenceException) { failed = true; }
                catch (TargetInvocationException e) when (e.InnerException is NullReferenceException) { failed = true; }
            }
            finally { field.SetValue(menu, previous); }
            Assert(failed, "保存呼び出しの異常を検証用に発生させる");
        }

        private static void ValidateManagerRecreation(InGameOptionMenu menu)
        {
            UnityEngine.Object.DestroyImmediate(OptionGameManager.Instance.gameObject);
            var root = new GameObject("Replacement OptionGameManager");
            root.AddComponent<OptionGameManager>();
            root.transform.SetParent(menu.transform, false);
            Assert(OptionGameManager.Actions.Player.Jump.bindings.Any(b => b.effectivePath == "<Keyboard>/j"),
                "専用GameManagerの再生成イベントでキー配置を復元");
        }

        private static void ValidateAudioLifecycle(InGameOptionMenu menu, GameOptions options)
        {
            var root = new GameObject("External Audio Probe");
            var source = root.AddComponent<AudioSource>();
            source.volume = TestValues.ExternalBaseVolume;
            var channel = root.AddComponent<OptionAudioChannel>();
            channel.Category = OptionAudioCategory.SE;
            options.Values.seVolume = TestValues.MutedVolume;
            options.Apply(OptionSettingsCategory.Audio);
            Assert(Mathf.Approximately(source.volume, TestValues.MutedVolume), "外部AudioSourceにカテゴリー音量を反映");
            UnityEngine.Object.DestroyImmediate(menu.gameObject);
            Assert(Mathf.Approximately(source.volume, TestValues.ExternalBaseVolume), "オプション破棄時に外部AudioSourceの音量を復元");
            ValidateExternalEventSystem();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OptionSceneSetup.PrefabPath);
            var replacement = UnityEngine.Object.Instantiate(prefab);
            Assert(Mathf.Approximately(source.volume, TestValues.MutedVolume), "オプション再生成イベントで既存音源を再接続");
            UnityEngine.Object.DestroyImmediate(replacement);
            Assert(Mathf.Approximately(source.volume, TestValues.ExternalBaseVolume), "再生成後の破棄でも外部音量を復元");
            UnityEngine.Object.DestroyImmediate(root);
        }

        private static void CreatePrefabFixture()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OptionSceneSetup.PrefabPath);
            if (prefab == null) throw new InvalidOperationException("共通Prefabが見つかりません。");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            PrefabUtility.InstantiatePrefab(prefab, scene);
            // 未使用の旧モジュールも含め、配置先の設定をオプションが書き換えないことを検証する。
            var system = new GameObject("Scene EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            system.GetComponent<StandaloneInputModule>().enabled = false;
            var path = OptionSceneSetup.Root + "/Scenes/Scene_OptionPrefabValidation_" + Guid.NewGuid().ToString("N") + ".unity";
            EditorSceneManager.SaveScene(scene, path);
            SessionState.SetString(FixtureKey, path);
        }

        private static void LoadFixture()
        {
            EditorSceneManager.LoadSceneInPlayMode(SessionState.GetString(FixtureKey, ""), new LoadSceneParameters(LoadSceneMode.Single));
        }

        private static void QueueKeys(params Key[] keys) => _pendingKeys = keys;

        private static void FeedKeys()
        {
            // Editorの入力更新ではなく、ゲームのDynamic更新へイベントを投入する。
            if (_pendingKeys == null || InputState.currentUpdateType != InputUpdateType.Dynamic) return;
            InputSystem.QueueStateEvent(_keyboard, new KeyboardState(_pendingKeys));
            _pendingKeys = null;
        }

        private static void Capture(string path)
        {
            // バッチEditorにGame Viewがなくても、uGUIを含む画面を描画する。
            var canvas = InGameOptionMenu.Instance.GetComponentInChildren<Canvas>();
            var state = new CaptureState(canvas, Camera.main);
            var target = RenderTexture.GetTemporary(OptionMenuLayout.ReferenceWidth, OptionMenuLayout.ReferenceHeight, CaptureDepthBits, RenderTextureFormat.ARGB32);
            var image = new Texture2D(OptionMenuLayout.ReferenceWidth, OptionMenuLayout.ReferenceHeight, TextureFormat.RGB24, false);
            try
            {
                state.WriteImage(target, image, path);
            }
            finally
            {
                state.Restore();
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(image);
            }
        }

        private sealed class CaptureState
        {
            private readonly Canvas _canvas;
            private readonly CanvasScaler _scaler;
            private readonly Camera _camera;
            private readonly RenderMode _mode;
            private readonly Camera _worldCamera;
            private readonly CanvasScaler.ScaleMode _scaleMode;
            private readonly float _scale;
            private readonly float _distance;
            private readonly RenderTexture _target;
            private readonly RenderTexture _active;

            public CaptureState(Canvas canvas, Camera camera)
            {
                _canvas = canvas;
                _scaler = canvas.GetComponent<CanvasScaler>();
                _camera = camera;
                _mode = canvas.renderMode;
                _worldCamera = canvas.worldCamera;
                _scaleMode = _scaler.uiScaleMode;
                _scale = _scaler.scaleFactor;
                _distance = canvas.planeDistance;
                _target = camera.targetTexture;
                _active = RenderTexture.active;
            }

            public void WriteImage(RenderTexture target, Texture2D image, string path)
            {
                _camera.targetTexture = target;
                _canvas.renderMode = RenderMode.ScreenSpaceCamera;
                _canvas.worldCamera = _camera;
                _canvas.planeDistance = CapturePlaneDistance;
                _scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                _scaler.scaleFactor = CaptureScaleFactor;
                Canvas.ForceUpdateCanvases();
                RenderPipeline.SubmitRenderRequest(_camera, new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, OptionMenuLayout.ReferenceWidth, OptionMenuLayout.ReferenceHeight), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }

            public void Restore()
            {
                _camera.targetTexture = _target;
                _canvas.renderMode = _mode;
                _canvas.worldCamera = _worldCamera;
                _canvas.planeDistance = _distance;
                _scaler.uiScaleMode = _scaleMode;
                _scaler.scaleFactor = _scale;
                Canvas.ForceUpdateCanvases();
                RenderTexture.active = _active;
            }
        }

        private static void AdvanceStep(double delay = DefaultStepDelaySeconds)
        {
            _step++;
            _afterFrame = Time.frameCount + StepFrameDelay;
            _afterTime = EditorApplication.timeSinceStartup + delay;
        }

        private static void Assert(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException("FAILED: " + label);
            Checks.Add("PASS: " + label);
        }

        private static void Finish(bool success, string message)
        {
            _finishing = true;
            try
            {
                SessionState.SetBool(Success, success);
                InputSystem.onBeforeUpdate -= FeedKeys;
                if (_keyboard != null) InputSystem.RemoveDevice(_keyboard);
                InputSystem.settings.backgroundBehavior = _previousBackground;
                InputSystem.settings.editorInputBehaviorInPlayMode = _previousEditorInput;
                Checks.Add(message);
                Directory.CreateDirectory(Output);
                File.WriteAllLines(Output + "/PlayChecks.txt", Checks);
                // 失敗の詳細をEditor/バッチログへ残す。成否自体はSessionStateと終了コードで通知。
                if (!success) Debug.LogError(message);
            }
            catch (Exception e)
            {
                SessionState.SetBool(Success, false);
                // 結果ファイルを書けない場合も、終了処理の異常を診断できるよう記録する。
                Debug.LogError("オプション検証の終了処理に失敗しました: " + e);
            }
            finally
            {
                EditorApplication.isPlaying = false;
            }
        }
    }
}
