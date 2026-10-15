using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
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
            PlayerPrefs.DeleteKey(GameOptions.SettingsKey);
            PlayerPrefs.DeleteKey(GameOptions.BindingsKey);
            PlayerPrefs.Save();
            SessionState.SetBool(Pending, true);
            SessionState.SetBool(Success, false);
            Directory.CreateDirectory(Output);
            EditorApplication.isPlaying = true;
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
            Restore(GameOptions.SettingsKey);
            Restore(GameOptions.BindingsKey);
            PlayerPrefs.Save();
            SessionState.SetBool(Pending, false);
            var fixturePath = SessionState.GetString(FixtureKey, "");
            if (fixturePath.StartsWith(OptionSceneSetup.Root + "/Scenes/OptionPrefabValidation_", StringComparison.Ordinal))
                AssetDatabase.DeleteAsset(fixturePath);
            SessionState.EraseString(FixtureKey);
            bool success = SessionState.GetBool(Success, false);
            if (Application.isBatchMode) EditorApplication.Exit(success ? 0 : 1);
        }

        private static void Tick()
        {
            if (!SessionState.GetBool(Pending, false) || !EditorApplication.isPlaying || _finishing) return;
            if (_started == 0) _started = EditorApplication.timeSinceStartup;
            if (EditorApplication.timeSinceStartup - _started > 90) { Finish(false, "PlayMode validation timeout"); return; }
            if (Time.frameCount < _afterFrame || EditorApplication.timeSinceStartup < _afterTime) return;
            var menu = InGameOptionMenu.Instance;
            var options = GameOptions.Instance;
            if (menu == null || options == null || Time.frameCount < 5) return;
            try
            {
                switch (_step)
                {
                    case 0:
                        _previousBackground = InputSystem.settings.backgroundBehavior;
                        _previousEditorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
                        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                        _keyboard = InputSystem.AddDevice<Keyboard>("Option validation keyboard");
                        InputSystem.onBeforeUpdate += FeedKeys;
                        Assert(!menu.IsOpen && OptionGameManager.Actions.Player.enabled, "開始時はメニュー非表示・ゲーム入力有効");
                        Assert(menu.GetComponentsInChildren<Canvas>(true).Length == 1, "PrefabからCanvasを重複生成しない");
                        Time.timeScale = 0.4f;
                        OptionGameManager.Actions.UI.Disable();
                        _legacyMenu = new GameObject("Legacy Menu Probe");
                        _legacyMenu.SetActive(false);
                        var manager = new SerializedObject(OptionGameManager.Instance);
                        manager.FindProperty("_menuUI").objectReferenceValue = _legacyMenu;
                        manager.ApplyModifiedPropertiesWithoutUndo();
                        Queue(Key.Escape);
                        Next();
                        break;
                    case 1:
                        Assert(menu.IsOpen && OptionGameManager.Instance.IsPaused && Time.timeScale == 0f, "ESC入力でメニュー表示・ゲーム停止 (open=" + menu.IsOpen + ", paused=" + OptionGameManager.Instance.IsPaused + ", timeScale=" + Time.timeScale + ")");
                        Assert(!OptionGameManager.Actions.Player.enabled && Cursor.lockState == CursorLockMode.None, "ポーズ中はプレイヤー入力停止・カーソル解放");
                        Assert(!_legacyMenu.activeSelf, "既存のポーズUIと新メニューを二重表示しない");
                        Queue();
                        menu.OpenOptions();
                        for (int i = 0; i < 4; i++)
                        {
                            menu.SelectTab(i);
                            Assert(All<Transform>("Page " + i).gameObject.activeInHierarchy, "タブ切り替え " + i);
                        }
                        menu.SelectTab(0);
                        All<Slider>("FOV").value = 82f;
                        All<Slider>("Brightness").value = 1.2f;
                        All<Button>("FPS Next").onClick.Invoke();
                        Assert(Mathf.Approximately(Camera.main.fieldOfView, 82f), "視野角をメインカメラへ反映");
                        Assert(Application.targetFrameRate == 120 && QualitySettings.vSyncCount == 0, "FPSと垂直同期を反映");
                        var volume = UnityEngine.Object.FindObjectsByType<Volume>().Single(v => v.name == "Option Brightness (Exposure)");
                        Assert(volume.sharedProfile.TryGet(out ColorAdjustments exposure) && Mathf.Approximately(exposure.postExposure.value, 1.2f), "明度をURP露出へ反映");
                        menu.SelectTab(1);
                        All<Slider>("Sensitivity X").value = 0.64f;
                        All<Slider>("Sensitivity Y").value = 0.38f;
                        All<Toggle>("Invert X").isOn = true;
                        All<Toggle>("Invert Y").isOn = true;
                        var cameraBehavior = Camera.main.GetComponent<OptionCameraBehavior>();
                        Assert(Mathf.Approximately(cameraBehavior.SensitivityX, 0.64f) && Mathf.Approximately(cameraBehavior.SensitivityY, 0.38f) && cameraBehavior.InvertX && cameraBehavior.InvertY, "感度・左右上下反転をCameraBehaviorへ反映");
                        menu.SelectTab(2);
                        All<Slider>("Master").value = 0.35f;
                        All<Slider>("SE").value = 0.25f;
                        All<Slider>("BGM").value = 0.45f;
                        All<Slider>("Voice").value = 0.65f;
                        Assert(Mathf.Approximately(AudioListener.volume, 0.35f), "マスター音量の反映");
                        foreach (var channel in menu.GetComponentsInChildren<OptionAudioChannel>())
                        {
                            float expected = channel.Category == OptionAudioCategory.SE ? 0.25f : channel.Category == OptionAudioCategory.BGM ? 0.45f : 0.65f;
                            Assert(Mathf.Approximately(channel.GetComponent<AudioSource>().volume, channel.BaseVolume * expected), "カテゴリー音量の独立反映 " + channel.Category);
                        }
                        options.Save();
                        var saved = JsonUtility.FromJson<OptionValues>(PlayerPrefs.GetString(GameOptions.SettingsKey));
                        Assert(Mathf.Approximately(saved.sensitivityX, 0.64f) && Mathf.Approximately(saved.masterVolume, 0.35f), "変更値をPlayerPrefsへ保存");
                        options.ResetTab(2);
                        Assert(Mathf.Approximately(options.Values.masterVolume, 1f) && Mathf.Approximately(options.Values.sensitivityX, 0.64f), "タブ初期化が他カテゴリーを保持");
                        menu.SelectTab(3);
                        Assert(!All<Toggle>("Aim Assist").interactable, "未対応エイムアシストを明示");
                        var entries = OptionRebinding.Entries(OptionGameManager.Actions.Player.Get());
                        Assert(entries.Count >= 15 && entries.Count == All<Transform>("Binding Content").Cast<Transform>().Count(child => child.gameObject.activeSelf), "キー設定一覧に移動の複合バインドとゲーム操作を表示");
                        _jump = entries.First(e => e.Action.name == "Jump");
                        Next();
                        break;
                    case 2:
                        menu.BeginRebind(_jump);
                        Assert(menu.IsRebinding, "Input Systemのリバインド開始");
                        Queue(Key.J);
                        Next(0.7);
                        break;
                    case 3:
                        Assert(!menu.IsRebinding && _jump.Action.bindings[_jump.Index].effectivePath == "<Keyboard>/j", "実際のキー入力でジャンプをJに変更 (rebinding=" + menu.IsRebinding + ", path=" + _jump.Action.bindings[_jump.Index].effectivePath + ", status=" + All<Text>("Option Status").text + ")");
                        Assert(!OptionGameManager.Actions.Player.enabled, "キー変更後もゲーム入力を停止");
                        using (var actions = new InputSystem_Actions())
                        {
                            actions.asset.LoadBindingOverridesFromJson(PlayerPrefs.GetString(GameOptions.BindingsKey));
                            Assert(actions.Player.Jump.bindings.Any(b => b.effectivePath == "<Keyboard>/j"), "キー変更を別のInputActionAssetへ復元");
                        }
                        Assert(!OptionRebinding.TryApply(_jump, "<Keyboard>/w", out _), "他の操作と重複するキーを拒否");
                        Assert(!OptionRebinding.TryApply(_jump, "<Keyboard>/escape", out _), "ESCをゲーム操作へ割り当てない");
                        Queue();
                        menu.BeginRebind(_jump);
                        Next();
                        break;
                    case 4:
                        Queue(Key.Escape);
                        Next();
                        break;
                    case 5:
                        Assert(!menu.IsRebinding && menu.IsOpen && All<Transform>("Options Panel").gameObject.activeInHierarchy, "リバインド中のESCはキャンセルだけを実行");
                        Assert(_jump.Action.bindings[_jump.Index].effectivePath == "<Keyboard>/j", "キャンセルは割り当てを保持");
                        Queue();
                        menu.SelectTab(1);
                        Capture(Output + "/options-camera.png");
                        Next(0.5);
                        break;
                    case 6:
                        menu.SelectTab(0);
                        Capture(Output + "/options-system.png");
                        Next(0.5);
                        break;
                    case 7:
                        menu.SelectTab(2);
                        Capture(Output + "/options-audio.png");
                        Next(0.5);
                        break;
                    case 8:
                        menu.SelectTab(3);
                        Capture(Output + "/options-controls.png");
                        Next(0.5);
                        break;
                    case 9:
                        Queue(Key.Escape);
                        Next();
                        break;
                    case 10:
                        Assert(menu.IsOpen && All<Transform>("Pause Panel").gameObject.activeInHierarchy, "オプション中のESCでメニューへ戻る");
                        Queue();
                        string scene = SceneManager.GetActiveScene().path;
                        All<Button>("Return Title").onClick.Invoke();
                        All<Button>("Return Stage").onClick.Invoke();
                        Assert(SceneManager.GetActiveScene().path == scene, "タイトル・ステージセレクトはシーン遷移しない");
                        All<Button>("Quit").onClick.Invoke();
                        Assert(All<Transform>("Confirmation").gameObject.activeInHierarchy, "終了ボタンは確認画面を表示");
                        All<Button>("Confirm Cancel").onClick.Invoke();
                        Assert(!All<Transform>("Confirmation").gameObject.activeSelf && menu.IsOpen, "終了キャンセルでゲームを保持");
                        Capture(Output + "/pause-menu.png");
                        Next(0.5);
                        break;
                    case 11:
                        Queue(Key.Escape);
                        Next();
                        break;
                    case 12:
                        Assert(!menu.IsOpen && !OptionGameManager.Instance.IsPaused && Mathf.Approximately(Time.timeScale, 0.4f), "ESCで閉じて元の時間倍率を復元");
                        Assert(OptionGameManager.Actions.Player.enabled && !OptionGameManager.Actions.UI.enabled, "元のアクションマップ状態を復元");
                        Queue();
                        OptionGameManager.Actions.Player.Disable();
                        menu.OpenMenu();
                        menu.CloseMenu();
                        Assert(!OptionGameManager.Actions.Player.enabled, "もともと停止していたプレイヤー入力は停止状態に復元");
                        OptionGameManager.Actions.Player.Enable();
                        Time.timeScale = 1f;
                        var malformed = new OptionValues { frameRate = 17, fieldOfView = float.NaN, sensitivityX = -4, masterVolume = 8 };
                        malformed.Sanitize();
                        Assert(malformed.frameRate == 60 && malformed.fieldOfView == 60 && malformed.sensitivityX == 0.02f && malformed.masterVolume == 1f, "範囲外・非数の保存値を補正");
                        UnityEngine.Object.DestroyImmediate(menu.gameObject);
                        LoadFixture();
                        Next(0.5);
                        break;
                    case 13:
                        _portableRoot = menu.gameObject;
                        Assert(SceneManager.GetActiveScene().path == SessionState.GetString(FixtureKey, ""), "別Sceneに共通Prefabを配置して起動");
                        Assert(!menu.IsOpen && !All<Transform>("Test HUD").gameObject.activeSelf, "共通PrefabはメニューとテストHUDが初期非表示");
                        Assert(OptionGameManager.Instance.transform.IsChildOf(menu.transform), "自動生成したゲーム管理もPrefab内に保持");
                        Assert(UnityEngine.Object.FindObjectsByType<EventSystem>().Length == 1 && !EventSystem.current.transform.IsChildOf(menu.transform), "既存のEventSystemを再利用");
                        Assert(Mathf.Approximately(Camera.main.fieldOfView, options.Values.fieldOfView), "別SceneのMainCameraへ保存した視野角を反映");
                        Assert(OptionGameManager.Actions.Player.Jump.bindings.Any(b => b.effectivePath == "<Keyboard>/j"), "別Sceneでも保存したキー配置を復元");
                        Queue(Key.Escape);
                        Next();
                        break;
                    case 14:
                        Assert(menu.IsOpen && Time.timeScale == 0f, "別SceneでもESCでメニューを開く");
                        Queue();
                        menu.CloseMenu();
                        EditorSceneManager.LoadSceneInPlayMode(OptionSceneSetup.ScenePath, new LoadSceneParameters(LoadSceneMode.Single));
                        Next(0.5);
                        break;
                    case 15:
                        Assert(menu.gameObject == _portableRoot && UnityEngine.Object.FindObjectsByType<InGameOptionMenu>().Length == 1, "SceneごとのPrefab配置でもメニューを重複させない");
                        Assert(UnityEngine.Object.FindObjectsByType<EventSystem>().Length == 1 && EventSystem.current.transform.IsChildOf(menu.transform), "EventSystemのない移動先では自動生成");
                        Assert(!All<Transform>("Test HUD").gameObject.activeSelf, "Scene移動後も共通PrefabのHUD設定を保持");
                        Queue(Key.Escape);
                        Next();
                        break;
                    case 16:
                        Assert(menu.IsOpen && Time.timeScale == 0f, "移動先でもESCメニューが機能");
                        Queue();
                        LoadFixture();
                        Next(0.5);
                        break;
                    case 17:
                        Assert(!menu.IsOpen && !OptionGameManager.Instance.IsPaused && Mathf.Approximately(Time.timeScale, 1f), "メニュー表示中のScene移動でポーズを解除");
                        Assert(UnityEngine.Object.FindObjectsByType<EventSystem>().Length == 1 && !EventSystem.current.transform.IsChildOf(menu.transform), "再移動先のEventSystemに切り替えて重複を防止");
                        Assert(menu.gameObject == _portableRoot && UnityEngine.Object.FindObjectsByType<InGameOptionMenu>().Length == 1, "往復後もPrefabを1つだけ保持");
                        Finish(true, "OPTION_PLAY_CHECKS_OK");
                        break;
                }
            }
            catch (Exception e) { Finish(false, e.ToString()); }
        }

        private static T All<T>(string name) where T : Component
        {
            return InGameOptionMenu.Instance.GetComponentsInChildren<T>(true).First(v => v.name == name);
        }

        private static void CreatePrefabFixture()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(OptionSceneSetup.PrefabPath);
            if (prefab == null) throw new InvalidOperationException("共通Prefabが見つかりません。");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            PrefabUtility.InstantiatePrefab(prefab, scene);
            var system = new GameObject("Scene EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            system.GetComponent<InputSystemUIInputModule>().AssignDefaultActions();
            var path = OptionSceneSetup.Root + "/Scenes/OptionPrefabValidation_" + Guid.NewGuid().ToString("N") + ".unity";
            EditorSceneManager.SaveScene(scene, path);
            SessionState.SetString(FixtureKey, path);
        }

        private static void LoadFixture()
        {
            EditorSceneManager.LoadSceneInPlayMode(SessionState.GetString(FixtureKey, ""), new LoadSceneParameters(LoadSceneMode.Single));
        }

        private static void Queue(params Key[] keys) => _pendingKeys = keys;

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
            var scaler = canvas.GetComponent<CanvasScaler>();
            var camera = Camera.main;
            var previousMode = canvas.renderMode;
            var previousCamera = canvas.worldCamera;
            var previousScaleMode = scaler.uiScaleMode;
            float previousScale = scaler.scaleFactor;
            float previousDistance = canvas.planeDistance;
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            var target = RenderTexture.GetTemporary(1280, 720, 24, RenderTextureFormat.ARGB32);
            var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            try
            {
                camera.targetTexture = target;
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 1f;
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.scaleFactor = 1f;
                Canvas.ForceUpdateCanvases();
                RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                camera.targetTexture = previousTarget;
                canvas.renderMode = previousMode;
                canvas.worldCamera = previousCamera;
                canvas.planeDistance = previousDistance;
                scaler.uiScaleMode = previousScaleMode;
                scaler.scaleFactor = previousScale;
                Canvas.ForceUpdateCanvases();
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.DestroyImmediate(image);
            }
        }
        private static void Next(double delay = 0.25)
        {
            _step++;
            _afterFrame = Time.frameCount + 3;
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
            SessionState.SetBool(Success, success);
            InputSystem.onBeforeUpdate -= FeedKeys;
            if (_keyboard != null) InputSystem.RemoveDevice(_keyboard);
            InputSystem.settings.backgroundBehavior = _previousBackground;
            InputSystem.settings.editorInputBehaviorInPlayMode = _previousEditorInput;
            Checks.Add(message);
            Directory.CreateDirectory(Output);
            File.WriteAllLines(Output + "/PlayChecks.txt", Checks);
            if (success) Debug.Log(message + " (" + Checks.Count + " checks)");
            else Debug.LogError(message);
            EditorApplication.isPlaying = false;
        }
    }
}
