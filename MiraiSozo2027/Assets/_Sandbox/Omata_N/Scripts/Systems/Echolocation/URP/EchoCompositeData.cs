// Rendererごとに深度を分離して強調色を蓄積する。対象同士で奥を隠さない。
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.Experimental.Rendering;

namespace Echo.Echolocation
{
    sealed internal class EchoCompositeData
    {
        public TextureHandle Source { get; set; }
        public Material Material { get; set; }
    }
}
