using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Aura.Gimmick.Tests
{
    /// @brief WaveRider のスイッチでの起動（Activate / Deactivate）の PlayMode テスト
    /// 無効な GameObject に付けると Awake が呼ばれないので、Awake より前に Activate を呼んだ状態を作れる
    public sealed class WaveRiderActivationTests
    {
        private GameObject _riderObject;

        [TearDown]
        public void TearDown()
        {
            if (_riderObject != null)
            {
                Object.Destroy(_riderObject);
            }
        }

        /// 無効なままのライダーを作る（まだ Awake は呼ばれない）
        private WaveRider CreateInactiveRider(bool isActiveOnStart)
        {
            _riderObject = new GameObject("TestRider");
            _riderObject.SetActive(false);
            _riderObject.AddComponent<BoxCollider>();
            _riderObject.AddComponent<Aura.ObjectInfo.WaveReaction>();
            WaveRider rider = _riderObject.AddComponent<WaveRider>();
            SetField(rider, "_isActiveOnStart", isActiveOnStart);
            return rider;
        }

        /// 有効にして Awake を呼ぶ（波を出すものが未設定のエラーが出るので、期待しておく）
        private void Awaken()
        {
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("_emitter"));
            _riderObject.SetActive(true);
        }

        private static void SetField(object target, string name, object value)
        {
            FieldInfo field = typeof(WaveRider).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, name);
            field.SetValue(target, value);
        }

        private static bool IsActive(WaveRider rider)
        {
            FieldInfo field = typeof(WaveRider).GetField("_isActive", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, "_isActive");
            return (bool)field.GetValue(rider);
        }

        [Test]
        public void Awakeより前にActivateを呼んでも起動したまま()
        {
            WaveRider rider = CreateInactiveRider(false);
            rider.Activate();
            Awaken();

            Assert.IsTrue(IsActive(rider));
        }

        [Test]
        public void Awakeより前にDeactivateを呼べば始めから起動しておく設定でも止まったまま()
        {
            WaveRider rider = CreateInactiveRider(true);
            rider.Deactivate();
            Awaken();

            Assert.IsFalse(IsActive(rider));
        }

        [Test]
        public void 何も呼ばなければ始めから起動しておく設定に従う()
        {
            WaveRider rider = CreateInactiveRider(true);
            Awaken();

            Assert.IsTrue(IsActive(rider));
        }

        [Test]
        public void Awakeより前に決めた初期位置と初期の向きをAwakeで上書きしない()
        {
            WaveRider rider = CreateInactiveRider(false);
            rider.SetInitialPosition(new Vector3(1, 2, 3));
            rider.SetInitialRotation(Quaternion.Euler(0, 30, 0));
            Awaken();

            Assert.AreEqual(new Vector3(1, 2, 3), GetField<Vector3>(rider, "_initialPosition"));
            Assert.That(Quaternion.Angle(GetField<Quaternion>(rider, "_initialRotation"), Quaternion.Euler(0, 30, 0)), Is.LessThan(0.01f));
        }

        [Test]
        public void 何も決めなければ初期位置と初期の向きはAwakeの位置と向き()
        {
            WaveRider rider = CreateInactiveRider(false);
            _riderObject.transform.SetPositionAndRotation(new Vector3(4, 5, 6), Quaternion.Euler(0, 60, 0));
            Awaken();

            Assert.AreEqual(new Vector3(4, 5, 6), GetField<Vector3>(rider, "_initialPosition"));
            Assert.That(Quaternion.Angle(GetField<Quaternion>(rider, "_initialRotation"), Quaternion.Euler(0, 60, 0)), Is.LessThan(0.01f));
        }

        private static T GetField<T>(WaveRider rider, string name)
        {
            FieldInfo field = typeof(WaveRider).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(field, name);
            return (T)field.GetValue(rider);
        }

        [Test]
        public void Awakeより前のToggleは始めから起動しておく設定から入れ替える()
        {
            WaveRider rider = CreateInactiveRider(true);
            rider.Toggle();
            Awaken();

            Assert.IsFalse(IsActive(rider));
        }
    }
}
