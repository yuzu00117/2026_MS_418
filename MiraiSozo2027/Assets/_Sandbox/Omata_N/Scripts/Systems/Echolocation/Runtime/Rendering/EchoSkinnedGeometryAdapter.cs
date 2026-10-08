// スキニングはUnityのDrawRenderer経路に任せ、毎フレームBakeMeshしない。
using UnityEngine;

namespace Echo.Echolocation
{
    /// <summary>スキンドメッシュのアダプター。</summary>
    public sealed class EchoSkinnedGeometryAdapter : IEchoGeometryAdapter
    {
        /// <summary>サブメッシュ列挙用の共有メッシュを返す。描画時は現在の変形が使用される。</summary>
        public bool TryGetGeometry(Renderer r, out Mesh mesh)
        {
            mesh = r is SkinnedMeshRenderer s ? s.sharedMesh : null;
            return mesh;
        }
    }
}
