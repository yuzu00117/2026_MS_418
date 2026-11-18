using UnityEngine;

namespace Aura.Gimmick
{
    /// @brief WaveRider が波の線の終点に着いたときの動き
    public enum WaveRiderEndMode
    {
        [InspectorName("終点で止まる")]
        Stop = 0,

        [InspectorName("折り返す")]
        PingPong = 1,
    }
}
