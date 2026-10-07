// 追加のRenderer種別を導入するための境界。CPU頂点の読出しは要求しない。
using UnityEngine;

namespace Echo.Echolocation
{
    /// <summary>対応Rendererの描画形状を取得する。</summary>
    public interface IEchoGeometryAdapter
    {
        /// <summary>現在の形状とサブメッシュ数を返す。未対応ならfalse。</summary>
        bool TryGetGeometry(Renderer renderer, out Mesh mesh);
    }
}
