using System;
using Aura.Inspector;
using Aura.Wave;
using UnityEngine;

namespace Aura.ObjectInfo
{
    /// @brief 弱点の 1 行分。波の種類と、その種類の効き目（倍率）。WeaknessType の Entries に並べる
    [Serializable]
    public struct WeaknessEntry
    {
        [InspectorLabel("波の種類")]
        [Tooltip("弱点になる波の種類（電磁波/超音波）")]
        [SerializeField] private WaveKind _waveKind;            ///< 弱点になる波の種類

        [InspectorLabel("効き目（倍率）")]
        [Tooltip("この種類の波の効き目（倍率）。2 なら 2 倍効く。1 なら普通、0 なら効かない")]
        [SerializeField, Min(0.0f)] private float _multiplier;  ///< 効き目（倍率）

        // 旧: WaveType のアセットを直接選んでいた欄。WeaknessType が読み込んだときに「波の種類」へ写して空にする（移行用に隠して残す）
        [SerializeField, HideInInspector] private WaveType _waveType;

        /// @brief 弱点の 1 行分を作る
        /// @param waveKind 弱点になる波の種類
        /// @param multiplier 効き目（倍率）。0 以上
        public WeaknessEntry(WaveKind waveKind, float multiplier)
        {
            _waveKind = waveKind;
            _multiplier = Mathf.Max(0.0f, multiplier);
            _waveType = null;
        }

        /// @brief 弱点になる波の種類
        public WaveKind WaveKind => _waveKind;

        /// @brief 効き目（倍率）。1 なら普通
        public float Multiplier => _multiplier;

        /// @brief 旧欄（WaveType のアセット）が残っていれば、「波の種類」へ写した 1 行を作る
        /// 対応表（SO_WaveTypeInfo）に無いアセットは、警告を出して捨てる（「波の種類」は今の値のまま）
        /// @param outMigrated 写した 1 行（写せなければ、この 1 行のまま）
        /// @return 写したら true
        internal bool TryMigrate(out WeaknessEntry outMigrated)
        {
            outMigrated = this;
            if (_waveType == null || WaveTypeInfo.Instance == null)
            {
                return false;
            }
            if (WaveTypeInfo.TryGetKind(_waveType, out WaveKind kind))
            {
                outMigrated._waveKind = kind;
            }
            else
            {
                Debug.LogWarning($"WeaknessEntry: 対応表（SO_WaveTypeInfo）に無い波の種類 {_waveType.name} が旧欄にあったので捨てた。「波の種類」は今の値のまま");
            }
            outMigrated._waveType = null;
            return true;
        }
    }
}
