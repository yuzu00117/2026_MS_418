using UnityEngine;
using UnityEngine.UI;

namespace MiraiSozo.Options
{
    /// <summary>Editorのセットアップでも使用する、編集可能なuGUIの階層。</summary>
    public static class OptionMenuLayout
    {
        public static readonly Color Background = new Color32(13, 23, 35, 255);
        public static readonly Color Surface = new Color32(24, 39, 54, 255);
        public static readonly Color Accent = new Color32(90, 227, 206, 255);
        public static readonly Color Muted = new Color32(154, 177, 193, 255);
        private static Font _font;

        public static Canvas Build(Transform parent)
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var root = new GameObject("Options Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(parent, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.matchWidthOrHeight = 0.5f;

            var hud = Rect("Test HUD", root.transform, Vector2.zero, Vector2.zero);
            Stretch(hud);
            var badge = Panel("Test Badge", hud, new Vector2(28, -28), new Vector2(340, 112), new Color(0.04f, 0.08f, 0.12f, 0.87f));
            TopLeft(badge, new Vector2(28, -28), new Vector2(340, 112));
            Label("Test Title", badge, "OPTION TEST", new Vector2(20, -15), new Vector2(300, 30), 24, Accent);
            Label("Test Subtitle", badge, "設定をゲーム内で確かめる", new Vector2(20, -53), new Vector2(300, 28), 17, Color.white);
            var hint = Label("Test Hint", hud, "ESC  メニューを開く    /    WASD  移動    /    マウス  視点", Vector2.zero, new Vector2(900, 34), 19, Color.white);
            hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(0, 0);
            hint.rectTransform.pivot = new Vector2(0, 0);
            hint.rectTransform.anchoredPosition = new Vector2(30, 28);
            var crosshair = Label("Crosshair", hud, "+", Vector2.zero, new Vector2(32, 32), 28, Accent);
            crosshair.rectTransform.anchorMin = crosshair.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            crosshair.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            crosshair.rectTransform.anchoredPosition = Vector2.zero;
            crosshair.alignment = TextAnchor.MiddleCenter;

            var overlay = Panel("Menu Overlay", root.transform, Vector2.zero, Vector2.zero, new Color(0.01f, 0.03f, 0.05f, 0.72f));
            Stretch(overlay);
            BuildPause(overlay);
            BuildOptions(overlay);
            BuildConfirmation(overlay);
            overlay.gameObject.SetActive(false);
            return canvas;
        }

        private static void BuildPause(Transform overlay)
        {
            var panel = Panel("Pause Panel", overlay, Vector2.zero, new Vector2(510, 584), Background);
            Label("Pause Eyebrow", panel, "PAUSED", new Vector2(40, -28), new Vector2(430, 25), 16, Accent);
            Label("Pause Heading", panel, "メニュー", new Vector2(40, -64), new Vector2(430, 48), 34, Color.white);
            Panel("Pause Divider", panel, new Vector2(40, -133), new Vector2(430, 2), Accent, true);
            Button("Resume", panel, "ゲームに戻る", new Vector2(40, -160), new Vector2(430, 54), true);
            Button("Return Title", panel, "タイトルに戻る", new Vector2(40, -226), new Vector2(430, 48));
            Button("Return Stage", panel, "ステージセレクトに戻る", new Vector2(40, -284), new Vector2(430, 48));
            Button("Open Options", panel, "オプション", new Vector2(40, -342), new Vector2(430, 48));
            Button("Quit", panel, "ゲームを閉じる", new Vector2(40, -400), new Vector2(430, 48));
            Label("Pause Status", panel, "ESC  ゲームに戻る", new Vector2(40, -472), new Vector2(430, 76), 17, Muted);
        }

        private static void BuildOptions(Transform overlay)
        {
            var panel = Panel("Options Panel", overlay, Vector2.zero, new Vector2(1080, 610), Background);
            Label("Options Eyebrow", panel, "GAME SETTINGS", new Vector2(36, -22), new Vector2(620, 24), 15, Accent);
            Label("Options Heading", panel, "オプション", new Vector2(36, -54), new Vector2(600, 48), 32, Color.white);
            Label("Autosave", panel, "変更は自動で保存されます", new Vector2(710, -63), new Vector2(334, 28), 17, Muted);
            string[] tabs = { "システム", "カメラ", "音量", "操作" };
            for (int i = 0; i < tabs.Length; i++)
                Button("Tab " + i, panel, tabs[i], new Vector2(36 + i * 255, -120), new Vector2(243, 46));
            Panel("Options Divider", panel, new Vector2(36, -181), new Vector2(1008, 2), Accent, true);

            for (int i = 0; i < 4; i++)
            {
                var page = Rect("Page " + i, panel, Vector2.zero, Vector2.zero);
                TopLeft(page, new Vector2(36, -202), new Vector2(1008, 298));
                switch (i)
                {
                    case 0:
                        Choice(page, "FPS", "FPS制御", 0, "60 FPS");
                        Slider(page, "Brightness", "明度", 73, -2f, 2f, 0f);
                        Slider(page, "FOV", "視野角", 146, 40f, 100f, 60f);
                        Label("System Note", page, "明度は描画の露出に反映されます。FPS制御では垂直同期を無効にします。", new Vector2(0, -240), new Vector2(1008, 42), 16, Muted);
                        break;
                    case 1:
                        Slider(page, "Sensitivity X", "カメラ感度・横", 0, 0.02f, 1f, 0.2f);
                        Slider(page, "Sensitivity Y", "カメラ感度・縦", 56, 0.02f, 1f, 0.2f);
                        Toggle(page, "Invert X", "カメラ方向・左右反転", 112);
                        Toggle(page, "Invert Y", "カメラ方向・上下反転", 160);
                        Slider(page, "Shake Reduction", "画面揺れ軽減", 216, 0f, 1f, 0f, 760);
                        Button("Preview Shake", page, "揺れを確認", new Vector2(794, -216), new Vector2(214, 40));
                        break;
                    case 2:
                        Slider(page, "Master", "マスター音量", 0, 0f, 1f, 1f);
                        Slider(page, "SE", "SE音量", 63, 0f, 1f, 1f, 760);
                        Slider(page, "BGM", "BGM音量", 126, 0f, 1f, 1f, 760);
                        Slider(page, "Voice", "ボイス音量", 189, 0f, 1f, 1f, 760);
                        Button("Preview SE", page, "SEを試聴", new Vector2(794, -63), new Vector2(214, 40));
                        Button("Preview BGM", page, "BGMを試聴", new Vector2(794, -126), new Vector2(214, 40));
                        Button("Preview Voice", page, "ボイス用テスト音", new Vector2(794, -189), new Vector2(214, 40));
                        Label("Audio Note", page, "試聴ボタンでもう一度押すと停止します。メニューを閉じると試聴を終了します。", new Vector2(0, -253), new Vector2(1008, 32), 16, Muted);
                        break;
                    case 3:
                        Label("Controls Note", page, "変更したい操作を選び、新しいキー・マウスボタンを押してください。ESCでキャンセル。", new Vector2(0, 0), new Vector2(1008, 32), 17, Muted);
                        BuildBindingScroll(page);
                        var aim = Toggle(page, "Aim Assist", "エイムアシスト", 250);
                        aim.interactable = false;
                        Label("Aim Unavailable", page, "対応するゲーム機能が未実装です", new Vector2(500, -250), new Vector2(490, 32), 16, Muted);
                        break;
                }
                page.gameObject.SetActive(i == 0);
            }

            Label("Option Status", panel, "ESC  メニューに戻る", new Vector2(36, -509), new Vector2(1008, 36), 17, Muted);
            Button("Reset Tab", panel, "このタブを初期化", new Vector2(36, -556), new Vector2(260, 36));
            Button("Back", panel, "メニューに戻る", new Vector2(784, -552), new Vector2(260, 42), true);
            panel.gameObject.SetActive(false);
        }

        private static void BuildBindingScroll(Transform parent)
        {
            var viewport = Panel("Binding Viewport", parent, new Vector2(0, -45), new Vector2(1008, 190), new Color(1, 1, 1, 0.025f), true);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = Rect("Binding Content", viewport, Vector2.zero, new Vector2(978, 0));
            TopLeft(content, Vector2.zero, new Vector2(978, 0));
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6;
            layout.childControlWidth = true;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var scroll = viewport.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 28;
            viewport.gameObject.AddComponent<OptionScrollSelection>();

            var track = Panel("Binding Scroll Track", viewport, new Vector2(988, 0), new Vector2(16, 190), Surface, true);
            var handle = Panel("Binding Scroll Handle", track, Vector2.zero, Vector2.zero, Accent);
            Stretch(handle);
            var scrollbar = track.gameObject.AddComponent<Scrollbar>();
            scrollbar.targetGraphic = handle.GetComponent<Image>();
            scrollbar.handleRect = handle;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = scrollbar;
        }

        private static void BuildConfirmation(Transform overlay)
        {
            var blocker = Panel("Confirmation", overlay, Vector2.zero, Vector2.zero, new Color(0, 0, 0, 0.8f));
            Stretch(blocker);
            var dialog = Panel("Dialog", blocker, Vector2.zero, new Vector2(620, 270), Surface);
            Label("Confirm Heading", dialog, "確認", new Vector2(32, -24), new Vector2(556, 40), 27, Accent);
            Label("Confirm Message", dialog, "ゲームを閉じますか？", new Vector2(32, -86), new Vector2(556, 74), 22, Color.white);
            Button("Confirm Cancel", dialog, "キャンセル", new Vector2(32, -194), new Vector2(260, 46));
            Button("Confirm Accept", dialog, "実行", new Vector2(328, -194), new Vector2(260, 46), true);
            blocker.gameObject.SetActive(false);
        }

        private static void Choice(Transform page, string name, string caption, float y, string value)
        {
            Label(name + " Label", page, caption, new Vector2(0, -y), new Vector2(370, 40), 22, Color.white);
            Button(name + " Previous", page, "<", new Vector2(400, -y), new Vector2(48, 40));
            var label = Label(name + " Value", page, value, new Vector2(460, -y), new Vector2(488, 40), 21, Accent);
            label.alignment = TextAnchor.MiddleCenter;
            Button(name + " Next", page, ">", new Vector2(960, -y), new Vector2(48, 40));
        }

        private static void Slider(Transform page, string name, string caption, float y, float min, float max, float value, float width = 1008)
        {
            Label(name + " Label", page, caption, new Vector2(0, -y), new Vector2(350, 40), 21, Color.white);
            var root = Rect(name, page, Vector2.zero, Vector2.zero);
            TopLeft(root, new Vector2(400, -y), new Vector2(width - 520, 40));
            var track = Panel(name + " Track", root, Vector2.zero, Vector2.zero, Surface);
            track.anchorMin = new Vector2(0, 0.42f); track.anchorMax = new Vector2(1, 0.58f);
            track.offsetMin = new Vector2(10, 0); track.offsetMax = new Vector2(-10, 0);
            var fill = Panel(name + " Fill", track, Vector2.zero, Vector2.zero, Accent);
            Stretch(fill);
            var handleArea = Rect(name + " Handle Area", root, Vector2.zero, Vector2.zero);
            Stretch(handleArea); handleArea.offsetMin = new Vector2(10, 0); handleArea.offsetMax = new Vector2(-10, 0);
            var handle = Panel(name + " Handle", handleArea, Vector2.zero, new Vector2(18, 28), Accent);
            handle.sizeDelta = new Vector2(18, -12);
            var slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill; slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.minValue = min; slider.maxValue = max; slider.value = value;
            slider.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            var label = Label(name + " Value", page, "100%", new Vector2(width - 110, -y), new Vector2(110, 40), 20, Accent);
            label.alignment = TextAnchor.MiddleRight;
        }

        private static Toggle Toggle(Transform page, string name, string caption, float y)
        {
            Label(name + " Label", page, caption, new Vector2(0, -y), new Vector2(370, 36), 21, Color.white);
            var box = Panel(name, page, new Vector2(400, -y), new Vector2(36, 36), Surface, true);
            var check = Panel(name + " Check", box, Vector2.zero, Vector2.zero, Accent);
            Stretch(check); check.offsetMin = new Vector2(8, 8); check.offsetMax = new Vector2(-8, -8);
            var toggle = box.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = box.GetComponent<Image>();
            toggle.graphic = check.GetComponent<Image>();
            toggle.isOn = false;
            return toggle;
        }

        public static Button Button(string name, Transform parent, string caption, Vector2 position, Vector2 size, bool primary = false)
        {
            var root = Panel(name, parent, position, size, primary ? Accent : Surface, true);
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = root.GetComponent<Image>();
            var colors = button.colors;
            colors.highlightedColor = new Color(0.7f, 1f, 0.94f);
            colors.selectedColor = new Color(0.7f, 1f, 0.94f);
            colors.pressedColor = new Color(0.45f, 0.72f, 0.7f);
            colors.disabledColor = new Color(0.45f, 0.45f, 0.45f);
            button.colors = colors;
            var text = Label(name + " Text", root, caption, Vector2.zero, Vector2.zero, 20, primary ? Background : Color.white);
            Stretch(text.rectTransform);
            text.alignment = TextAnchor.MiddleCenter;
            return button;
        }

        public static Text Label(string name, Transform parent, string text, Vector2 position, Vector2 size, int fontSize, Color color)
        {
            var rect = Rect(name, parent, Vector2.zero, Vector2.zero);
            TopLeft(rect, position, size);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = _font != null ? _font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = text; label.fontSize = fontSize; label.color = color;
            label.alignment = TextAnchor.MiddleLeft;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
            return label;
        }

        private static RectTransform Panel(string name, Transform parent, Vector2 position, Vector2 size, Color color, bool topLeft = false)
        {
            var rect = Rect(name, parent, position, size);
            if (topLeft) TopLeft(rect, position, size);
            var image = rect.gameObject.AddComponent<Image>(); image.color = color;
            return rect;
        }

        private static RectTransform Rect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size; rect.anchoredPosition = position;
            return rect;
        }

        private static void TopLeft(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.sizeDelta = size; rect.anchoredPosition = position;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
        }
    }
}
