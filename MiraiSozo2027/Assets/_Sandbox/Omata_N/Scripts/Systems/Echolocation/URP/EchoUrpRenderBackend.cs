// 専用Materialの生成と破棄。対象のMaterialやPropertyBlockには触れない。
using UnityEngine;
using UnityEngine.Rendering;

namespace Echo.Echolocation
{
    /// <summary>URP用の描画リソース。</summary>
    public sealed class EchoUrpRenderBackend : IEchoRenderBackend
    {
        public Material Highlight { get; private set; }
        public Material Composite { get; private set; }
        /// <summary>両シェーダーが利用可能か。</summary>
        public bool IsReady => Highlight && Composite;

        /// <summary>Featureで保持したシェーダーから専用Materialを生成する。</summary>
        public EchoUrpRenderBackend(Shader highlight, Shader composite)
        {
            if (highlight)
                Highlight = CoreUtils.CreateEngineMaterial(highlight);
            if (composite)
                Composite = CoreUtils.CreateEngineMaterial(composite);
        }

        /// <summary>Feature再生成時にも確実に破棄する。</summary>
        public void Dispose()
        {
            CoreUtils.Destroy(Highlight);
            CoreUtils.Destroy(Composite);
            Highlight = Composite = null;
        }
    }
}
