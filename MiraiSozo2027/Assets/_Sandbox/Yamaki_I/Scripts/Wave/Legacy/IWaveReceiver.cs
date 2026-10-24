namespace Aura.Wave
{
    /// @brief 波を受ける物の窓口。当たったコライダーか、その親の GameObject に付ける
    /// 決まった反応で足りるなら WaveReceiver を使い、独自の反応はこれを実装して書く
    public interface IWaveReceiver
    {
        /// @brief 波が当たったときに呼ばれる
        /// @param hit 当たりの情報
        /// @return 波を受け付けたら true。反応しない種類なら false
        bool ReceiveWave(in WaveHit hit);
    }
}
