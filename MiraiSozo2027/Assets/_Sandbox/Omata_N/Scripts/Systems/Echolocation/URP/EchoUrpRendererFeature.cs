// Base Cameraの透明物描画後にRender Graphパスを追加する。
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Echo.Echolocation
{
    /// <summary>Universal Renderer Dataに追加するエコー描画Feature。</summary>
    public sealed class EchoUrpRendererFeature : ScriptableRendererFeature
    {
        [SerializeField, Tooltip("ビルドでシェーダーが除去されないようアセット参照を保持します。")]
        [UnityEngine.Serialization.FormerlySerializedAs("highlightShader")]
        private Shader _highlightShader;
        [SerializeField, Tooltip("乗算済みアルファで合成するシェーダーです。")]
        [UnityEngine.Serialization.FormerlySerializedAs("compositeShader")]
        private Shader _compositeShader;
        [SerializeField, Tooltip("スキャン範囲と境界線の表示シェーダーです。")]
        [UnityEngine.Serialization.FormerlySerializedAs("rangeShader")]
        private Shader _rangeShader;
        private EchoUrpRenderBackend _backend;
        private EchoUrpRenderPass _pass;
        private Material _rangeMaterial;
        private EchoRangeRenderPass _rangePass;
        /// <summary>Materialとパスを再生成する。</summary>
        public override void Create()
        {
            _backend?.Dispose();
            _pass?.Dispose();
            _rangePass?.Dispose();
            CoreUtils.Destroy(_rangeMaterial);
            if (!_highlightShader)
                _highlightShader = Shader.Find("Hidden/Echo/Highlight");
            if (!_compositeShader)
                _compositeShader = Shader.Find("Hidden/Echo/Composite");
            if (!_rangeShader)
                _rangeShader = Shader.Find("Hidden/Echo/Range");
            if (_rangeShader)
            {
                _rangeMaterial = CoreUtils.CreateEngineMaterial(_rangeShader);
                _rangePass = new EchoRangeRenderPass(_rangeMaterial);
            }

            _backend = new EchoUrpRenderBackend(_highlightShader, _compositeShader);
            _pass = new EchoUrpRenderPass(_backend)
            {
                renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing
            };
        }

        /// <summary>登録済みGame Cameraだけに追加する。</summary>
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
        {
            if (!Application.isPlaying || !_backend.IsReady || data.cameraData.cameraType != CameraType.Game || data.cameraData.renderType != CameraRenderType.Base)
                return;
            var controller = EchoController.GetForCamera(data.cameraData.camera);
            if (!controller || controller.CurrentState == EchoState.Hidden)
                return;
            if (_rangeMaterial && controller.TryGetComponent<EchoRangeDisplay>(out var display) && display.isActiveAndEnabled)
            {
                _rangePass.ConfigureInput(ScriptableRenderPassInput.Depth);
                renderer.EnqueuePass(_rangePass);
            }

            if (controller.RenderItems.Count == 0)
                return;
            _pass.ConfigureInput(ScriptableRenderPassInput.Depth);
            renderer.EnqueuePass(_pass);
        }

        /// <summary>保持するGPUリソースを解放する。</summary>
        protected override void Dispose(bool isDisposing)
        {
            _pass?.Dispose();
            _rangePass?.Dispose();
            _backend?.Dispose();
            CoreUtils.Destroy(_rangeMaterial);
        }
    }
}
