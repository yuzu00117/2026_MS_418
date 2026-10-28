using NUnit.Framework;
using UnityEngine;
namespace Echo.Echolocation.Tests
{
    public sealed class EchoGlitchSettingsTests
    {
        [Test]
        public void RuleCopyDoesNotShareGlitchSettings()
        {
            var original = new EchoTagColorRule();
            original.Glitch.IsEnabled = true;
            original.Glitch.Mode = EchoGlitchMode.Always;
            var copy = original.Copy();
            copy.Glitch.IsEnabled = false;
            copy.Glitch.Mode = EchoGlitchMode.OnReveal;
            Assert.That(original.Glitch.IsEnabled, Is.True);
            Assert.That(original.Glitch.Mode, Is.EqualTo(EchoGlitchMode.Always));
        }
        [Test]
        public void TagRulesSerializeIndependentGlitchSettings()
        {
            var settings = new EchoSettings();
            var first = new EchoTagColorRule();
            var second = new EchoTagColorRule();
            first.Glitch.IsEnabled = true;
            first.Glitch.Mode = EchoGlitchMode.WhileRevealed;
            settings.TagColorRules.Add(first); settings.TagColorRules.Add(second);
            var restored = JsonUtility.FromJson<EchoSettings>(JsonUtility.ToJson(settings));
            Assert.That(restored.TagColorRules[0].Glitch.IsEnabled, Is.True);
            Assert.That(restored.TagColorRules[0].Glitch.Mode, Is.EqualTo(EchoGlitchMode.WhileRevealed));
            Assert.That(restored.TagColorRules[1].Glitch.IsEnabled, Is.False);
        }
    }
}
