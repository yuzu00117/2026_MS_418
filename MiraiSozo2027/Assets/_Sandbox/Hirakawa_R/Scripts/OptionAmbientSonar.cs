using UnityEngine;
using UnityEngine.UI;

namespace MiraiSozo.Options
{
    /// <summary>パネルを表示している間、背景で控えめなソナーの波紋を一定間隔で流す。</summary>
    public sealed class OptionAmbientSonar : MonoBehaviour
    {
        private const string ObjectName = "Ambient Sonar";
        // 間隔・速さ・濃さはOptionMenuThemeで調整する。
        private OptionMenuSonar _sonar;
        private bool _fromLeft;
        private float _timer;
        private bool _isPulsing;
        private Vector2 _origin;
        private float _startRadius;
        private float _endRadius;

        /// <summary>パネルの背景（項目と枠の下）に波紋を付ける。fromLeftなら左端から、そうでなければ上端から発信する。</summary>
        public static void Attach(RectTransform panel, bool fromLeft)
        {
            if (panel == null || panel.Find(ObjectName) != null) return;
            var root = new GameObject(ObjectName, typeof(RectTransform), typeof(RectMask2D), typeof(LayoutElement));
            root.layer = panel.gameObject.layer;
            root.GetComponent<LayoutElement>().ignoreLayout = true;
            var rect = (RectTransform)root.transform;
            rect.SetParent(panel, false);
            // パネル背景の直上、枠装飾と項目の下に描画する。
            rect.SetAsFirstSibling();
            Stretch(rect);
            var wave = new GameObject("Wave", typeof(RectTransform));
            wave.layer = panel.gameObject.layer;
            var waveRect = (RectTransform)wave.transform;
            waveRect.SetParent(rect, false);
            Stretch(waveRect);
            var ambient = root.AddComponent<OptionAmbientSonar>();
            ambient._fromLeft = fromLeft;
            ambient._sonar = wave.AddComponent<OptionMenuSonar>();
            ambient._sonar.raycastTarget = false;
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        // 表示し直すたびに数え直し、開く演出の波紋と重ならないよう最初の発信は一間隔後にする。
        private void OnEnable()
        {
            _timer = 0f;
            _isPulsing = false;
            if (_sonar != null) _sonar.SetPulse(Vector2.zero, 0f, 0f, 1f);
        }

        private void Update()
        {
            var theme = OptionMenuTheme.Current;
            if (!theme.AmbientEnabled)
            {
                if (_isPulsing) _sonar.SetPulse(Vector2.zero, 0f, 0f, 1f);
                _isPulsing = false;
                _timer = 0f;
                return;
            }
            // ポーズ中も動くよう、unscaledDeltaTimeで進める。
            _timer += Time.unscaledDeltaTime;
            if (_timer >= theme.AmbientInterval)
            {
                _timer -= theme.AmbientInterval;
                BeginPulse(theme);
            }
            if (!_isPulsing) return;
            _sonar.Intensity = theme.AmbientIntensity;
            float progress = Mathf.Clamp01(_timer / theme.AmbientDuration);
            _sonar.SetPulse(_origin, _startRadius, _endRadius, progress);
            if (progress >= 1f) _isPulsing = false;
        }

        // 発信のたびにパネルの現在の大きさから発信点と終点を決める。
        private void BeginPulse(OptionMenuTheme theme)
        {
            float offset = theme.OriginOffset;
            var rect = _sonar.rectTransform.rect;
            _origin = _fromLeft ? new Vector2(rect.xMin - offset, rect.center.y) : new Vector2(rect.center.x, rect.yMax + offset);
            var nearest = new Vector2(Mathf.Clamp(_origin.x, rect.xMin, rect.xMax), Mathf.Clamp(_origin.y, rect.yMin, rect.yMax));
            _startRadius = Vector2.Distance(_origin, nearest);
            float x = Mathf.Max(Mathf.Abs(_origin.x - rect.xMin), Mathf.Abs(_origin.x - rect.xMax));
            float y = Mathf.Max(Mathf.Abs(_origin.y - rect.yMin), Mathf.Abs(_origin.y - rect.yMax));
            // 最後の輪の尾まで枠を抜けきってから消えるよう、尾の長さ分の余白を足す。
            _endRadius = new Vector2(x, y).magnitude + theme.TailLength;
            _isPulsing = true;
        }
    }
}
