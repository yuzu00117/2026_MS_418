// MeshRendererの形状取得。Read/Writeフラグは変更しない。
using UnityEngine;

namespace Echo.Echolocation
{
    /// <summary>通常メッシュのアダプター。</summary>
    public sealed class EchoMeshGeometryAdapter : IEchoGeometryAdapter
    {
        /// <summary>共有メッシュ参照だけを取得する。</summary>
        public bool TryGetGeometry(Renderer r, out Mesh mesh)
        {
            mesh = r is MeshRenderer && r.TryGetComponent<MeshFilter>(out var f) ? f.sharedMesh : null;
            return mesh;
        }
    }
}
