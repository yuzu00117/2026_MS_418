// 描画へ渡す1つのRenderer。サブメッシュ数は現在のメッシュから取得する。
using UnityEngine;

namespace Echo.Echolocation
{
    /// <summary>元のマテリアルを変更せず追加描画するための情報。</summary>
    public readonly struct EchoRenderItem
    {
        public Renderer Renderer { get; }
        public Color Color { get; }
        public int SubMeshCount { get; }
        public EchoTagColorRule Rule { get; }
        public Object EffectGroup { get; }
        public EchoRevealState Reveal { get; }

        /// <summary>対象・色・サブメッシュ数を固定する。</summary>
        public EchoRenderItem(Renderer renderer, Color color, int count)
        {
            Renderer = renderer;
            Color = color;
            SubMeshCount = count;
            Rule = new EchoTagColorRule { Color = color };
            EffectGroup = renderer;
            Reveal = null;
        }

        public EchoRenderItem(Renderer renderer, EchoTagColorRule rule, int count, Object effectGroup = null, EchoRevealState reveal = null)
        {
            Renderer = renderer;
            Color = rule.Color;
            SubMeshCount = count;
            Rule = rule;
            EffectGroup = effectGroup ? effectGroup : renderer;
            Reveal = reveal;
        }
    }
}
