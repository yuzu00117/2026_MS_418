namespace Aura.Wave
{
    /// @brief ビーム（点と線）の見た目の部品が持つ口。WaveEmitter と同じ GameObject に付けると、WaveEmitter が呼ぶ
    /// 見た目は当たり判定に関わらない。線・パーティクルなど、描き方は部品ごとに自由に作る（今の見た目は BeamLineView）
    /// ・1 つも付いていなければ、WaveEmitter が再生を始めるときに BeamLineView を足す（無効にした物も「付いている」に数える）
    /// ・無効にした部品は呼ばれない。いくつ付けてもよく、付いている物すべてが呼ばれる
    /// ・再生中に足した部品は、WaveEmitter.RefreshViews を呼ぶまで呼ばれない
    public interface IBeamWaveView
    {
        /// @brief ビームを出している間、毎フレーム呼ばれる（WaveEmitter の LateUpdate で、当たり判定の後）
        /// @param frame このフレームのビームの形
        void DrawBeam(in BeamWaveFrame frame);

        /// @brief ビームを止めたときに 1 回呼ばれる（引き金を離した、撃ち方を変えた、WaveEmitter を無効にしたなど）
        void HideBeam();
    }
}
