using System.Collections.Generic;
using UnityEngine;

namespace Aura.Gimmick
{
    /// @brief 有効な WaveRider の Rigidbody を覚える
    /// 当たり判定で「ほかの WaveRider か」を、周りの Collider ごとに毎回 GetComponent せずに引くために使う
    /// WaveRider が OnEnable で登録し、OnDisable で外す
    public static class WaveRiderRegistry
    {
        private static readonly HashSet<Rigidbody> _riders = new HashSet<Rigidbody>();  ///< 有効な WaveRider の Rigidbody

        /// ドメインの再読み込みを切った再生でも、前の再生の物を持ち越さない
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            _riders.Clear();
        }

        /// @brief 有効な WaveRider として登録する
        /// 計算量: O(1)（償却）
        public static void Register(Rigidbody body)
        {
            if (body != null)
            {
                _riders.Add(body);
            }
        }

        /// @brief 登録を外す
        /// 計算量: O(1)
        public static void Unregister(Rigidbody body)
        {
            if (body != null)
            {
                _riders.Remove(body);
            }
        }

        /// @brief 有効な WaveRider の Rigidbody か
        /// 計算量: O(1)
        public static bool Contains(Rigidbody body)
        {
            return _riders.Contains(body);
        }
    }
}
