using UnityEngine;

namespace Aura.DebugTools
{
    /// @brief 色分けした文字の部品を並べた、1 行ぶんの文字
    internal sealed class TextLine
    {
        public const int MaxSegments = 4;                           ///< 1 行を作る、色分けした文字の部品の最大数

        public string[] Texts { get; } = new string[MaxSegments];   ///< 部品の文字
        public Color[] Colors { get; } = new Color[MaxSegments];    ///< 部品の色
        public int[] SpacesBefore { get; } = new int[MaxSegments];  ///< 部品の前に空ける、半角スペースの数
        public float[] Widths { get; } = new float[MaxSegments];    ///< 部品の幅（描く前に測る）
        public int Count { get; private set; }  ///< 部品の数
        public bool IsGap { get; set; }         ///< 文字がなく、あきだけの行か
        public float TotalWidth { get; set; }   ///< 部品を並べた全体の幅（背景の余白は含まない）

        /// @brief 中身を空にする。行は毎回 new せず、空にして使い回す（毎フレームのメモリ確保を避けるため）
        public void Reset()
        {
            Count = 0;
            IsGap = false;
            TotalWidth = 0.0f;
        }

        /// @brief 部品を 1 つ足す。上限（MaxSegments）を超えた分は捨てる。配列を作り直さずに済むよう、数を固定している
        public void Add(string text, Color color, int spaces = 0)
        {
            if (Count >= MaxSegments)
            {
                return;
            }

            Texts[Count] = text;
            Colors[Count] = color;
            SpacesBefore[Count] = spaces;
            Count++;
        }
    }
}
