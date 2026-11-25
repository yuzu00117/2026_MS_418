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
        public void Awakeより前のToggleは始めから起動しておく設定から入れ替える()
        {
            WaveRider rider = CreateInactiveRider(true);
            rider.Toggle();
            Awaken();

            Assert.IsFalse(IsActive(rider));
        }
    }
}
