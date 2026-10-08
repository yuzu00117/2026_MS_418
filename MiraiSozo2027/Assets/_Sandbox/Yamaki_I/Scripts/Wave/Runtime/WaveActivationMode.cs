namespace Aura.Wave
{
    /// @brief WaveReceiver が、溜まったエネルギーでどう起動するか
    public enum WaveActivationMode
    {
        Latch,      ///< 一度起動したら起動したまま（扉を開けるなど）
        Toggle,     ///< 起動するたびに ON と OFF が入れ替わる（スイッチ）
        Sustain,    ///< 起動した後、エネルギーが 0 に減るまで ON（当てている間だけ動くリフトなど）
    }
}
