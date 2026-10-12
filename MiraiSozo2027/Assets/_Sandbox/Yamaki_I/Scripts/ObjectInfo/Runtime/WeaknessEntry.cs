using System;
using Aura.Wave;
using UnityEngine;

namespace Aura.ObjectInfo
{
    /// @brief 弱点の 1 行分。波の種類と、その種類の効き目（倍率）。WeaknessType の Entries に並べる
    [Serializable]
    public struct WeaknessEntry
    {
        [Tooltip("弱点になる波の種類（WaveType のアセット）")]
        [SerializeField] private WaveType _waveType;            ///< 弱点になる波の種類

        [Tooltip("この種類の波の効き目（倍率）。2 なら 2 倍効く。1 なら普通、0 なら効かない")]
        [SerializeField, Min(0.0f)] private float _multiplier;  ///< 効き目（倍率）

        /// @brief 弱点の 1 行分を作る
        /// @param waveType 弱点になる波の種類
        /// @param multiplier 効き目（倍率）。0 以上
        public WeaknessEntry(WaveType waveType, float multiplier)
        {
            _waveType = waveType;
            _multiplier = Mathf.Max(0.0f, multiplier);
        }

        /// @brief 弱点になる波の種類
        public WaveType WaveType => _waveType;

        /// @brief 効き目（倍率）。1 なら普通
        public float Multiplier => _multiplier;
    }
}
