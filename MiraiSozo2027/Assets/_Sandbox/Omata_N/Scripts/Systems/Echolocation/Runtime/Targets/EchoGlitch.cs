using UnityEngine;
namespace Echo.Echolocation
{
    /// <summary>所有するRendererの輪郭を画面上で断片化する。通常の輪郭線は不要。</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(EchoTag))]
    public sealed class EchoGlitch : MonoBehaviour
    {
        private const float DefaultWidth = 4;
        private const float DefaultSpeed = 18;
        private const float DefaultDuration = .6f;
        [SerializeField, Tooltip("Always: 常時 / WhileRevealed: 表示中 / OnReveal: 全体表示開始時のみ")]
        private EchoGlitchMode _mode = EchoGlitchMode.OnReveal;
        [SerializeField, ColorUsage(true, true)] private Color _color = Color.cyan;
        [SerializeField, Range(0, 1)] private float _intensity = .85f;
        [SerializeField, Range(1, 16)] private float _width = DefaultWidth;
        [SerializeField, Range(0, 32)] private float _displacement = 8;
        [SerializeField, Range(1, 64)] private float _blockHeight = 8;
        [SerializeField, Range(0, 60)] private float _speed = DefaultSpeed;
        [SerializeField, Min(0)] private float _duration = DefaultDuration;
        [SerializeField, Tooltip("ContactRegion時は接触部分の境界に限定。輪郭線そのものは描画しません。")]
        private bool _isContactBoundaryEnabled = true;
        public EchoGlitchMode Mode { get => _mode; set => _mode = value; }
        public Color Color => _color;
        public float Intensity => ClampFinite(_intensity, 0, 1, 0);
        public float Width => ClampFinite(_width, 1, 16, DefaultWidth);
        public float Displacement => ClampFinite(_displacement, 0, 32, 0);
        public float BlockHeight => ClampFinite(_blockHeight, 1, 64, 8);
        public float Speed => ClampFinite(_speed, 0, 60, DefaultSpeed);
        public float Duration => ClampFinite(_duration, 0, 3600, DefaultDuration);
        public bool IsContactBoundaryEnabled => _isContactBoundaryEnabled;
        private static float ClampFinite(float value, float min, float max, float fallback) =>
            float.IsFinite(value) ? Mathf.Clamp(value, min, max) : fallback;
    }
}
