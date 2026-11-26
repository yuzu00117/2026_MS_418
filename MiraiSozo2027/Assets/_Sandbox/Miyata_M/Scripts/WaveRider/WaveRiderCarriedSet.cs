using System.Collections.Generic;
using UnityEngine;

namespace Aura.Gimmick
{
    /// @brief この物理ステップで、どれかの WaveRider がもう運んだ物を覚える
    /// 2 つのライダーにまたがって乗った物を、1 ステップに 2 回運ばない（先に運んだライダーの動きだけを使う）
    /// 物理ステップ（Time.fixedTime）が変わったら空にする
    public static class WaveRiderCarriedSet
    {
        private static readonly HashSet<Rigidbody> _carried = new HashSet<Rigidbody>();  ///< この物理ステップで運んだ物
        private static float _stepTime = -1.0f;   ///< _carried を集めている物理ステップの時刻（Time.fixedTime）

        /// ドメインの再読み込みを切った再生でも、前の再生の物を持ち越さない
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            _carried.Clear();
            _stepTime = -1.0f;
        }

        /// @brief この物理ステップで、まだ運んでいなければ運んだことにして true を返す。もう運んでいれば false
        /// 計算量: O(1)（償却）。ステップが変わったときだけ O(n) で空にする（n は前のステップで運んだ物の数）
        public static bool TryMark(Rigidbody body)
        {
            float now = Time.fixedTime;
            if (now != _stepTime)
            {
                _carried.Clear();
                _stepTime = now;
            }
            return _carried.Add(body);
        }
    }
}
