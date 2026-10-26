using UnityEngine;

namespace Aura.Wave
{
    /// @brief 波の撃ち方
    public enum WaveFireMode
    {
        [InspectorName("単発")]
        Pulse,  ///< 単発。波の塊を飛ばす
        [InspectorName("連続（ビーム）")]
        Beam,   ///< 連続。押している間、ビームのように照射し続ける
    }
}
