// スキニングはUnityのDrawRenderer経路に任せ、毎フレームBakeMeshしない。
using UnityEngine;

namespace Echo.Echolocation
{
    /// <summary>スキンドメッシュのアダプター。</summary>
    public sealed class EchoSkinnedGeometryAdapter : IEchoGeometryAdapter
    {
        /// <summary>サブメッシュ列挙用の共有メッシュを返す。描画時は現在の変形が使用される。</summary>
        public bool TryGetGeometry(Renderer renderer, out Mesh mesh)
        {
            mesh = renderer is SkinnedMeshRenderer skinned ? skinned.sharedMesh : null;
            return mesh;
        }
    }
}
