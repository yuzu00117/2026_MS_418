using UnityEngine;

namespace Aura.Gimmick
{
    /// @brief 動くギミック（GimmickMover、GimmickRotator）の、起動している間の動き方。「動き方」で選ぶ
    /// シーンやプレハブには番号で保存されるので、番号を変えず、新しい値は最後に足す
    public enum GimmickMotionMode
    {
        [InspectorName("起動で動く")]
        Once = 0,      ///< 起動した位置まで動いて止まる（今までどおり）

        [InspectorName("起動中は往復する")]
        PingPong = 1,  ///< 起動している間、元の位置と起動した位置をずっと往復する
    }
}
