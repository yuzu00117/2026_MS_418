using UnityEngine;

namespace Aura.ObjectInfo
{
    /// @brief 波の種類の選択肢。電磁波と超音波の 2 種で確定（今後も増やさない見込み）
    /// 対応する WaveType のアセットは、WaveTypeInfo（Resources/SO_WaveTypeInfo）の枠で割り当てる
    public enum WaveKind
    {
        [InspectorName("電磁波")]
        Electromagnetic = 0,

        [InspectorName("超音波")]
        Ultrasound = 1,
    }
}
