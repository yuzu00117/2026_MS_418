// ONと波状OFFで共用する時間→距離の計算。フレームレートに依存させない。
using UnityEngine;

namespace Echo.Echolocation
{
    /// <summary>一定速度の伝播距離を求める純粋関数。</summary>
    public static class EchoPropagation
    {
        /// <summary>経過秒を最大半径までの距離へ変換する。duration=0は即時。</summary>
        public static float GetDistance(float radius, float duration, float elapsed) => Mathf.Max(0, radius) * (duration <= 0 ? 1 : Mathf.Clamp01(elapsed / duration));
    }
}
