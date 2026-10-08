// 180度超、巨大面の候補、消去境界など間違えやすい幾何条件を確認する。
using NUnit.Framework;
using UnityEngine;

namespace Echo.Echolocation.Tests
{
    public sealed class EchoVolumeMathTests
    {
        [TestCase(90, 44, true)]
        [TestCase(90, 46, false)]
        [TestCase(180, 89, true)]
        [TestCase(180, 91, false)]
        [TestCase(270, 134, true)]
        [TestCase(270, 136, false)]
        [TestCase(360, 180, true)]
        [TestCase(0, 0, false)]
        public void AngleHasCorrectOpening(float angle, float pointAngle, bool isExpected)
        {
            var f = new EchoFrameData(Vector3.zero, Vector3.forward, angle, 10, 0, EchoState.Active, EchoOcclusionMode.ThroughWalls);
            Assert.That(EchoVolumeMath.Contains(f, Quaternion.Euler(0, pointAngle, 0) * Vector3.forward * 5), Is.EqualTo(isExpected));
            Assert.That(EchoVolumeMath.Contains(f, Quaternion.Euler(pointAngle, 0, 0) * Vector3.forward * 5), Is.EqualTo(isExpected));
        }

        [Test]
        public void HidingKeepsOnlyOpenInnerClosedOuterShell()
        {
            var f = new EchoFrameData(Vector3.zero, Vector3.forward, 360, 10, 5, EchoState.Hiding, EchoOcclusionMode.ThroughWalls);
            Assert.False(EchoVolumeMath.Contains(f, Vector3.forward * 5));
            Assert.True(EchoVolumeMath.Contains(f, Vector3.forward * 5.01f));
            Assert.True(EchoVolumeMath.Contains(f, Vector3.forward * 10));
            Assert.False(EchoVolumeMath.Contains(f, Vector3.forward * 10.01f));
        }

        [Test]
        public void LargeSurfaceMustNotBeRejectedByItsCenter()
        {
            Assert.True(EchoVolumeMath.Intersects(new Bounds(new Vector3(50, 0, 0), new Vector3(100, 2, 2)), Vector3.zero, 1));
        }

        [Test]
        public void OriginDoesNotDivideByZero()
        {
            Assert.True(EchoVolumeMath.Contains(new EchoFrameData(Vector3.one, Vector3.forward, 90, 10, 0, EchoState.Active, 0), Vector3.one));
        }
    }
}
