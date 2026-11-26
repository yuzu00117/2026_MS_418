using UnityEngine;

namespace Aura.Gimmick
{
    /// @brief WaveRider が波に乗れなくなったとき（波が遮られた、止まった、スイッチが止まった）の動き
    public enum WaveRiderLostWaveMode
    {
        [InspectorName("その場で止まる")]
        StopInPlace = 0,

        [InspectorName("慣性で進んでから止まる")]
        Inertia = 1,

        [InspectorName("初期位置へ戻る")]
        ReturnToStart = 2,

        [InspectorName("経路探索で初期位置へ戻る")]
        ReturnByNavMesh = 3,
    }
}
