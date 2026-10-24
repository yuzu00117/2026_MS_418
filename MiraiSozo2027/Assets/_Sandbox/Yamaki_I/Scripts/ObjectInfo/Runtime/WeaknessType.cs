using System;
using System.Collections.Generic;
using Aura.Inspector;
using Aura.Wave;
using UnityEngine;

namespace Aura.ObjectInfo
{
    /// @brief 弱点の種類（電磁波に弱い、など）。シートの O5「弱点の波の種類・効き目」
    /// 弱い部位に付けた WeakPoint が、これを選ぶ。値を直すと、この種類を選んだ物すべてに効く
    [CreateAssetMenu(fileName = "SO_Weakness_New", menuName = "Aura/Object Info/Weakness Type")]
    public sealed class WeaknessType : ScriptableObject
    {
        /// @brief 普通の効き目（倍率）。これより大きい種類を「弱点」とみなす
        public const float NormalMultiplier = 1.0f;

        [InspectorLabel("画面に出す名前")]
        [Tooltip("画面（HUD など）に出す名前。空なら、このアセットのファイル名を使う")]
        [SerializeField] private string _displayName;                       ///< 画面に出す名前。空ならアセットの名前

        [InspectorLabel("波ごとの効き目")]
        [Tooltip("弱点の一覧。波の種類ごとの効き目（倍率）。同じ種類が 2 回あれば、上の行を使う")]
        [SerializeField] private WeaknessEntry[] _entries;                  ///< 波の種類ごとの効き目

        [InspectorLabel("ほかの波の効き目（倍率）")]
        [Tooltip("一覧にない種類の波の効き目（倍率）。1 なら普通に効く")]
        [SerializeField, Min(0.0f)] private float _otherMultiplier = NormalMultiplier;  ///< 一覧にない種類の効き目

        /// @brief 画面に出す名前
        public string DisplayName => string.IsNullOrEmpty(_displayName) ? name : _displayName;

        /// @brief 波の種類ごとの効き目の一覧
        public IReadOnlyList<WeaknessEntry> Entries => _entries ?? Array.Empty<WeaknessEntry>();

        /// @brief 一覧にない種類の波の効き目（倍率）
        public float OtherMultiplier => _otherMultiplier;

        /// @brief その種類の波の効き目（倍率）を返す。一覧になければ Other Multiplier
        /// @param kind 当たった波の種類
        /// @return 効き目（倍率）
        public float GetMultiplier(WaveKind kind)
        {
            if (_entries != null)
            {
                for (int i = 0; i < _entries.Length; ++i)
                {
                    if (_entries[i].WaveKind == kind)
                    {
                        return _entries[i].Multiplier;
                    }
                }
            }
            return _otherMultiplier;
        }

        /// @brief その種類の波の効き目（倍率）を返す（WaveType のアセットで調べる）
        /// 対応表（SO_WaveTypeInfo）に無いアセットは、一覧にない扱い（Other Multiplier）
        /// @param type 当たった波の種類のアセット
        /// @return 効き目（倍率）
        public float GetMultiplier(WaveType type)
        {
            return WaveTypeInfo.TryGetKind(type, out WaveKind kind) ? GetMultiplier(kind) : _otherMultiplier;
        }

        /// @brief その種類の波が弱点か（普通より効くか）
        /// @param kind 波の種類
        /// @return 効き目が普通（1）より大きければ true
        public bool IsWeakTo(WaveKind kind)
        {
            return GetMultiplier(kind) > NormalMultiplier;
        }

        /// @brief その種類の波が弱点か（WaveType のアセットで調べる）
        /// @param type 波の種類のアセット
        /// @return 効き目が普通（1）より大きければ true
        public bool IsWeakTo(WaveType type)
        {
            return GetMultiplier(type) > NormalMultiplier;
        }

        private void OnEnable()
        {
            MigrateEntries();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (MigrateEntries())
            {
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }
#endif

        /// @brief 旧欄（WaveType のアセット）が残っている行があれば、「波の種類」へ写す
        /// @return 写した行があれば true
        private bool MigrateEntries()
        {
            if (_entries == null)
            {
                return false;
            }
            bool hasMigrated = false;
            for (int i = 0; i < _entries.Length; ++i)
            {
                if (_entries[i].TryMigrate(out WeaknessEntry migrated))
                {
                    _entries[i] = migrated;
                    hasMigrated = true;
                }
            }
            return hasMigrated;
        }
    }
}
