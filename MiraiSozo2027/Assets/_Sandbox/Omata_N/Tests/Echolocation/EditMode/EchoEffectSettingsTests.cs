using NUnit.Framework;
using UnityEngine;
namespace Echo.Echolocation.Tests
{
    public sealed class EchoEffectSettingsTests
    {
        [Test]
        public void RuleCopyKeepsAllThreeEffectsIndependent()
        {
            var original = new EchoTagColorRule();
            var copy = original.Copy();
            copy.Fill.HasPersistence = true;
            copy.Outline.HasPersistence = true;
            copy.Original.HasPersistence = true;
            Assert.That(original.Fill.HasPersistence, Is.False);
            Assert.That(original.Outline.HasPersistence, Is.False);
            Assert.That(original.Original.HasPersistence, Is.False);
        }
        [Test]
        public void AssetRoundTripPreservesSharedDisplayScope()
        {
            var original = ScriptableObject.CreateInstance<EchoTagDefinition>();
            var restored = ScriptableObject.CreateInstance<EchoTagDefinition>();
            try
            {
                original.IsBlocking = false;
                original.Settings.Original.IsEnabled = true;
                original.Settings.Original.DisplayScope = EchoOriginalRevealMode.WholeObject;
                original.Settings.Original.HasPersistence = true;
                original.Settings.Original.PersistenceSeconds = 3;
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(original), restored);
                Assert.That(restored.IsBlocking, Is.False);
                Assert.That(restored.Settings.Original.DisplayScope, Is.EqualTo(EchoOriginalRevealMode.WholeObject));
                Assert.That(restored.Settings.Original.EffectivePersistence, Is.EqualTo(3));
                // 全体表示は残存も全体で行うため、表面ごとの記録は不要。
                Assert.That(restored.Settings.Original.NeedsSurfaceMemory, Is.False);
                Assert.That(restored.Settings.Original.NeedsWholeObjectContact, Is.True);
                restored.Settings.Original.DisplayScope = EchoOriginalRevealMode.ContactRegion;
                Assert.That(restored.Settings.Original.NeedsSurfaceMemory, Is.True);
                Assert.That(restored.Settings.Original.NeedsWholeObjectContact, Is.False);
            }
            finally { Object.DestroyImmediate(original); Object.DestroyImmediate(restored); }
        }
    }
}
