using UnityEngine;

namespace Aura.ObjectInfo
{
    /// @brief 素材が波（ビーム）を受けたときの扱い。SubstanceType の Wave Response で選ぶ
    /// アセットには番号で保存されるので、番号を変えず、新しい値は最後に足す
    public enum SubstanceWaveResponse
    {
        [InspectorName("止める")]
        Absorb = 0,   ///< 止める（コンクリート、吸音材など）
        [InspectorName("跳ね返す")]
        Reflect = 1,  ///< 跳ね返す（鏡、金属など）
    }
}
