using UnityEngine;

namespace Aura.ObjectInfoDemo
{
    /// @brief ビームの作り方。InfoBeamEmitter の Beam Shape で選ぶ
    public enum BeamShape
    {
        [InspectorName("真っすぐ")]
        Straight = 0,    ///< 今までの真っすぐなビーム。点は作らず、念力でも曲がらない（点と線を入れる前と同じ動き。比べる用）
        [InspectorName("点と線")]
        PointChain = 1,  ///< 一定の間隔で並べた点と、点同士をつなぐ直線でできたビーム。点を念力で掴んで動かすと曲がる
    }
}
