using UnityEngine;

namespace Aura.Wave
{
    /// @brief 円すいの輪の見た目（ConeRingView）の、泡の重ね方の選択肢
    public enum ConeRingBlend
    {
        /// 光を足す（加算）。暗い背景では、泡が重なる管の縁が光る。明るい背景の上では、ほとんど見えない（「もや」を 0 にすると、2026-11-10 の最初の見た目）
        [InspectorName("光を足す（暗い場面向け）")]
        Additive = 0,

        /// 色を重ねる（事前乗算の半透明）。泡が後ろを「泡の濃さ」だけ隠して色を乗せるので、明るい背景の上でも見える
        [InspectorName("色を重ねる（明るい場面でも見える）")]
        Blend = 1,
    }
}
