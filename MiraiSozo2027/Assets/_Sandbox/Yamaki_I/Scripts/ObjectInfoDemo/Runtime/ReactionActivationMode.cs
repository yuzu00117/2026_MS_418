namespace Aura.ObjectInfoDemo
{
    /// @brief ReactionTrigger が、溜まりきったときにどう起動するか。ReactionTrigger の Mode で選ぶ
    /// 番号は Aura.Wave の WaveActivationMode と同じ（シーンに保存した値をそのまま読むため。番号は変えないこと）
    public enum ReactionActivationMode
    {
        Latch = 0,    ///< 一度起動したら、ずっと起動したまま（扉を開けるなど）
        Toggle = 1,   ///< 起動するたびに ON と OFF が入れ替わる（スイッチ、電気など）
        Sustain = 2,  ///< 起動した後、溜まった分が 0 に減ると止まる（当てている間だけ動くリフトなど）
    }
}
