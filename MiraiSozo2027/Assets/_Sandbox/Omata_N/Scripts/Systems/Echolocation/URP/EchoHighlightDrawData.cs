// Rendererごとに深度を分離して強調色を蓄積する。対象同士で奥を隠さない。
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering;

namespace Echo.Echolocation
{
    sealed internal class EchoHighlightDrawData
    {
        public EchoVolumeShaderData Waves { get; } = new EchoVolumeShaderData();
        public EchoOcclusionMode Occlusion { get; set; }
        public EchoRenderItem Item { get; set; }
        public Material Material { get; set; }
    }
}
