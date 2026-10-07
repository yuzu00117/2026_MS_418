// 新しい透過は反射率ではなく、タグごとの遮断・非遮断であることを検証する。
using NUnit.Framework;

namespace Echo.Echolocation.Tests
{
    public sealed class EchoShadowSettingsTests
    {
        [Test]
        public void RulesUseFirstEnabledMatchAndFallback()
        {
            var s = new EchoShadowSettings();
            s.Rules.Add(new EchoBlockingRule { TagName = "Wall", IsEnabled = false, IsBlocking = true });
            s.Rules.Add(new EchoBlockingRule { TagName = "Wall", IsBlocking = false });
            s.Rules.Add(new EchoBlockingRule { TagName = "Wall", IsBlocking = true });
            Assert.False(s.IsBlockingTag("Wall"));
            Assert.True(s.IsBlockingTag("Unknown"));
            s.IsBlockingByDefault = false;
            Assert.False(s.IsBlockingTag("Unknown"));
        }

        [Test]
        public void CopiesAreIndependentAndValuesAreValidated()
        {
            var s = new EchoSettings();
            s.Shadow.Rules.Add(new EchoBlockingRule { TagName = "Wall", IsBlocking = false });
            s.Shadow.Resolution = 600;
            s.Shadow.Bias = float.NaN;
            var copy = s.Copy();
            copy.Shadow.Rules[0].IsBlocking = true;
            Assert.False(s.Shadow.Rules[0].IsBlocking);
            Assert.That(copy.Shadow.Resolution, Is.EqualTo(512));
            Assert.That(copy.Shadow.Bias, Is.EqualTo(.03f));
        }

        [Test]
        public void ShadowResolutionDoesNotReduceSonarPulseCount()
        {
            var s = new EchoSettings
            {
                Duration = 1,
                PulseHoldDuration = 3,
                PulseInterval = .1f
            };
            s.Shadow.Resolution = 1024;
            s.Validate();
            Assert.That(s.PulseInterval, Is.EqualTo(.1f), "反射経路数による旧制限は適用しない");
        }
    }
}
