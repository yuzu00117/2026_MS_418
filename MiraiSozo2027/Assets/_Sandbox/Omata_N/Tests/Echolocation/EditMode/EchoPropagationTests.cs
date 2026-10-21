// 時間から距離への変換を検証する。
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
    }
}
