namespace Aura.ObjectInfoDemo
{
    /// @brief ビームの点が、ビームのどこにあるか
    public enum BeamPointKind
    {
        Unused = 0,      ///< 今はビームに使っていない（作り置き）
        Start = 1,       ///< 出る位置（List の最初）。掴めない
        Middle = 2,      ///< 途中の点。掴める
        Reflection = 3,  ///< 跳ね返った位置。掴めない
        End = 4,         ///< 終わりの位置（List の最後）。物に当たって止まった位置か、届く距離の端。掴める
    }
}
