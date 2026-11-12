// MeshRendererの形状取得。Read/Writeフラグは変更しない。
using System.Collections.Generic;
using UnityEngine;

namespace Echo.Echolocation
{
    /// <summary>通常メッシュのアダプター。</summary>
    public sealed class EchoMeshGeometryAdapter : IEchoGeometryAdapter
    {
        // 候補の収集で毎フレーム呼ばれるため、MeshFilterの検索は対象ごとに初回だけにする。
        private readonly Dictionary<Renderer, MeshFilter> _filters = new Dictionary<Renderer, MeshFilter>();

        /// <summary>共有メッシュ参照だけを取得する。メッシュの差し替えは毎回反映する。</summary>
        public bool TryGetGeometry(Renderer renderer, out Mesh mesh)
        {
            mesh = null;
            if (!(renderer is MeshRenderer))
                return false;
            if (!_filters.TryGetValue(renderer, out var filter) || !filter)
            {
                renderer.TryGetComponent(out filter);
                _filters[renderer] = filter;
            }

            mesh = filter ? filter.sharedMesh : null;
            return mesh;
        }

        /// <summary>シーンの再収集時に、破棄された対象の記録を捨てる。</summary>
        public void Clear() => _filters.Clear();
    }
}
