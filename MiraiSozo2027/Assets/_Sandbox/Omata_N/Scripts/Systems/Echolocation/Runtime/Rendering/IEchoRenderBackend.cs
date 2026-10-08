// 描画実装の寿命管理用契約。CoreはURP型を参照しない。
using System;

namespace Echo.Echolocation
{
    /// <summary>描画パイプライン固有リソースの管理境界。</summary>
    public interface IEchoRenderBackend : IDisposable
    {
        /// <summary>シェーダー等が利用可能か。</summary>
        bool IsReady { get; }
    }
}
