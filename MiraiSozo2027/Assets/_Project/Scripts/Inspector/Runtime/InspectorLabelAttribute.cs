using System;
using UnityEngine;

namespace Aura.Inspector
{
    /// @brief Inspector の欄に、変数名の代わりに出す名前（日本語）を決める
    /// 例: [InspectorLabel("撃ち方")] [SerializeField] private WaveFireMode _fireMode;
    /// [Tooltip] の説明文はそのまま出て、その最後に元の変数名が添えられる。保存される値や動きは何も変わらない
    /// 配列やリストに付けると、1 つ 1 つの要素ではなく、一覧の見出しの名前になる
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class InspectorLabelAttribute : PropertyAttribute
    {
        /// @brief 欄を描く順番。Min や Range などのほかの描き方より外側で、先に名前を差し替えるため、いちばん先にする
        private const int DrawOrder = -1000;

        /// @brief Inspector に出す名前を決める
        /// @param text Inspector に出す名前
        public InspectorLabelAttribute(string text) : base(true)
        {
            Text = text;
            order = DrawOrder;
        }

        /// @brief Inspector に出す名前
        public string Text { get; }
    }
}
