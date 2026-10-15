using UnityEngine;

namespace Aura.ObjectInfoDemo
{
    /// @brief ビームの点が、ビームのどこにあるか
    public enum BeamPointKind
    {
        [InspectorName("使っていない")]
        Unused = 0,      ///< 今はビームに使っていない（作り置き）
        [InspectorName("出る位置")]
        Start = 1,       ///< 出る位置（List の最初）。掴めない
        [InspectorName("途中の点")]
        Middle = 2,      ///< 途中の点。掴める
        [InspectorName("跳ね返った位置")]
        Reflection = 3,  ///< 跳ね返った位置。掴めない
        [InspectorName("終わりの位置")]
        End = 4,         ///< 終わりの位置（List の最後）。物に当たって止まった位置か、届く距離の端。掴める
    }
}
