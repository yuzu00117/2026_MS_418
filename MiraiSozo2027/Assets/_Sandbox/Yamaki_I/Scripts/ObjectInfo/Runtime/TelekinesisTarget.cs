using UnityEngine;

namespace Aura.ObjectInfo
{
    /// @brief 念力で動かせる物の印。シートの O1「念力で動かせるか」と O2「重さ・動かしやすさ」
    /// これが付いている物だけを、念力で動かせる。重さは WeightType（種類）で選ぶ
    /// 動かす処理は持たない（念力の機能の側が、これを見て動かす）
    [DisallowMultipleComponent]
    public sealed class TelekinesisTarget : MonoBehaviour, IObjectInfo
    {
        [Tooltip("重さの種類（WeightType のアセット）。軽い物、重い物など\n" +
                 "空なら、念力の機能の側が決める既定の重さで扱う")]
        [SerializeField] private WeightType _weightType;  ///< 重さの種類

        /// @brief 重さの種類。空のこともある
        public WeightType WeightType => _weightType;

        /// @brief 今、念力で動かせるか。このコンポーネントが無効か、GameObject が非アクティブなら false
        public bool IsMovable => enabled && gameObject.activeInHierarchy;

        // 有効・無効が変わると ObjectInfoLookup の結果が変わるので、覚えた結果を捨ててもらう
        // （OnEnable があるので、Inspector に有効と無効のチェックボックスも出る。無効にすると、動かせない扱いになる）
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
