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
        public List<EchoRenderItem> Items { get; set; }
        // パスのデータはRender Graphが使い回すため、配列も一度だけ確保する。
        public Matrix4x4[] ContactMatrices { get; } = new Matrix4x4[3];
        public Material Material { get; set; }
        public TextureHandle SurfaceMemory;
        public Matrix4x4 WorldToMemory;
        public float ContactTime;
    }
}
