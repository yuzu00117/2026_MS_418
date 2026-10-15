namespace Aura.ObjectInfoDemo
{
    /// @brief 念力で掴んだ点（支点）で、点と線のビームをどう曲げるか。InfoBeamEmitter の Bend Mode で選ぶ
    public enum BendMode
    {
        Bump = 0,      ///< 支点の前後 Bend Range だけが山のように曲がり、その外は元の線に戻る（当たる先はほぼ変わらず、途中の物を避ける）
        Redirect = 1,  ///< 支点の手前 Bend Range から円の一部のように曲がって支点を通り、その先は曲がった向きのまま真っすぐ進む（当たる先が変わる）
    }
}
