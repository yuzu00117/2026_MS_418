using UnityEngine;
using UnityEngine.UI;

namespace MiraiSozo.Options
{
    /// <summary>機械的な開閉の途中、パネルが細い帯の間だけ中央を横切る光のライン。</summary>
    public sealed class OptionDeployLine
    {
        private readonly int _layer;
        private readonly Vector3[] _corners = new Vector3[4];
        private Image _image;

        public OptionDeployLine(int layer)
        {
            _layer = layer;
        }

        /// <summary>パネルの現在の位置と幅に合わせて表示する。opacityが0なら隠す。</summary>
        public void Show(RectTransform panel, float opacity)
        {
            if (opacity <= 0f || panel == null)
            {
                Hide();
                return;
            }
            if (_image == null) Create();
            var line = _image.rectTransform;
            line.SetParent(panel.parent, false);
            // パネルの直後に置き、パネルの上に重ねる。
            line.SetAsLastSibling();
            line.SetSiblingIndex(panel.GetSiblingIndex() + 1);
            panel.GetWorldCorners(_corners);
            Vector2 min = line.parent.InverseTransformPoint(_corners[0]);
            Vector2 max = line.parent.InverseTransformPoint(_corners[2]);
            line.localPosition = (min + max) * 0.5f;
            var theme = OptionMenuTheme.Current;
            line.sizeDelta = new Vector2(max.x - min.x, theme.DeployLineThickness);
            var color = theme.DeployLineColor;
            color.a *= opacity;
            _image.color = color;
            _image.gameObject.SetActive(true);
        }

        public void Hide()
        {
            if (_image != null) _image.gameObject.SetActive(false);
        }

        private void Create()
        {
            var root = new GameObject("Deploy Line", typeof(RectTransform), typeof(LayoutElement));
            root.layer = _layer;
            root.GetComponent<LayoutElement>().ignoreLayout = true;
            var rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            _image = root.AddComponent<Image>();
            _image.raycastTarget = false;
        }
    }
}
