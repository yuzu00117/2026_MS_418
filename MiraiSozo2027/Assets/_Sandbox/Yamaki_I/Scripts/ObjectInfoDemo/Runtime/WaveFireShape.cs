using UnityEngine;

namespace Aura.ObjectInfoDemo
{
    /// @brief WaveEmitter の撃ち方の選択肢。長射程のビーム（点と線）と、近距離・広範囲の扇
    public enum WaveFireShape
    {
        [InspectorName("ビーム（点と線）")]
        Beam = 0,

        [InspectorName("扇（近距離）")]
        Fan = 1,
    }
}
