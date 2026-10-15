using UnityEngine;

namespace Aura.ObjectInfo
{
    /// @brief ビームを出したもの。シートの B5「出したもの」。BeamSourceInfo の Source Kind に入る
    /// 受け手の反応には使わない（受け手は波の種類だけで反応する）
    public enum BeamSourceKind
    {
        [InspectorName("未設定")]
        Unknown = 0,  ///< まだ入っていない（ビームを出す機能の側が入れる前）
        [InspectorName("ギミック")]
        Gimmick = 1,  ///< フィールドのギミック
        [InspectorName("敵")]
        Enemy = 2,    ///< 敵
        [InspectorName("プレイヤー")]
        Player = 3,   ///< プレイヤー（波発射）
    }
}
