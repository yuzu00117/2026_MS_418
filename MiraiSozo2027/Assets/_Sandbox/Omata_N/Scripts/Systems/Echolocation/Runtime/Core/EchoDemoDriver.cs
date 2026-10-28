using UnityEngine;
using UnityEngine.Rendering;
namespace Echo.Echolocation
{
    /// <summary>共有設定を保存変更せず、デモ専用Pipelineを再生中だけ使う。</summary>
    [DefaultExecutionOrder(-2000)]
    public sealed class EchoDemoDriver : MonoBehaviour
    {
        private const float DefaultCycle = 6;
        // 画面左上に出す操作パネルの位置と大きさ（ピクセル）。
        private static readonly Rect PanelRect = new Rect(16, 16, 470, 150);
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
            GUILayout.BeginArea(PanelRect, GUI.skin.box);
            GUILayout.Label("エコー表示検証：各対象の専用タグで表示を設定");
            _isAutomatic = GUILayout.Toggle(_isAutomatic, "スキャンを自動で切り替える");
            if (GUILayout.Button(_isScanning ? "スキャン停止" : "スキャン開始")) SetScanning(!_isScanning);
            GUILayout.Label("対象の専用タグのアセットで3種類の表示を調整できます。");
            GUILayout.EndArea();
        }
    }
}
