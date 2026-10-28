using UnityEngine;
using UnityEngine.Rendering;
namespace Echo.Echolocation
{
    /// <summary>共有設定を保存変更せず、デモ専用Pipelineを再生中だけ使う。</summary>
    [DefaultExecutionOrder(-2000)]
    public sealed class EchoGlitchDemo : MonoBehaviour
    {
        private const float DefaultCycle = 6;
        [SerializeField] private RenderPipelineAsset _pipeline;
        [SerializeField] private EchoController _controller;
        [SerializeField] private bool _isAutomatic = true;
        [SerializeField, Min(1)] private float _cycleSeconds = DefaultCycle;
        private RenderPipelineAsset _previousPipeline;
        private float _elapsed;
        private bool _isScanning;
        public EchoController Controller => _controller;
        public bool IsAutomatic { get => _isAutomatic; set => _isAutomatic = value; }
        public void Configure(RenderPipelineAsset pipeline, EchoController controller)
        { _pipeline = pipeline; _controller = controller; }
        private void OnEnable()
        {
            _previousPipeline = QualitySettings.renderPipeline;
            if (_pipeline) QualitySettings.renderPipeline = _pipeline;
        }
        private void Start() => SetScanning(true);
        private void Update()
        {
            if (!_isAutomatic || !_controller) return;
            _elapsed += Time.deltaTime;
            float cycle = float.IsFinite(_cycleSeconds) ? Mathf.Max(1, _cycleSeconds) : DefaultCycle;
            if (_elapsed < cycle) return;
            _elapsed = 0;
            SetScanning(!_isScanning);
        }
        public void SetScanning(bool isScanning)
        { _isScanning = isScanning; if (_controller) _controller.SetScanEnabled(isScanning); }
        private void OnDisable()
        {
            if (QualitySettings.renderPipeline == _pipeline) QualitySettings.renderPipeline = _previousPipeline;
        }
        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(16, 16, 470, 150), GUI.skin.box);
            GUILayout.Label("グリッチ検証｜左：常時／中央：表示開始時／右：接触部分");
            _isAutomatic = GUILayout.Toggle(_isAutomatic, "スキャンを自動で切り替える");
            if (GUILayout.Button(_isScanning ? "スキャン停止" : "スキャン開始")) SetScanning(!_isScanning);
            GUILayout.Label("管理元の「タグと表示設定」でグリッチを調整できます。");
            GUILayout.EndArea();
        }
    }
}
