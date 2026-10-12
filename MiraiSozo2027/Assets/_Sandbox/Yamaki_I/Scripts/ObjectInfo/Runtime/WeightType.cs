using UnityEngine;

namespace Aura.ObjectInfo
{
    /// @brief 重さの種類（軽い物、重い物など）。シートの O2「重さ・動かしやすさ」
    /// 念力で動かせる物（TelekinesisTarget）が、これを選ぶ。値を直すと、この種類を選んだ物すべてに効く
    [CreateAssetMenu(fileName = "SO_Weight_New", menuName = "Aura/Object Info/Weight Type")]
    public sealed class WeightType : ScriptableObject
    {
        [Tooltip("画面（HUD など）に出す名前。空なら、このアセットのファイル名を使う")]
        [SerializeField] private string _displayName;                       ///< 画面に出す名前。空ならアセットの名前

        [Tooltip("重さ（kg）。0.01 以上。念力で持ち上げたときや、ぶつけたときの重さとして使う")]
        [SerializeField, Min(0.01f)] private float _mass = 1.0f;            ///< 重さ（kg）

        [Tooltip("念力での動かしやすさ（0.05〜1）。1 がいちばん動かしやすい。小さいほど、念力で動かしたときに重く、遅れてついてくる\n" +
                 "どう効かせるか（動く速さに掛けるなど）は、念力の機能の側で決める")]
        [SerializeField, Range(0.05f, 1.0f)] private float _mobility = 1.0f; ///< 念力での動かしやすさ（0.05〜1）

        /// @brief 画面に出す名前
        public string DisplayName => string.IsNullOrEmpty(_displayName) ? name : _displayName;

        /// @brief 重さ（kg）
        public float Mass => _mass;

        /// @brief 念力での動かしやすさ（0.05〜1）。1 がいちばん動かしやすい
        public float Mobility => _mobility;
    }
}
