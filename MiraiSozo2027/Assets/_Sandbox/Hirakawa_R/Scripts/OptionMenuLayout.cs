using UnityEngine;
using UnityEngine.UI;

namespace MiraiSozo.Options
{
    /// <summary>オプションの共通色とキー設定一覧のレイアウト。</summary>
    public static class OptionMenuLayout
    {
        public const int TabCount = ControlsTab + 1;
        public const int SystemTab = 0;
        public const int CameraTab = 1;
        public const int AudioTab = 2;
        public const int ControlsTab = 3;
        public const int AudioChannelCount = 3;
        public const int MinimumBindingSlots = 24;
        // 共通色はOptionMenuThemeで変更できる。
        public static Color Background => OptionMenuTheme.Current.Light;
        public static Color Surface => OptionMenuTheme.Current.Surface;
        public static Color Accent => OptionMenuTheme.Current.Accent;
        public static Color AccentSurface => new Color(Accent.r, Accent.g, Accent.b, Surface.a);
        public static Color TextColor => OptionMenuTheme.Current.Text;

        private const int BindingFontSize = 17;
        private const int MinimumBindingFontSize = 10;
        private const float LabelInset = 6f;
        private const float LabelWidthRatio = 0.65f;
        private static Color ReserveColor => Surface;
        private static readonly Color _reserveTextColor = new Color32(71, 96, 107, 255);
        private static readonly Color _highlightColor = new Color(1f, 1f, 1f, 0.95f);
        private const float PressedOpacity = 0.9f;
        private static Color PressedColor => new Color(Accent.r, Accent.g, Accent.b, PressedOpacity);
        private static readonly Color _disabledColor = new Color(176f / 255f, 202f / 255f, 210f / 255f, 0.35f);

        public static void FitBindingGrid(GridLayoutGroup grid, int slotCount)
        {
            int columns = grid.constraintCount;
            int rows = Mathf.CeilToInt((float)slotCount / columns);
            var area = ((RectTransform)grid.transform).rect.size;
            float width = (area.x - grid.padding.horizontal - grid.spacing.x * (columns - 1)) / columns;
            float height = (area.y - grid.padding.vertical - grid.spacing.y * (rows - 1)) / rows;
            grid.cellSize = new Vector2(Mathf.Max(1f, width), Mathf.Max(1f, height));
        }

        public static Button CreateBindingButton(Transform parent, string caption, string key, Font font, out Text keyLabel)
        {
            var image = CreateBindingSlot("Binding", parent, Color.white);
            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = Surface;
            colors.highlightedColor = _highlightColor;
            colors.selectedColor = _highlightColor;
            colors.pressedColor = PressedColor;
            colors.disabledColor = _disabledColor;
            button.colors = colors;
            CreateBindingLabel(image.transform, "Action", caption, font, isKey: false);
            keyLabel = CreateBindingLabel(image.transform, "Key", key, font, isKey: true);
            return button;
        }

        public static void CreateReservedBindingSlot(Transform parent, Font font)
        {
            var image = CreateBindingSlot("Binding Reserved", parent, ReserveColor);
            image.raycastTarget = false;
            CreateBindingLabel(image.transform, "Action", "予備枠", font, isKey: false).color = _reserveTextColor;
        }

        private static Image CreateBindingSlot(string name, Transform parent, Color color)
        {
            var root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(parent, false);
            var image = root.AddComponent<Image>();
            image.color = color;
            OptionSoftSprite.Apply(image, OptionSoftStyle.SlotRadius);
            return image;
        }

        private static Text CreateBindingLabel(Transform parent, string name, string caption, Font font, bool isKey)
        {
            var label = new GameObject(name, typeof(RectTransform)).AddComponent<Text>();
            var rect = label.rectTransform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(isKey ? LabelWidthRatio : 0f, 0f);
            rect.anchorMax = new Vector2(isKey ? 1f : LabelWidthRatio, 1f);
            rect.offsetMin = new Vector2(LabelInset, 0f);
            rect.offsetMax = new Vector2(-LabelInset, 0f);
            label.font = font != null ? font : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = caption;
            label.fontSize = BindingFontSize;
            label.resizeTextForBestFit = true;
            label.resizeTextMinSize = MinimumBindingFontSize;
            label.resizeTextMaxSize = BindingFontSize;
            label.color = isKey ? Accent : TextColor;
            label.alignment = isKey ? TextAnchor.MiddleRight : TextAnchor.MiddleLeft;
            label.raycastTarget = false;
            return label;
        }
    }
}
