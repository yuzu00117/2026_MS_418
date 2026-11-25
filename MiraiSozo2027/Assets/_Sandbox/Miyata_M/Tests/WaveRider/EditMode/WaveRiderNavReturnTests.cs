using NUnit.Framework;
using UnityEngine;

namespace Aura.Gimmick.Tests
{
    /// @brief WaveRiderNavReturn.Step（経路に沿って進む計算）のテスト。NavMesh は使わず、経路を直接入れる
    public sealed class WaveRiderNavReturnTests
    {
        private const float Tolerance = 0.0001f;

        /// L 字の経路: (0,0,0) → (4,0,0) → (4,0,3)。長さ 7
        private static readonly Vector3[] LShape =
        {
            new Vector3(0, 0, 0), new Vector3(4, 0, 0), new Vector3(4, 0, 3),
        };

        private static WaveRiderNavReturn Begin(Vector3 offset, Vector3 goalOffset)
        {
            var nav = new WaveRiderNavReturn();
            nav.SetCornersForTest(LShape);
            nav.BeginPlan(LShape[0], offset, goalOffset);
            return nav;
        }

        [Test]
        public void Step_角の手前では線分の上を進む()
        {
            WaveRiderNavReturn nav = Begin(Vector3.zero, Vector3.zero);
            Assert.That((nav.Step(3.0f) - new Vector3(3, 0, 0)).magnitude, Is.LessThan(Tolerance));
        }

        [Test]
        public void Step_1回で角をまたいでも余りを次の線分へ進む()
        {
            WaveRiderNavReturn nav = Begin(Vector3.zero, Vector3.zero);
            Assert.That((nav.Step(5.0f) - new Vector3(4, 0, 1)).magnitude, Is.LessThan(Tolerance));
        }

        [Test]
        public void Step_終点を超えて進めても終点で止まる()
        {
            WaveRiderNavReturn nav = Begin(Vector3.zero, Vector3.zero);
            nav.Step(5.0f);
            Assert.That((nav.Step(100.0f) - new Vector3(4, 0, 3)).magnitude, Is.LessThan(Tolerance));
        }

        [Test]
        public void Step_ずれを先に縮めてから経路を進む()
        {
            // 高さ 2 浮いて出発し、終点では高さ 0 の目標。先に降りてから経路を進む
            WaveRiderNavReturn nav = Begin(new Vector3(0, 2, 0), Vector3.zero);
            Assert.That((nav.Step(1.0f) - new Vector3(0, 1, 0)).magnitude, Is.LessThan(Tolerance));
            Assert.That((nav.Step(1.0f) - new Vector3(0, 0, 0)).magnitude, Is.LessThan(Tolerance));
            Assert.That((nav.Step(1.0f) - new Vector3(1, 0, 0)).magnitude, Is.LessThan(Tolerance));
        }

        [Test]
        public void Step_ずれの縮みと経路を合わせた移動量が与えた距離を超えない()
        {
            WaveRiderNavReturn nav = Begin(new Vector3(0, 0.5f, 0), Vector3.zero);
            Vector3 previous = new Vector3(0, 0.5f, 0);
            Vector3 position = nav.Step(2.0f);   // 0.5 降りて、残り 1.5 を経路へ
            Assert.That(Vector3.Distance(previous, position), Is.LessThanOrEqualTo(2.0f + Tolerance));
            Assert.That((position - new Vector3(1.5f, 0, 0)).magnitude, Is.LessThan(Tolerance));
        }

        [Test]
        public void Step_ずれの縮み方が速くても目標を行き過ぎない()
        {
            WaveRiderNavReturn nav = Begin(new Vector3(0, 1, 0), new Vector3(0, 0.5f, 0));
            Vector3 position = nav.Step(100.0f);
            Assert.That((position - new Vector3(4, 0.5f, 3)).magnitude, Is.LessThan(Tolerance));
        }

        [Test]
        public void Reset_経路を捨てる()
        {
            WaveRiderNavReturn nav = Begin(Vector3.zero, Vector3.zero);
            Assert.IsTrue(nav.HasPlan);
            nav.Reset();
            Assert.IsFalse(nav.HasPlan);
        }
    }
}
