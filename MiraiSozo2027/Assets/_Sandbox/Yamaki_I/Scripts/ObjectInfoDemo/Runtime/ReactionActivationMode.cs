using UnityEngine;

namespace Aura.ObjectInfoDemo
{
    /// @brief ReactionTrigger が、溜まりきったときにどう起動するか。ReactionTrigger の Mode で選ぶ
    /// 番号は Aura.Wave の WaveActivationMode と同じ（シーンに保存した値をそのまま読むため。番号は変えないこと）
    public enum ReactionActivationMode
    {
        [InspectorName("起動したまま")]
        Latch = 0,    ///< 一度起動したら、ずっと起動したまま（扉を開けるなど）
        [InspectorName("オンとオフを交互に")]
        Toggle = 1,   ///< 起動するたびに ON と OFF が入れ替わる（スイッチ、電気など）
        [InspectorName("減りきると止まる")]
        Sustain = 2,  ///< 起動した後、溜まった分が 0 に減ると止まる（当てている間だけ動くリフトなど）
    }
}
