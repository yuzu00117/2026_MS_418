using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace MiraiSozo.Options.Editor
{
    /// <summary>現在のPlay中のSceneで、GameManagerと配置済みCanvasの接続を検査する。</summary>
    public static class OptionCanvasValidation
    {
        private const string ReportPath = "Logs/OptionValidation/ExistingCanvasChecks.txt";
        private const int ReopenCount = 3;
        private static readonly List<string> Checks = new List<string>();

        [MenuItem("Tools/Hirakawa R/Options/Validate Existing Canvas (Play Mode)")]
        public static void Run()
        {
            var manager = GameManager.Instance;
            var menu = InGameOptionMenu.Instance;
            if (!EditorApplication.isPlaying || manager == null || menu == null || manager.IsPaused || menu.IsOpen)
                throw new InvalidOperationException("GameManagerのあるSceneをPlayし、メニューを閉じてから実行してください。");
            Checks.Clear();
            try
            {
                RunChecksWithSettingsRestore(manager, menu);
                Checks.Add("EXISTING_CANVAS_CHECKS_OK");
            }
            catch (Exception error)
            {
                Checks.Add("FAILED: " + error);
                throw;
            }
            finally
            {
                Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
                File.WriteAllLines(ReportPath, Checks);
            }
        }

        private static void RunChecksWithSettingsRestore(GameManager manager, InGameOptionMenu menu)
        {
            var options = GameOptions.Instance;
            int frameRate = options.Values.frameRate;
            string savedSettings = PlayerPrefs.HasKey(GameOptions.SettingsKey)
                ? PlayerPrefs.GetString(GameOptions.SettingsKey) : null;
            try { RunChecks(manager, menu); }
            finally { RestoreSettings(manager, options, frameRate, savedSettings); }
        }

        private static void RunChecks(GameManager manager, InGameOptionMenu menu)
        {
            var canvas = menu.BoundCanvas;
            Assert(canvas != null, "起動時に配置済みCanvasを取得");
            var configured = manager.MenuUI == null ? null : manager.MenuUI.GetComponentInChildren<Canvas>(true);
            if (configured != null) Assert(canvas == configured, "GameManagerのMenu UIと同じCanvasを使用");
            var canvases = GetCanvases();
            ValidateReopening(manager, menu, canvas);
            ValidateNavigation(manager, menu, canvas);
            ValidateSingleListener(menu, canvas);
            manager.ResumePause();
            Assert(canvases.SetEquals(GetCanvases()), "開閉・操作後もCanvasを追加生成しない");
            Assert(menu.BoundCanvas == canvas, "最初に配置したCanvasへの参照を保持");
        }

        private static void ValidateReopening(GameManager manager, InGameOptionMenu menu, Canvas canvas)
        {
            float timeScale = Time.timeScale;
            bool playerEnabled = GameManager.Actions.Player.enabled;
            bool uiEnabled = GameManager.Actions.UI.enabled;
            for (int i = 0; i < ReopenCount; i++)
            {
                manager.Pause();
                Assert(menu.IsOpen && manager.IsPaused && canvas.gameObject.activeInHierarchy, "既存Canvasでメニューを表示 " + i);
                Assert(Time.timeScale == 0f && !GameManager.Actions.Player.enabled && GameManager.Actions.UI.enabled,
                    "ポーズ中の時間・入力切り替え " + i);
                FindButton(canvas, "Resume").onClick.Invoke();
                Assert(!menu.IsOpen && !manager.IsPaused, "ゲームに戻るボタンで再開 " + i);
            }
            Assert(Mathf.Approximately(Time.timeScale, timeScale), "開く前の時間倍率を復元");
            Assert(GameManager.Actions.Player.enabled == playerEnabled && GameManager.Actions.UI.enabled == uiEnabled,
                "開く前のアクションマップ状態を復元");
        }

        private static void ValidateNavigation(GameManager manager, InGameOptionMenu menu, Canvas canvas)
        {
            manager.Pause();
            FindButton(canvas, "Open Options").onClick.Invoke();
            Assert(canvas.transform.Find("Menu Overlay/Options Panel").gameObject.activeSelf, "オプションボタンで設定画面へ移動");
            FindButton(canvas, "Reset Tab").onClick.Invoke();
            Assert(canvas.transform.Find("Menu Overlay/Confirmation").gameObject.activeSelf, "初期化ボタンで確認画面を表示");
            FindButton(canvas, "Confirm Cancel").onClick.Invoke();
            Assert(!canvas.transform.Find("Menu Overlay/Confirmation").gameObject.activeSelf, "キャンセルで設定画面へ戻る");
            menu.HandlePause();
            Assert(menu.IsOpen && manager.IsPaused && canvas.transform.Find("Menu Overlay/Pause Panel").gameObject.activeSelf,
                "戻る操作一回でポーズ画面へ移動しメニューを維持");
            menu.HandlePause();
            Assert(!menu.IsOpen && !manager.IsPaused, "ポーズ画面の戻る操作でゲームを再開");
        }

        private static void ValidateSingleListener(InGameOptionMenu menu, Canvas canvas)
        {
            GameManager.Instance.Pause();
            menu.OpenOptions();
            menu.SelectTab(OptionMenuLayout.SystemTab);
            int index = OptionValues.FrameRates.ToList().IndexOf(GameOptions.Instance.Values.frameRate);
            int expected = OptionValues.FrameRates[(index + 1) % OptionValues.FrameRates.Count];
            var next = FindButton(canvas, "FPS Next");
            int authoredCalls = 0;
            UnityAction authored = () => authoredCalls++;
            next.onClick.AddListener(authored);
            try
            {
                next.onClick.Invoke();
                Assert(GameOptions.Instance.Values.frameRate == expected, "繰り返し開閉してもFPSボタンは一段階だけ変更");
                Assert(authoredCalls == 1, "既存のイベントを保持");
            }
            finally { next.onClick.RemoveListener(authored); }
        }

        private static HashSet<Canvas> GetCanvases() => new HashSet<Canvas>(UnityEngine.Object.FindObjectsByType<Canvas>(
            FindObjectsInactive.Include, FindObjectsSortMode.None));

        private static Button FindButton(Canvas canvas, string name) => canvas.GetComponentsInChildren<Button>(true).First(button => button.name == name);

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            Checks.Add("PASS: " + message);
        }

        private static void RestoreSettings(GameManager manager, GameOptions options, int frameRate, string savedSettings)
        {
            try
            {
                options.Values.frameRate = frameRate;
                options.Apply(OptionSettingsCategory.System);
            }
            finally
            {
                try { manager.ResumePause(); }
                finally
                {
                    // メニューを閉じると保存されるため、元の値の復元は最後に行う。
                    if (savedSettings != null) PlayerPrefs.SetString(GameOptions.SettingsKey, savedSettings);
                    else PlayerPrefs.DeleteKey(GameOptions.SettingsKey);
                    PlayerPrefs.Save();
                }
            }
        }
    }
}
