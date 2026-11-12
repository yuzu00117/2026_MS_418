using Aura.Inspector;
using Aura.Wave;
using UnityEngine;

namespace Aura.ObjectInfo
{
    /// @brief 弱点ポイントの印。シートの O10「弱点ポイントか」と O5「弱点の波の種類・効き目」
    /// 敵などの弱い部位（コライダーが付いた子など）に付ける。付いていれば、エコーで見つかる弱点になる
    /// どの波に弱いかは WeaknessType（種類）で選ぶ。ダメージの処理は持たない
    [DisallowMultipleComponent]
    public sealed class WeakPoint : MonoBehaviour, IObjectInfo
    {
        [InspectorLabel("弱点の種類")]
        [Tooltip("弱点の種類（WeaknessType のアセット）。電磁波に弱い、など\n" +
                 "空なら、どの波も普通の効き目（倍率 1）で扱う")]
        [SerializeField] private WeaknessType _weaknessType;  ///< 弱点の種類

        /// @brief 弱点の種類。空のこともある
        public WeaknessType WeaknessType => _weaknessType;

        /// @brief その種類の波の効き目（倍率）を返す。弱点の種類が空なら 1
        /// @param type 当たった波の種類
        /// @return 効き目（倍率）
        public float GetMultiplier(WaveType type)
        {
            return _weaknessType != null ? _weaknessType.GetMultiplier(type) : WeaknessType.NormalMultiplier;
        }

        // 有効・無効が変わると ObjectInfoLookup の結果が変わるので、覚えた結果を捨ててもらう
        // （OnEnable があるので、Inspector に有効と無効のチェックボックスも出る。無効にすると、無いものとして扱う）
        private void OnEnable()
        {
            ObjectInfoCache.MarkDirty();
        }

        private void OnDisable()
        {
            ObjectInfoCache.MarkDirty();
        }
    }
}
