// 設定のコピーとタグ色の実行時変更を検証する。
using NUnit.Framework;
using UnityEngine;

namespace Echo.Echolocation.Tests
{
    public sealed class EchoTagResolverTests
    {
        [Test]
        public void HiddenMaterialsPreservePartialEditsAndMultipleOwners()
        {
            var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            var cameraObject = new GameObject("Original reference test", typeof(Camera));
            var first = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            var replacement = new Material(first);
            var a = new EchoTargetRegistry();
            var b = new EchoTargetRegistry();
            try
            {
                target.tag = "Respawn";
                var renderer = target.GetComponent<Renderer>();
                renderer.sharedMaterials = new[] { first, first };
                var tags = new EchoTagResolver();
                tags.Update(new System.Collections.Generic.List<EchoTagColorRule>
                {
                    new EchoTagColorRule { TagName = "Respawn", OriginalEnabled = true }
                });
                a.Refresh(true);
                b.Refresh(true);
                var camera = cameraObject.GetComponent<Camera>();
                a.UpdateHidden(~0, null, camera, tags);
                b.UpdateHidden(~0, null, camera, tags);
                Assert.That(EchoTargetRegistry.GetOriginalMaterials(renderer)[0], Is.SameAs(first));
                a.Clear();
                Assert.That(renderer.sharedMaterial, Is.Not.SameAs(first), "別Controllerが使用中なら非表示を維持");
                var current = renderer.sharedMaterials;
                current[1] = replacement;
                renderer.sharedMaterials = current;
                b.Clear();
                Assert.That(renderer.sharedMaterials[0], Is.SameAs(first));
                Assert.That(renderer.sharedMaterials[1], Is.SameAs(replacement), "外部の変更は上書きしない");
            }
            finally
            {
                a.Clear();
                b.Clear();
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(first);
                Object.DestroyImmediate(replacement);
            }
        }

        [Test]
        public void EffectSettingsCopyValidateAndResolveLiveChanges()
        {
            var settings = new EchoSettings();
            var rule = new EchoTagColorRule { TagName = "Player", OutlineEnabled = true, OriginalEnabled = true,
                OutlineWidth = float.NaN, OriginalHoldDuration = -2, OriginalOpacity = 3 };
            settings.TagColorRules.Add(rule);
            settings.Validate();
            Assert.That(rule.OutlineWidth, Is.EqualTo(3));
            Assert.That(rule.OriginalHoldDuration, Is.Zero);
            Assert.That(rule.OriginalOpacity, Is.EqualTo(1));
            var copy = settings.Copy();
            Assert.True(copy.TagColorRules[0].OutlineEnabled);
            Assert.True(copy.TagColorRules[0].OriginalEnabled);
            copy.TagColorRules[0].OriginalHoldDuration = 5;
            Assert.That(rule.OriginalHoldDuration, Is.Zero);
            var resolver = new EchoTagResolver();
            resolver.Update(settings.TagColorRules);
            rule.OutlineWidth = 7;
            Assert.True(resolver.TryGetRule("Player", out var resolved));
            Assert.That(resolved.OutlineWidth, Is.EqualTo(7));
            settings.TagColorRules[0] = copy.TagColorRules[0];
            resolver.Update(settings.TagColorRules);
            resolver.TryGetRule("Player", out resolved);
            Assert.That(resolved.OriginalHoldDuration, Is.EqualTo(5));
        }

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
