using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Aura.Gimmick.Tests
{
    /// @brief 2 つのライダーにまたがって乗った物を 1 ステップに 1 回だけ運ぶための、運んだ物の記録の PlayMode テスト
    public sealed class WaveRiderCarriedSetTests
    {
        private GameObject _object;

        [TearDown]
        public void TearDown()
        {
            if (_object != null)
            {
                Object.Destroy(_object);
            }
        }

        [UnityTest]
        public IEnumerator 同じ物理ステップでは1回だけ運べて次のステップでまた運べる()
        {
            _object = new GameObject("Passenger");
            Rigidbody body = _object.AddComponent<Rigidbody>();
            yield return new WaitForFixedUpdate();

            Assert.IsTrue(WaveRiderCarriedSet.TryMark(body), "1 回目は運べる");
            Assert.IsFalse(WaveRiderCarriedSet.TryMark(body), "同じステップの 2 回目は運ばない");

            yield return new WaitForFixedUpdate();

            Assert.IsTrue(WaveRiderCarriedSet.TryMark(body), "次のステップではまた運べる");
        }
    }
}
