using System.Collections.Generic;
using UnityEngine;

namespace Aura.Wave
{
    /// @brief 1 フレームのビーム（点と線）の形。WaveEmitter が当たり判定の後に作り、ビームの見た目の部品（IBeamWaveView）に渡す
    public readonly struct BeamWaveFrame
    {
        /// @param corners 実体の線の点
        /// @param fadingCorners corners と同じ数。跳ね返った位置なら true
        /// @param up 出る位置の上の向き
        /// @param type 波の種類
        public BeamWaveFrame(IReadOnlyList<Vector3> corners, IReadOnlyList<bool> fadingCorners, Vector3 up, WaveType type)
        {
            Corners = corners;
            FadingCorners = fadingCorners;
            Up = up;
            Type = type;
        }

        /// @brief 実体の線の点。出る位置から順に、間の点・跳ね返った位置・支点（掴んだ点）が並び、最後が終わりの位置
        /// 当たり判定は、隣どうしの点を結ぶ直線で行っている。中身は次のフレームで変わるので、このフレームの間だけ使う
        public IReadOnlyList<Vector3> Corners { get; }

        /// @brief Corners と同じ数。true の点は跳ね返った位置（線の見た目では、ここの前後で揺れを 0 にしている）
        public IReadOnlyList<bool> FadingCorners { get; }

        /// @brief 出る位置の上の向き（線の見た目では、波が揺れる面を決めるのに使っている）
        public Vector3 Up { get; }

        /// @brief 波の種類（色など）
        public WaveType Type { get; }
    }
}
