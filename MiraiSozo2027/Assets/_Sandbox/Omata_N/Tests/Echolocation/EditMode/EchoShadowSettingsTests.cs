// 新しい透過は反射率ではなく、タグごとの遮断・非遮断であることを検証する。
using NUnit.Framework;

namespace Echo.Echolocation.Tests
{
    public sealed class EchoShadowSettingsTests
    {
        [Test]
        public void RulesUseFirstEnabledMatchAndFallback()
        {
            var settings = new EchoShadowSettings();
            settings.Rules.Add(new EchoBlockingRule { TagName = "Wall", IsEnabled = false, IsBlocking = true });
            settings.Rules.Add(new EchoBlockingRule { TagName = "Wall", IsBlocking = false });
            settings.Rules.Add(new EchoBlockingRule { TagName = "Wall", IsBlocking = true });
            Assert.False(settings.IsBlockingTag("Wall"));
            Assert.True(settings.IsBlockingTag("Unknown"));
            settings.IsBlockingByDefault = false;
            Assert.False(settings.IsBlockingTag("Unknown"));
        }

        [Test]
        public void CopiesAreIndependentAndValuesAreValidated()
        {
            var settings = new EchoSettings();
            settings.Shadow.Rules.Add(new EchoBlockingRule { TagName = "Wall", IsBlocking = false });
            settings.Shadow.Resolution = 600;
            settings.Shadow.Bias = float.NaN;
            var copy = settings.Copy();
            copy.Shadow.Rules[0].IsBlocking = true;
            Assert.False(settings.Shadow.Rules[0].IsBlocking);
            Assert.That(copy.Shadow.Resolution, Is.EqualTo(512));
            Assert.That(copy.Shadow.Bias, Is.EqualTo(.03f));
        }

        [Test]
        public void ShadowResolutionDoesNotReduceSonarPulseCount()
        {
            var settings = new EchoSettings
            {
                Duration = 1,
                PulseHoldDuration = 3,
                PulseInterval = .1f
            };
            settings.Shadow.Resolution = 1024;
            settings.Validate();
            Assert.That(settings.PulseInterval, Is.EqualTo(.1f), "反射経路数による旧制限は適用しない");
        }
    }
}
