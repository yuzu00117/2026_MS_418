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
        private const float CargoSize = 0.3f;      ///< 運ばれる、重力のある箱の 1 辺（m）
        private const float CarrySpeed = 2.0f;     ///< 運ぶ検証で、ライダーが戻る速さ（m/s）
        private const float CarryTarget = 3.0f;    ///< 運ぶ検証で、ライダーが向かう初期位置の z（m）
        private const int CarrySteps = 40;         ///< 運ぶ検証で動かす物理ステップ数（戻り終える前に止める）
        private const float CarryRatioTolerance = 0.1f;  ///< 箱の進んだ距離とライダーの進んだ距離の比の、1 からのずれの許容

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

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = typeof(WaveRider).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, name);
            field.SetValue(target, value);
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
    
        [UnityTest]
        public IEnumerator 上に置いた重力のある箱はライダーと同じ距離だけ運ばれる()
        {
            // 初期位置（z = CarryTarget）へ戻る動きで、波を出すもの無しにライダーを一定の速さで動かす
            WaveRider rider = CreateRider(0.0f);
            SetField(rider, "_lostWaveMode", WaveRiderLostWaveMode.ReturnToStart);
            SetField(rider, "_returnSpeed", CarrySpeed);
            SetField(rider, "_isReturnPending", true);
            rider.SetInitialPosition(new Vector3(0, 0, CarryTarget));
            Rigidbody riderBody = rider.GetComponent<Rigidbody>();

            // 既定の材質（摩擦あり）の、重力のある箱を上面に置く
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _objects.Add(box);
            box.transform.localScale = Vector3.one * CargoSize;
            box.transform.position = new Vector3(0, RiderTop + CargoSize * 0.5f, 0);
            Rigidbody cargo = box.AddComponent<Rigidbody>();
            Physics.SyncTransforms();

            float riderStart = riderBody.position.z;
            float cargoStart = cargo.position.z;
            for (int i = 0; i < CarrySteps; i++)
            {
                yield return new WaitForFixedUpdate();
            }

            float riderMoved = riderBody.position.z - riderStart;
            float cargoMoved = cargo.position.z - cargoStart;
            Assert.That(riderMoved, Is.GreaterThan(0.5f), "ライダーが動いていない");
            Assert.That(cargoMoved / riderMoved, Is.EqualTo(1.0f).Within(CarryRatioTolerance),
                "箱の進んだ距離 " + cargoMoved + "m、ライダーの進んだ距離 " + riderMoved + "m");
        }
    }
}
