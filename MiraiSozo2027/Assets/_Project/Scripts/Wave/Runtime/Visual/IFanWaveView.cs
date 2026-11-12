namespace Aura.Wave
{
    /// @brief 扇（近距離）の 1 発の見た目の部品が持つ口。WaveEmitter と同じ GameObject に付けると、WaveEmitter が呼ぶ
    /// 見た目は当たり判定に関わらない。輪・パーティクルなど、描き方は部品ごとに自由に作る（今の見た目は FanRingView）
    /// ・1 つも付いていなければ、WaveEmitter が再生を始めるときに FanRingView を足す（無効にした物も「付いている」に数える）
    /// ・無効にした部品は呼ばれない。いくつ付けてもよく、付いている物すべてが呼ばれる
    /// ・再生中に足した部品は、WaveEmitter.RefreshViews を呼ぶまで呼ばれない
    public interface IFanWaveView
    {
        /// @brief 1 発撃った瞬間に呼ばれる。撃った後の動き（輪を進める、パーティクルを飛ばすなど）は、部品が自分で進める
        /// 当たりとそろえるときは、shot.GetFrontDistance(Time.time) で波面の位置を取る
        /// @param shot 撃った 1 発の情報
        void ShowFanShot(in FanShotInfo shot);

        /// @brief WaveEmitter を無効にしたときに呼ばれる。進んでいる 1 発の見た目を、すべて消す
        void ClearFanShots();
    }
}
