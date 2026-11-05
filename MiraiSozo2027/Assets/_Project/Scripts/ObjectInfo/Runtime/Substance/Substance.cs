using Aura.Inspector;
using UnityEngine;

namespace Aura.ObjectInfo
{
    /// @brief その物の素材。シートの O6「波を跳ね返すか止めるか」と O7「エコーをさえぎるか通すか」
    /// 素材の種類（SubstanceType）を選ぶ。この GameObject と子のコライダー・見た目（Renderer）に効く
    /// 取り出すときは ObjectInfoLookup.TryGetSubstanceType を使う
    [DisallowMultipleComponent]
    public sealed class Substance : MonoBehaviour, IObjectInfo
    {
        [InspectorLabel("素材の種類")]
        [Tooltip("素材の種類（SubstanceType のアセット）。金属、コンクリート、ガラスなど\n" +
                 "これで、波を跳ね返すか止めるかと、エコーをさえぎるか通すかが決まる\n" +
                 "この GameObject と子に効く。子にも Substance があれば、いちばん近いものが効く\n" +
                 "空にするか、コンポーネントを無効にすると、無いものとして扱う（親に Substance があれば、そちらが効く）")]
        [SerializeField] private SubstanceType _type;  ///< 素材の種類

        /// @brief 素材の種類。空のこともある
        public SubstanceType Type => _type;

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

        // Inspector で素材の種類を変えると TryGetSubstanceType の結果が変わるので、覚えた結果を捨ててもらう
        private void OnValidate()
        {
            ObjectInfoCache.MarkDirty();
        }
    }
}
