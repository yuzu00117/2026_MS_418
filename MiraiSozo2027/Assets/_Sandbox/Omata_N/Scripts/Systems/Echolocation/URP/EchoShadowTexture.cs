// エコー地点からの影マップをRender Graphへ読み取り専用で登録する。
using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;

namespace Echo.Echolocation
{
    public sealed class EchoShadowTexture : IDisposable
    {
        private Texture _texture;
        private RTHandle _handle;
        public TextureHandle Import(RenderGraph graph, EchoController controller)
        {
            Texture next = controller.ShadowMaps.IsReady ? controller.ShadowMaps.Texture : null;
            if (!ReferenceEquals(_texture, next))
            {
                Dispose();
                _texture = next;
                if (next)
                    _handle = RTHandles.Alloc(next);
            }

            return _handle != null ? graph.ImportTexture(_handle) : TextureHandle.nullHandle;
        }

        public void Dispose()
        {
            _handle?.Release();
            _handle = null;
            _texture = null;
        }
    }
}
