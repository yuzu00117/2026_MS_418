using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Aura.Gimmick.Tests
{
    /// @brief WaveRider の上に乗った物の検出の PlayMode テスト
    /// 波を出すものは置かず、その場で止まる設定で動かさずに、上面の検出だけを確かめる
    public sealed class WaveRiderPassengerTests
    {
        private const float SmallBoxSize = 0.1f;   ///< 上に置く小さな箱の 1 辺（m）
        private const float RiderTop = 0.5f;       ///< 1m のライダーの上面の高さ（m）

        private readonly List<GameObject> _objects = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject obj in _objects)
            {
                if (obj != null)
                {
                    Object.Destroy(obj);
                }
            }
            _objects.Clear();
        }

        /// Y 軸まわりに yaw 度回った、1m のライダーを作る
        private WaveRider CreateRider(float yaw)
        {
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("_emitter"));
            var riderObject = new GameObject("TestRider");
            _objects.Add(riderObject);
            riderObject.transform.rotation = Quaternion.Euler(0, yaw, 0);
            riderObject.AddComponent<BoxCollider>();
            riderObject.AddComponent<Aura.ObjectInfo.WaveReaction>();
            WaveRider rider = riderObject.AddComponent<WaveRider>();
            FieldInfo mode = typeof(WaveRider).GetField("_lostWaveMode", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(mode, "_lostWaveMode");
            mode.SetValue(rider, WaveRiderLostWaveMode.StopInPlace);
            return rider;
        }

        /// 上面の高さに、重力の無い小さな動く箱を置く
        private Rigidbody CreateSmallBox(float x, float z)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _objects.Add(box);
            box.transform.localScale = Vector3.one * SmallBoxSize;
            box.transform.position = new Vector3(x, RiderTop + SmallBoxSize * 0.5f, z);
            Rigidbody body = box.AddComponent<Rigidbody>();
            body.useGravity = false;
            return body;
        }

        private static HashSet<Rigidbody> GetPassengers(WaveRider rider)
        {
            FieldInfo field = typeof(WaveRider).GetField("_passengers", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "_passengers");
            return (HashSet<Rigidbody>)field.GetValue(rider);
        }

        [UnityTest]
        public IEnumerator 回っているときは上面の外の角の物を乗っている物とみなさない()
        {
            // 45° 回った 1m の箱の上面は、中心から各軸へ約 0.707m のひし形。(0.6, 0.6) は軸平行の箱の内側だが、上面の外
            WaveRider rider = CreateRider(45.0f);
            Rigidbody outside = CreateSmallBox(0.6f, 0.6f);
            Rigidbody inside = CreateSmallBox(0.0f, 0.0f);
            Physics.SyncTransforms();

            yield return new WaitForFixedUpdate();
            yield return new WaitForFixedUpdate();

            HashSet<Rigidbody> passengers = GetPassengers(rider);
            Assert.IsTrue(passengers.Contains(inside), "上面の真ん中の物は乗っている");
            Assert.IsFalse(passengers.Contains(outside), "上面の外の角の物は乗っていない");
        }
    }
}
