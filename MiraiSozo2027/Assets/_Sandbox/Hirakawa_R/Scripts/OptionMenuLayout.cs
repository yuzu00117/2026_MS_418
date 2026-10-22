using UnityEngine;
using UnityEngine.UI;

namespace MiraiSozo.Options
{
    /// <summary>Editorのセットアップでも使用する、編集可能なuGUIの階層。</summary>
    public static class OptionMenuLayout
    {
        public const int TabCount = ControlsTab + 1;
        public const int SystemTab = 0;
        public const int CameraTab = 1;
        public const int AudioTab = 2;
        public const int ControlsTab = 3;
        public const int AudioChannelCount = 3;
        public const float BindingWidth = 978f;
        public const float BindingHeight = 42f;
        public const int BindingFontSize = 18;
        public const int ReferenceWidth = 1280;
        public const int ReferenceHeight = 720;
        public static readonly Color Background = new Color32(13, 23, 35, 255);
        public static readonly Color Surface = new Color32(24, 39, 54, 255);
        public static readonly Color Accent = new Color32(90, 227, 206, 255);
        public static readonly Color Muted = new Color32(154, 177, 193, 255);
        private static Font _font;

        // 位置・サイズは1280x720基準。生成処理から分離し、用途ごとに調整する。
        private static class CanvasStyle
        {
            public const int SortingOrder = 100;
            public const float CenterAnchor = 0.5f;
            public const float ScaleMatch = 0.5f;
            public static readonly Color OverlayColor = new Color(0.01f, 0.03f, 0.05f, 0.72f);
            public static readonly Vector2 Center = Vector2.one * CenterAnchor;
        }

        private static class HudLayout
        {
            public static readonly Vector2 BadgePosition = new Vector2(28, -28);
            public static readonly Vector2 BadgeSize = new Vector2(340, 112);
            public static readonly Color BadgeColor = new Color(0.04f, 0.08f, 0.12f, 0.87f);
            public static readonly Vector2 TitlePosition = new Vector2(20, -15);
            public static readonly Vector2 TitleSize = new Vector2(300, 30);
            public const int TitleFontSize = 24;
            public static readonly Vector2 SubtitlePosition = new Vector2(20, -53);
            public static readonly Vector2 SubtitleSize = new Vector2(300, 28);
            public const int SubtitleFontSize = 17;
            public static readonly Vector2 HintSize = new Vector2(900, 34);
            public static readonly Vector2 HintPosition = new Vector2(30, 28);
            public const int HintFontSize = 19;
            public static readonly Vector2 CrosshairSize = new Vector2(32, 32);
            public const int CrosshairFontSize = 28;
        }

        private static class PauseLayout
        {
            public static readonly Vector2 PanelSize = new Vector2(510, 584);
            public static readonly Vector2 EyebrowPosition = new Vector2(40, -28);
            public static readonly Vector2 EyebrowSize = new Vector2(430, 25);
            public const int EyebrowFontSize = 16;
            public static readonly Vector2 HeadingPosition = new Vector2(40, -64);
            public static readonly Vector2 HeadingSize = new Vector2(430, 48);
            public const int HeadingFontSize = 34;
            public static readonly Vector2 DividerPosition = new Vector2(40, -133);
            public static readonly Vector2 DividerSize = new Vector2(430, 2);
            public static readonly Vector2 ResumePosition = new Vector2(40, -160);
            public static readonly Vector2 ResumeSize = new Vector2(430, 54);
            public static readonly Vector2 TitlePosition = new Vector2(40, -226);
            public static readonly Vector2 StagePosition = new Vector2(40, -284);
            public static readonly Vector2 OptionsPosition = new Vector2(40, -342);
            public static readonly Vector2 QuitPosition = new Vector2(40, -400);
            public static readonly Vector2 ButtonSize = new Vector2(430, 48);
            public static readonly Vector2 StatusPosition = new Vector2(40, -472);
            public static readonly Vector2 StatusSize = new Vector2(430, 76);
            public const int StatusFontSize = 17;
        }

        private static class OptionsLayout
        {
            public const float PageWidth = 1008f;
            public static readonly string[] TabCaptions = { "システム", "カメラ", "音量", "操作" };
            public static readonly Vector2 PanelSize = new Vector2(1080, 610);
            public static readonly Vector2 EyebrowPosition = new Vector2(36, -22);
            public static readonly Vector2 EyebrowSize = new Vector2(620, 24);
            public const int EyebrowFontSize = 15;
            public static readonly Vector2 HeadingPosition = new Vector2(36, -54);
            public static readonly Vector2 HeadingSize = new Vector2(600, 48);
            public const int HeadingFontSize = 32;
            public static readonly Vector2 AutosavePosition = new Vector2(710, -63);
            public static readonly Vector2 AutosaveSize = new Vector2(334, 28);
            public const int AutosaveFontSize = 17;
            public static readonly Vector2 TabOrigin = new Vector2(36, -120);
            public const float TabStride = 255f;
            public static readonly Vector2 TabSize = new Vector2(243, 46);
            public static readonly Vector2 DividerPosition = new Vector2(36, -181);
            public static readonly Vector2 DividerSize = new Vector2(PageWidth, 2);
            public static readonly Vector2 PagePosition = new Vector2(36, -202);
            public static readonly Vector2 PageSize = new Vector2(PageWidth, 298);
            public static readonly Vector2 StatusPosition = new Vector2(36, -509);
            public static readonly Vector2 StatusSize = new Vector2(PageWidth, 36);
            public const int StatusFontSize = 17;
            public static readonly Vector2 ResetPosition = new Vector2(36, -556);
            public static readonly Vector2 ResetSize = new Vector2(260, 36);
            public static readonly Vector2 BackPosition = new Vector2(784, -552);
            public static readonly Vector2 BackSize = new Vector2(260, 42);
        }

        private static class PageLayout
        {
            public const float SystemRowStride = 73f;
            public const float SystemFieldOfViewOffset = SystemRowStride * 2;
            public static readonly Vector2 SystemNotePosition = new Vector2(0, -240);
            public static readonly Vector2 SystemNoteSize = new Vector2(OptionsLayout.PageWidth, 42);
            public const int NoteFontSize = 16;
            public const float SensitivityYOffset = 56f;
            public const float InvertXOffset = 112f;
            public const float InvertYOffset = 160f;
            public const float ShakeOffset = 216f;
            public const float PreviewRowWidth = 760f;
            public const float PreviewButtonX = 794f;
            public static readonly Vector2 PreviewButtonSize = new Vector2(214, 40);
            public const float AudioRowStride = 63f;
            public const float SEOffset = AudioRowStride;
            public const float BGMOffset = AudioRowStride * 2;
            public const float VoiceOffset = AudioRowStride * 3;
            public static readonly Vector2 AudioNotePosition = new Vector2(0, -253);
            public static readonly Vector2 AudioNoteSize = new Vector2(OptionsLayout.PageWidth, 32);
            public static readonly Vector2 ControlsNoteSize = new Vector2(OptionsLayout.PageWidth, 32);
            public const int ControlsNoteFontSize = 17;
            public const float AimOffset = 250f;
            public static readonly Vector2 AimNotePosition = new Vector2(500, -AimOffset);
            public static readonly Vector2 AimNoteSize = new Vector2(490, 32);
        }

        private static class BindingLayout
        {
            public static readonly Vector2 ViewportPosition = new Vector2(0, -45);
            public const float ViewportHeight = 190f;
            public static readonly Vector2 ViewportSize = new Vector2(OptionsLayout.PageWidth, ViewportHeight);
            public static readonly Color ViewportColor = new Color(1, 1, 1, 0.025f);
            public const float RowSpacing = 6f;
            public const float ScrollSensitivity = 28f;
            public static readonly Vector2 TrackPosition = new Vector2(988, 0);
            public static readonly Vector2 TrackSize = new Vector2(16, ViewportHeight);
        }

        private static class ConfirmationLayout
        {
            public static readonly Color BlockerColor = new Color(0, 0, 0, 0.8f);
            public static readonly Vector2 DialogSize = new Vector2(620, 270);
            public static readonly Vector2 HeadingPosition = new Vector2(32, -24);
            public static readonly Vector2 HeadingSize = new Vector2(556, 40);
            public const int HeadingFontSize = 27;
            public static readonly Vector2 MessagePosition = new Vector2(32, -86);
            public static readonly Vector2 MessageSize = new Vector2(556, 74);
            public const int MessageFontSize = 22;
            public static readonly Vector2 CancelPosition = new Vector2(32, -194);
            public static readonly Vector2 AcceptPosition = new Vector2(328, -194);
            public static readonly Vector2 ButtonSize = new Vector2(260, 46);
        }

        private static class ControlLayout
        {
            public const float RowHeight = 40f;
            public const float ControlLeft = 400f;
            public static readonly Vector2 CaptionSize = new Vector2(370, RowHeight);
            public const int CaptionFontSize = 22;
            public static readonly Vector2 ChoiceButtonSize = new Vector2(48, RowHeight);
            public const float ChoiceValueX = 460f;
            public static readonly Vector2 ChoiceValueSize = new Vector2(488, RowHeight);
            public const int ChoiceValueFontSize = 21;
            public const float ChoiceNextX = 960f;
            public static readonly Vector2 SliderCaptionSize = new Vector2(350, RowHeight);
            public const int SliderCaptionFontSize = 21;
            public const float SliderReservedWidth = 520f;
            public const float TrackAnchorMin = 0.42f;
            public const float TrackAnchorMax = 0.58f;
            public const float HandleInset = 10f;
            public static readonly Vector2 SliderHandleSize = new Vector2(18, 28);
            public static readonly Vector2 SliderHandleDelta = new Vector2(18, -12);
            public const float ValueWidth = 110f;
            public static readonly Vector2 SliderValueSize = new Vector2(ValueWidth, RowHeight);
            public const int ValueFontSize = 20;
            public static readonly Vector2 ToggleCaptionSize = new Vector2(370, 36);
            public static readonly Vector2 ToggleSize = new Vector2(36, 36);
            public static readonly Vector2 ToggleInset = new Vector2(8, 8);
            public const int ButtonFontSize = 20;
            public static readonly Color HighlightColor = new Color(0.7f, 1f, 0.94f);
            public static readonly Color PressedColor = new Color(0.45f, 0.72f, 0.7f);
            public static readonly Color DisabledColor = new Color(0.45f, 0.45f, 0.45f);
        }

        public static Canvas Build(Transform parent)
        {
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvas = CreateCanvas(parent);
            BuildTestHud(canvas.transform);
            var overlay = CreatePanel("Menu Overlay", canvas.transform, Vector2.zero, Vector2.zero, CanvasStyle.OverlayColor);
            Stretch(overlay);
            BuildPause(overlay);
            BuildOptions(overlay);
            BuildConfirmation(overlay);
            overlay.gameObject.SetActive(false);
            return canvas;
        }

        private static Canvas CreateCanvas(Transform parent)
        {
            var root = new GameObject("Options Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            root.transform.SetParent(parent, false);
            var canvas = root.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = CanvasStyle.SortingOrder;
            var scaler = root.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.matchWidthOrHeight = CanvasStyle.ScaleMatch;
            return canvas;
        }

        private static void BuildTestHud(Transform parent)
        {
            var hud = CreateRect("Test HUD", parent, Vector2.zero, Vector2.zero);
            Stretch(hud);
            var badge = CreatePanel("Test Badge", hud, HudLayout.BadgePosition, HudLayout.BadgeSize, HudLayout.BadgeColor);
            SetTopLeft(badge, HudLayout.BadgePosition, HudLayout.BadgeSize);
            CreateLabel("Test Title", badge, "オプション確認", HudLayout.TitlePosition, HudLayout.TitleSize, HudLayout.TitleFontSize, Accent);
            CreateLabel("Test Subtitle", badge, "設定をゲーム内で確かめる", HudLayout.SubtitlePosition, HudLayout.SubtitleSize, HudLayout.SubtitleFontSize, Color.white);
            var hint = CreateLabel("Test Hint", hud, "ESC  メニューを開く    ／    WASD  移動    ／    マウス  視点", Vector2.zero, HudLayout.HintSize, HudLayout.HintFontSize, Color.white);
            hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(0, 0);
            hint.rectTransform.pivot = new Vector2(0, 0);
            hint.rectTransform.anchoredPosition = HudLayout.HintPosition;
            var crosshair = CreateLabel("Crosshair", hud, "+", Vector2.zero, HudLayout.CrosshairSize, HudLayout.CrosshairFontSize, Accent);
            crosshair.rectTransform.anchorMin = crosshair.rectTransform.anchorMax = CanvasStyle.Center;
            crosshair.rectTransform.pivot = CanvasStyle.Center;
            crosshair.rectTransform.anchoredPosition = Vector2.zero;
            crosshair.alignment = TextAnchor.MiddleCenter;
        }

        private static void BuildPause(Transform overlay)
        {
            var panel = CreatePanel("Pause Panel", overlay, Vector2.zero, PauseLayout.PanelSize, Background);
            CreateLabel("Pause Eyebrow", panel, "一時停止", PauseLayout.EyebrowPosition, PauseLayout.EyebrowSize, PauseLayout.EyebrowFontSize, Accent);
            CreateLabel("Pause Heading", panel, "メニュー", PauseLayout.HeadingPosition, PauseLayout.HeadingSize, PauseLayout.HeadingFontSize, Color.white);
            CreatePanel("Pause Divider", panel, PauseLayout.DividerPosition, PauseLayout.DividerSize, Accent, true);
            CreateButton("Resume", panel, "ゲームに戻る", PauseLayout.ResumePosition, PauseLayout.ResumeSize, true);
            CreateButton("Return Title", panel, "タイトルに戻る", PauseLayout.TitlePosition, PauseLayout.ButtonSize);
            CreateButton("Return Stage", panel, "ステージセレクトに戻る", PauseLayout.StagePosition, PauseLayout.ButtonSize);
            CreateButton("Open Options", panel, "オプション", PauseLayout.OptionsPosition, PauseLayout.ButtonSize);
            CreateButton("Quit", panel, "ゲームを閉じる", PauseLayout.QuitPosition, PauseLayout.ButtonSize);
            CreateLabel("Pause Status", panel, "ESC  ゲームに戻る", PauseLayout.StatusPosition, PauseLayout.StatusSize, PauseLayout.StatusFontSize, Muted);
        }

        private static void BuildOptions(Transform overlay)
        {
            var panel = CreatePanel("Options Panel", overlay, Vector2.zero, OptionsLayout.PanelSize, Background);
            CreateLabel("Options Eyebrow", panel, "ゲーム設定", OptionsLayout.EyebrowPosition, OptionsLayout.EyebrowSize, OptionsLayout.EyebrowFontSize, Accent);
            CreateLabel("Options Heading", panel, "オプション", OptionsLayout.HeadingPosition, OptionsLayout.HeadingSize, OptionsLayout.HeadingFontSize, Color.white);
            CreateLabel("Autosave", panel, "変更は自動で保存されます", OptionsLayout.AutosavePosition, OptionsLayout.AutosaveSize, OptionsLayout.AutosaveFontSize, Muted);
            for (int i = 0; i < TabCount; i++)
                CreateButton("Tab " + i, panel, OptionsLayout.TabCaptions[i], OptionsLayout.TabOrigin + Vector2.right * (i * OptionsLayout.TabStride), OptionsLayout.TabSize);
            CreatePanel("Options Divider", panel, OptionsLayout.DividerPosition, OptionsLayout.DividerSize, Accent, true);

            for (int i = 0; i < TabCount; i++)
            {
                var page = CreateRect("Page " + i, panel, Vector2.zero, Vector2.zero);
                SetTopLeft(page, OptionsLayout.PagePosition, OptionsLayout.PageSize);
                BuildTab(page, i);
                page.gameObject.SetActive(i == SystemTab);
            }

            CreateLabel("Option Status", panel, "ESC  メニューに戻る", OptionsLayout.StatusPosition, OptionsLayout.StatusSize, OptionsLayout.StatusFontSize, Muted);
            CreateButton("Reset Tab", panel, "このタブを初期化", OptionsLayout.ResetPosition, OptionsLayout.ResetSize);
            CreateButton("Back", panel, "メニューに戻る", OptionsLayout.BackPosition, OptionsLayout.BackSize, true);
            panel.gameObject.SetActive(false);
        }

        private static void BuildTab(Transform page, int tab)
        {
            switch (tab)
            {
                case SystemTab: BuildSystemTab(page); break;
                case CameraTab: BuildCameraTab(page); break;
                case AudioTab: BuildAudioTab(page); break;
                case ControlsTab: BuildControlsTab(page); break;
            }
        }

        private static void BuildSystemTab(Transform page)
        {
            CreateChoice(page, "FPS", "FPS制御", 0, OptionValues.DefaultFrameRate + " FPS");
            CreateSlider(page, "Brightness", "明度", PageLayout.SystemRowStride, OptionValues.MinimumBrightness, OptionValues.MaximumBrightness, OptionValues.DefaultBrightness);
            CreateSlider(page, "FOV", "視野角", PageLayout.SystemFieldOfViewOffset, OptionValues.MinimumFieldOfView, OptionValues.MaximumFieldOfView, OptionValues.DefaultFieldOfView);
            CreateLabel("System Note", page, "明度は描画の露出に反映されます。FPS制御では垂直同期を無効にします。", PageLayout.SystemNotePosition, PageLayout.SystemNoteSize, PageLayout.NoteFontSize, Muted);
        }

        private static void BuildCameraTab(Transform page)
        {
            CreateSlider(page, "Sensitivity X", "カメラ感度・横", 0, OptionValues.MinimumSensitivity, OptionValues.MaximumSensitivity, OptionValues.DefaultSensitivity);
            CreateSlider(page, "Sensitivity Y", "カメラ感度・縦", PageLayout.SensitivityYOffset, OptionValues.MinimumSensitivity, OptionValues.MaximumSensitivity, OptionValues.DefaultSensitivity);
            CreateToggle(page, "Invert X", "カメラ方向・左右反転", PageLayout.InvertXOffset);
            CreateToggle(page, "Invert Y", "カメラ方向・上下反転", PageLayout.InvertYOffset);
            CreateSlider(page, "Shake Reduction", "画面揺れ軽減", PageLayout.ShakeOffset, OptionValues.MinimumNormalizedValue, OptionValues.MaximumNormalizedValue, OptionValues.MinimumNormalizedValue, PageLayout.PreviewRowWidth);
            CreateButton("Preview Shake", page, "揺れを確認", new Vector2(PageLayout.PreviewButtonX, -PageLayout.ShakeOffset), PageLayout.PreviewButtonSize);
        }

        private static void BuildAudioTab(Transform page)
        {
            CreateSlider(page, "Master", "マスター音量", 0, OptionValues.MinimumNormalizedValue, OptionValues.MaximumNormalizedValue, OptionValues.MaximumNormalizedValue);
            CreateSlider(page, "SE", "SE音量", PageLayout.SEOffset, OptionValues.MinimumNormalizedValue, OptionValues.MaximumNormalizedValue, OptionValues.MaximumNormalizedValue, PageLayout.PreviewRowWidth);
            CreateSlider(page, "BGM", "BGM音量", PageLayout.BGMOffset, OptionValues.MinimumNormalizedValue, OptionValues.MaximumNormalizedValue, OptionValues.MaximumNormalizedValue, PageLayout.PreviewRowWidth);
            CreateSlider(page, "Voice", "ボイス音量", PageLayout.VoiceOffset, OptionValues.MinimumNormalizedValue, OptionValues.MaximumNormalizedValue, OptionValues.MaximumNormalizedValue, PageLayout.PreviewRowWidth);
            CreateButton("Preview SE", page, "SEを試聴", new Vector2(PageLayout.PreviewButtonX, -PageLayout.SEOffset), PageLayout.PreviewButtonSize);
            CreateButton("Preview BGM", page, "BGMを試聴", new Vector2(PageLayout.PreviewButtonX, -PageLayout.BGMOffset), PageLayout.PreviewButtonSize);
            CreateButton("Preview Voice", page, "ボイスを試聴", new Vector2(PageLayout.PreviewButtonX, -PageLayout.VoiceOffset), PageLayout.PreviewButtonSize);
            CreateLabel("Audio Note", page, "試聴ボタンでもう一度押すと停止します。メニューを閉じると試聴を終了します。", PageLayout.AudioNotePosition, PageLayout.AudioNoteSize, PageLayout.NoteFontSize, Muted);
        }

        private static void BuildControlsTab(Transform page)
        {
            CreateLabel("Controls Note", page, "変更したい操作を選び、新しいキー・マウスボタンを押してください。ESCでキャンセル。", Vector2.zero, PageLayout.ControlsNoteSize, PageLayout.ControlsNoteFontSize, Muted);
            BuildBindingScroll(page);
            var aim = CreateToggle(page, "Aim Assist", "エイムアシスト", PageLayout.AimOffset);
            aim.interactable = false;
            CreateLabel("Aim Unavailable", page, "対応するゲーム機能が未実装です", PageLayout.AimNotePosition, PageLayout.AimNoteSize, PageLayout.NoteFontSize, Muted);
        }

        private static void BuildBindingScroll(Transform parent)
        {
            var viewport = CreatePanel("Binding Viewport", parent, BindingLayout.ViewportPosition, BindingLayout.ViewportSize, BindingLayout.ViewportColor, true);
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = CreateRect("Binding Content", viewport, Vector2.zero, new Vector2(BindingWidth, 0));
            SetTopLeft(content, Vector2.zero, new Vector2(BindingWidth, 0));
            var layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = BindingLayout.RowSpacing;
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
            scroll.scrollSensitivity = BindingLayout.ScrollSensitivity;
            viewport.gameObject.AddComponent<OptionScrollSelection>();
            scroll.verticalScrollbar = BuildBindingScrollbar(viewport);
        }

        private static Scrollbar BuildBindingScrollbar(Transform viewport)
        {
            var track = CreatePanel("Binding Scroll Track", viewport, BindingLayout.TrackPosition, BindingLayout.TrackSize, Surface, true);
            var handle = CreatePanel("Binding Scroll Handle", track, Vector2.zero, Vector2.zero, Accent);
            Stretch(handle);
            var scrollbar = track.gameObject.AddComponent<Scrollbar>();
            scrollbar.targetGraphic = handle.GetComponent<Image>();
            scrollbar.handleRect = handle;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            return scrollbar;
        }

        private static void BuildConfirmation(Transform overlay)
        {
            var blocker = CreatePanel("Confirmation", overlay, Vector2.zero, Vector2.zero, ConfirmationLayout.BlockerColor);
            Stretch(blocker);
            var dialog = CreatePanel("Dialog", blocker, Vector2.zero, ConfirmationLayout.DialogSize, Surface);
            CreateLabel("Confirm Heading", dialog, "確認", ConfirmationLayout.HeadingPosition, ConfirmationLayout.HeadingSize, ConfirmationLayout.HeadingFontSize, Accent);
            CreateLabel("Confirm Message", dialog, "ゲームを閉じますか？", ConfirmationLayout.MessagePosition, ConfirmationLayout.MessageSize, ConfirmationLayout.MessageFontSize, Color.white);
            CreateButton("Confirm Cancel", dialog, "キャンセル", ConfirmationLayout.CancelPosition, ConfirmationLayout.ButtonSize);
            CreateButton("Confirm Accept", dialog, "実行", ConfirmationLayout.AcceptPosition, ConfirmationLayout.ButtonSize, true);
            blocker.gameObject.SetActive(false);
        }

        private static void CreateChoice(Transform page, string name, string caption, float y, string value)
        {
            CreateLabel(name + " Label", page, caption, new Vector2(0, -y), ControlLayout.CaptionSize, ControlLayout.CaptionFontSize, Color.white);
            CreateButton(name + " Previous", page, "<", new Vector2(ControlLayout.ControlLeft, -y), ControlLayout.ChoiceButtonSize);
            var label = CreateLabel(name + " Value", page, value, new Vector2(ControlLayout.ChoiceValueX, -y), ControlLayout.ChoiceValueSize, ControlLayout.ChoiceValueFontSize, Accent);
            label.alignment = TextAnchor.MiddleCenter;
            CreateButton(name + " Next", page, ">", new Vector2(ControlLayout.ChoiceNextX, -y), ControlLayout.ChoiceButtonSize);
        }

        private static void CreateSlider(Transform page, string name, string caption, float y, float min, float max, float value, float width = OptionsLayout.PageWidth)
        {
            CreateLabel(name + " Label", page, caption, new Vector2(0, -y), ControlLayout.SliderCaptionSize, ControlLayout.SliderCaptionFontSize, Color.white);
            var root = CreateRect(name, page, Vector2.zero, Vector2.zero);
            SetTopLeft(root, new Vector2(ControlLayout.ControlLeft, -y), new Vector2(width - ControlLayout.SliderReservedWidth, ControlLayout.RowHeight));
            var track = CreatePanel(name + " Track", root, Vector2.zero, Vector2.zero, Surface);
            track.anchorMin = new Vector2(0, ControlLayout.TrackAnchorMin); track.anchorMax = new Vector2(1, ControlLayout.TrackAnchorMax);
            track.offsetMin = new Vector2(ControlLayout.HandleInset, 0); track.offsetMax = new Vector2(-ControlLayout.HandleInset, 0);
            var fill = CreatePanel(name + " Fill", track, Vector2.zero, Vector2.zero, Accent);
            Stretch(fill);
            var handleArea = CreateRect(name + " Handle Area", root, Vector2.zero, Vector2.zero);
            Stretch(handleArea); handleArea.offsetMin = new Vector2(ControlLayout.HandleInset, 0); handleArea.offsetMax = new Vector2(-ControlLayout.HandleInset, 0);
            var handle = CreatePanel(name + " Handle", handleArea, Vector2.zero, ControlLayout.SliderHandleSize, Accent);
            handle.sizeDelta = ControlLayout.SliderHandleDelta;
            var slider = root.gameObject.AddComponent<Slider>();
            slider.fillRect = fill; slider.handleRect = handle;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.minValue = min; slider.maxValue = max; slider.value = value;
            slider.navigation = new Navigation { mode = Navigation.Mode.Automatic };
            var label = CreateLabel(name + " Value", page, "100%", new Vector2(width - ControlLayout.ValueWidth, -y), ControlLayout.SliderValueSize, ControlLayout.ValueFontSize, Accent);
            label.alignment = TextAnchor.MiddleRight;
        }

        private static Toggle CreateToggle(Transform page, string name, string caption, float y)
        {
            CreateLabel(name + " Label", page, caption, new Vector2(0, -y), ControlLayout.ToggleCaptionSize, ControlLayout.SliderCaptionFontSize, Color.white);
            var box = CreatePanel(name, page, new Vector2(ControlLayout.ControlLeft, -y), ControlLayout.ToggleSize, Surface, true);
            var check = CreatePanel(name + " Check", box, Vector2.zero, Vector2.zero, Accent);
            Stretch(check); check.offsetMin = ControlLayout.ToggleInset; check.offsetMax = -ControlLayout.ToggleInset;
            var toggle = box.gameObject.AddComponent<Toggle>();
            toggle.targetGraphic = box.GetComponent<Image>();
            toggle.graphic = check.GetComponent<Image>();
            toggle.isOn = false;
            return toggle;
        }

        public static Button CreateButton(string name, Transform parent, string caption, Vector2 position, Vector2 size, bool primary = false)
        {
            var root = CreatePanel(name, parent, position, size, primary ? Accent : Surface, true);
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = root.GetComponent<Image>();
            var colors = button.colors;
            colors.highlightedColor = ControlLayout.HighlightColor;
            colors.selectedColor = ControlLayout.HighlightColor;
            colors.pressedColor = ControlLayout.PressedColor;
            colors.disabledColor = ControlLayout.DisabledColor;
            button.colors = colors;
            var text = CreateLabel(name + " Text", root, caption, Vector2.zero, Vector2.zero, ControlLayout.ButtonFontSize, primary ? Background : Color.white);
            Stretch(text.rectTransform);
            text.alignment = TextAnchor.MiddleCenter;
            return button;
        }

        public static Text CreateLabel(string name, Transform parent, string text, Vector2 position, Vector2 size, int fontSize, Color color)
        {
            var rect = CreateRect(name, parent, Vector2.zero, Vector2.zero);
            SetTopLeft(rect, position, size);
            var label = rect.gameObject.AddComponent<Text>();
            label.font = _font != null ? _font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = text; label.fontSize = fontSize; label.color = color;
            label.alignment = TextAnchor.MiddleLeft;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Truncate;
            label.raycastTarget = false;
            return label;
        }

        private static RectTransform CreatePanel(string name, Transform parent, Vector2 position, Vector2 size, Color color, bool topLeft = false)
        {
            var rect = CreateRect(name, parent, position, size);
            if (topLeft) SetTopLeft(rect, position, size);
            var image = rect.gameObject.AddComponent<Image>(); image.color = color;
            return rect;
        }

        private static RectTransform CreateRect(string name, Transform parent, Vector2 position, Vector2 size)
        {
            var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = CanvasStyle.Center;
            rect.sizeDelta = size; rect.anchoredPosition = position;
            return rect;
        }

        private static void SetTopLeft(RectTransform rect, Vector2 position, Vector2 size)
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
