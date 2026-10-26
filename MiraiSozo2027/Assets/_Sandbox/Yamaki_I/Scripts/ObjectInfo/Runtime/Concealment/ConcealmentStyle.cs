using UnityEngine;

namespace Aura.ObjectInfo
{
    /// @brief 隠れ方。Concealment の Style で選ぶ
    /// シーンには番号で保存されるので、番号を変えず、新しい値は最後に足す
    public enum ConcealmentStyle
    {
        [InspectorName("見えない")]
        Invisible = 0,  ///< 見えない
        [InspectorName("化けている")]
        Disguised = 1,  ///< 別の見た目に化けている
    }
}
