using UnityEngine;

namespace Aura.Wave
{
    /// @brief ビームの見た目（BeamLineView）の見せ方。「見せ方」で選ぶ
    /// シーンやプレハブには番号で保存されるので、番号を変えず、新しい値は最後に足す
    public enum BeamLineVisibility
    {
        [InspectorName("いつも見せる")]
        Always = 0,           ///< 出している間ずっと見せる（今までどおり）

        [InspectorName("見える範囲の中だけ")]
        InRevealVolumes = 1,  ///< 見せる範囲（WaveRevealSource）の中にある部分だけ見せる。範囲が無ければ見せない（エコーロケーション中だけ波が見える、など）

        [InspectorName("範囲に触れたら全体を見せる")]
        WholeWhenTouched = 2,  ///< 線のどこかが見せる範囲に触れたら、線の全体を見せる。触れなくなってからも「触れた後も見せる秒数」の間は見せる
    }
}
