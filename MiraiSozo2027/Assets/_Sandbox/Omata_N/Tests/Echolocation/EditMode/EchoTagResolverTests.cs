// 設定のコピーとタグ色の実行時変更を検証する。
using NUnit.Framework;
using UnityEngine;

namespace Echo.Echolocation.Tests
{
    public sealed class EchoTagResolverTests
    {
        [Test]
        public void CopyDoesNotShareMutableRules()
        {
            var s = new EchoSettings();
            s.TagColorRules.Add(new EchoTagColorRule());
            var copy = s.Copy();
            copy.TagColorRules[0].TagName = "Player";
            Assert.That(s.TagColorRules[0].TagName, Is.EqualTo("Untagged"));
        }

        [Test]
        public void ChangesToColorsAndEnabledRowsTakeEffect()
        {
            var s = new EchoSettings();
            var row = new EchoTagColorRule
            {
                TagName = "Player",
                Color = Color.red
            };
            s.TagColorRules.Add(row);
            var resolver = new EchoTagResolver();
            resolver.Update(s.TagColorRules);
            Assert.True(resolver.TryGet("Player", out var color));
            Assert.That(color, Is.EqualTo(Color.red));
            row.Color = Color.green;
            resolver.Update(s.TagColorRules);
            resolver.TryGet("Player", out color);
            Assert.That(color, Is.EqualTo(Color.green));
            row.IsEnabled = false;
            resolver.Update(s.TagColorRules);
            Assert.False(resolver.TryGet("Player", out _));
        }
    }
}
