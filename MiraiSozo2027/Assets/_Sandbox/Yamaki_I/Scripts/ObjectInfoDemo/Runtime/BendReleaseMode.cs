namespace Aura.ObjectInfoDemo
{
    /// @brief 念力で掴んだ点を離した後の、ビームの曲がりの扱い。InfoBeamEmitter の Bend Release Mode で選ぶ
    public enum BendReleaseMode
    {
        Keep = 0,               ///< 離した位置に点が残り、ビームを止めるまで曲がったまま
        HoldThenReturn = 1,     ///< 離した後、決めた時間だけ曲がったままにし、その後決めた時間をかけて真っすぐに戻る
        ReturnImmediately = 2,  ///< 離した瞬間に真っすぐに戻る
    }
}
