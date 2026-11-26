using UnityEngine;
using UnityEngine.UI;

namespace MiraiSozo.Options
{
    /// <summary>Prefabを変更せずに、実行時にメニューへ柔らかいSF調の見た目を適用する。</summary>
    public static class OptionSoftStyle
    {
        public static float PanelRadius => OptionMenuTheme.Current.PanelRadius;
        public static float ButtonRadius => OptionMenuTheme.Current.ButtonRadius;
        public static float SlotRadius => OptionMenuTheme.Current.SlotRadius;
        private const string FrameName = "Soft Frame";

        /// <summary>パネルの角を丸め、枠装飾を重ねる。decorateButtonsが有効ならボタンにも装飾を付ける。</summary>
        public static void ApplyPanel(RectTransform panel, bool decorateButtons)
        {
            if (panel == null) return;
            OptionSoftSprite.Apply(panel.GetComponent<Image>(), PanelRadius);
            if (panel.Find(FrameName) == null) CreateFrame(panel);
            RoundButtons(panel);
            PreventClickTheft(panel);
            if (!decorateButtons) return;
            foreach (var button in panel.GetComponentsInChildren<Button>(true))
                if (button.GetComponent<OptionSoftButton>() == null) button.gameObject.AddComponent<OptionSoftButton>();
        }

        public static void RoundButtons(Transform root)
        {
            if (root == null) return;
            foreach (var button in root.GetComponentsInChildren<Button>(true))
                OptionSoftSprite.Apply(button.targetGraphic as Image, ButtonRadius);
        }

        /// <summary>
        /// ボタンの近くにある見出しや説明の文字・飾りがクリックを横取りしないよう、マウス判定を外す。
        /// （UnityのTextは初期状態でマウス判定を持つため、ボタンに重なると押せる範囲がずれて見える）
        /// ボタン自身の判定（targetGraphic）、スライダーなどの部品、スクロール範囲の判定はそのまま残す。
        /// </summary>
        public static void PreventClickTheft(Transform root)
        {
            if (root == null) return;
            foreach (var graphic in root.GetComponentsInChildren<Graphic>(true))
            {
                if (!graphic.raycastTarget || graphic.transform == root) continue;
                if (graphic.GetComponentInParent<ScrollRect>(true) != null) continue;
                var selectable = graphic.GetComponentInParent<Selectable>(true);
                // ボタンの中の文字は、ボタンの枠からはみ出して押せる範囲を広げないよう判定を外す。
                bool isButtonLabel = selectable is Button && graphic != selectable.targetGraphic;
                if (selectable == null || isButtonLabel) graphic.raycastTarget = false;
            }
        }

        private static void CreateFrame(RectTransform panel)
        {
            var frame = new GameObject(FrameName, typeof(RectTransform), typeof(LayoutElement));
            frame.layer = panel.gameObject.layer;
            frame.GetComponent<LayoutElement>().ignoreLayout = true;
            var rect = (RectTransform)frame.transform;
            rect.SetParent(panel, false);
            // パネル背景の上、項目の下に描画する。
            rect.SetAsFirstSibling();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            frame.AddComponent<OptionSoftFrame>().raycastTarget = false;
        }
    }
}
