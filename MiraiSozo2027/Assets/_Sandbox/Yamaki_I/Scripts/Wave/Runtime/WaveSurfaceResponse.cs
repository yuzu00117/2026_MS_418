using UnityEngine;

namespace Aura.Wave
{
    /// @brief 波が当たったときの、物の扱い（跳ね返すか止めるか）。WaveSurface の Response で選ぶ
    /// シーンには番号で保存されるので、新しい値は最後に足す
    public enum WaveSurfaceResponse
    {
        [InspectorName("跳ね返す")]
        Reflect,  ///< 跳ね返す（鏡、金属の板など）
        [InspectorName("止める")]
        Absorb,   ///< 吸い込んで止める（布、吸音材など）
    }
}
