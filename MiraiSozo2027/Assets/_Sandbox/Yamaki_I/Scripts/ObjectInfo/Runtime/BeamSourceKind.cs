namespace Aura.ObjectInfo
{
    /// @brief ビームを出したもの。シートの B5「出したもの」。BeamSourceInfo の Source Kind に入る
    /// 受け手の反応には使わない（受け手は波の種類だけで反応する）
    public enum BeamSourceKind
    {
        Unknown = 0,  ///< まだ入っていない（ビームを出す機能の側が入れる前）
        Gimmick = 1,  ///< フィールドのギミック
        Enemy = 2,    ///< 敵
        Player = 3,   ///< プレイヤー（波発射）
    }
}
