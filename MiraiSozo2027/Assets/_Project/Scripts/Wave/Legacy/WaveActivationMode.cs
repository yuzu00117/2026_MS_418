using UnityEngine;

namespace Aura.Wave
{
    /// @brief WaveReceiver が、溜まったエネルギーでどう起動するか
    public enum WaveActivationMode
    {
        [InspectorName("起動したまま")]
        Latch,      ///< 一度起動したら起動したまま（扉を開けるなど）
        [InspectorName("オンとオフを交互に")]
        Toggle,     ///< 起動するたびに ON と OFF が入れ替わる（スイッチ）
        [InspectorName("減りきると止まる")]
        Sustain,    ///< 起動した後、エネルギーが 0 に減るまで ON（当てている間だけ動くリフトなど）
    }
}
