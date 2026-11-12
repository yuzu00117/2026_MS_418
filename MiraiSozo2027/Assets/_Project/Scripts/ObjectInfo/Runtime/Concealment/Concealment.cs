using Aura.Inspector;
using UnityEngine;

namespace Aura.ObjectInfo
{
    /// @brief 隠れている物の情報。シートの O8「隠れ方」と O9「本当の姿」
    /// 見つける処理と、見た目を切り替える処理は持たない（エコーロケーションの機能の側が、これを見て切り替える）
    [DisallowMultipleComponent]
    public sealed class Concealment : MonoBehaviour, IObjectInfo
    {
        [InspectorLabel("隠れ方")]
        [Tooltip("隠れ方\n" +
                 "見えない: 見えない\n" +
                 "化けている: 別の見た目に化けている（「化けている見た目」の見た目で見えている）")]
        [SerializeField] private ConcealmentStyle _style;  ///< 隠れ方

        [InspectorLabel("本当の姿")]
        [Tooltip("本当の姿。見つかったときに見せる見た目（子の GameObject）。「見えない」でも「化けている」でも使う\n" +
                 "空なら、見つかったときに何を見せるかは、エコーロケーションの機能の側が決める")]
        [SerializeField] private GameObject _trueForm;     ///< 見つかったときに見せる見た目

        [InspectorLabel("化けている見た目")]
        [Tooltip("化けている見た目（子の GameObject）。見つかる前に見えている姿\n" +
                 "「隠れ方」が「化けている」のときだけ使う（Inspector にも、そのときだけ出る）\n" +
                 "空なら、化けている見た目は無い扱い（見つかる前の見え方は、エコーロケーションの機能の側が決める）")]
        [SerializeField] private GameObject _disguiseForm; ///< 見つかる前に見えている、化けた見た目

        /// @brief 隠れ方
        public ConcealmentStyle Style => _style;

        /// @brief 本当の姿（見つかったときに見せる見た目）。空のこともある
        public GameObject TrueForm => _trueForm;

        /// @brief 化けている見た目。Style が Disguised でなければ null
        public GameObject DisguiseForm => _style == ConcealmentStyle.Disguised ? _disguiseForm : null;

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
