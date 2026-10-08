// シーン深度から表面位置を復元し、対象タグのない床にもスキャン範囲を表示する。
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace Echo.Echolocation
{
    sealed internal class EchoRangePassData
    {
        public Material Material { get; set; }
        public Mesh Mesh { get; set; }
        public EchoVolumeShaderData Waves { get; } = new EchoVolumeShaderData();
        public Color Fill { get; set; }
        public Color Wave { get; set; }
        public Color Erase { get; set; }
        public Color Boundary { get; set; }
        public float Width { get; set; }
        public bool IsSurfaceVisible { get; set; }
        public bool IsBoundaryVisible { get; set; }
    }
}
