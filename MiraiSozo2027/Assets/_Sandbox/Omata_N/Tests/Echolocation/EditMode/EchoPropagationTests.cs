// 時間から距離への変換と入力の再受付境界を検証する。
using NUnit.Framework;

namespace Echo.Echolocation.Tests
{
    public sealed class EchoPropagationTests
    {
        [TestCase(0, 0)]
        [TestCase(1, 5)]
        [TestCase(2, 10)]
        [TestCase(5, 10)]
        public void DistanceDependsOnElapsedSeconds(float elapsed, float expected) => Assert.That(EchoPropagation.GetDistance(10, 2, elapsed), Is.EqualTo(expected));
        [Test]
        public void ZeroDurationIsImmediate() => Assert.That(EchoPropagation.GetDistance(10, 0, 0), Is.EqualTo(10));
        [Test]
        public void HeldInputRequiresReleaseBeforeRearming()
        {
            var edge = new EchoInputEdgeDetector();
            edge.Synchronize(0, .25f);
            Assert.False(edge.Evaluate(.49f, .5f, .25f));
            Assert.True(edge.Evaluate(.5f, .5f, .25f));
            Assert.False(edge.Evaluate(.4f, .5f, .25f));
            Assert.False(edge.Evaluate(.5f, .5f, .25f));
            Assert.False(edge.Evaluate(.25f, .5f, .25f));
            Assert.True(edge.Evaluate(.5f, .5f, .25f));
        }

        [Test]
        public void AlreadyHeldInputNeverFiresOnEnable()
        {
            var edge = new EchoInputEdgeDetector();
            edge.Synchronize(1, .25f);
            Assert.False(edge.Evaluate(1, .5f, .25f));
        }
    }
}
