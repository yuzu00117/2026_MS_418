using UnityEngine;
using UnityEngine.UI;

namespace MiraiSozo.Options
{
    /// <summary>開く演出の間だけ、波紋の内側に描画を限定するUI要素。</summary>
    public sealed class OptionMenuRevealItem
    {
        private readonly Graphic _graphic;

        public OptionMenuRevealItem(Graphic graphic, Material revealMaterial)
        {
            _graphic = graphic;
            graphic.material = revealMaterial;
        }

        /// <summary>標準マテリアルを使う要素だけを対象にし、独自マテリアルの見た目は変えない。</summary>
        public static bool CanReveal(Graphic graphic) => graphic.material == graphic.defaultMaterial;

        public void Restore()
        {
            // nullを入れるとGraphicの標準マテリアルへ戻る。
            if (_graphic != null) _graphic.material = null;
        }
    }
}
