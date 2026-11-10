using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
namespace Echo.Echolocation.Tests
{
    public sealed class EchoMaterializationTests
    {
        private static EchoMaterializationSettings CreateSettings(EchoIrradiationInterruptionMode interruption = EchoIrradiationInterruptionMode.Reset,
            EchoMaterializedPersistenceMode persistence = EchoMaterializedPersistenceMode.Permanent) =>
            new EchoMaterializationSettings { RequiredSeconds = 1, InterruptionMode = interruption, DecaySpeed = .5f, PersistenceMode = persistence, RevertSeconds = 2 };

        [Test]
        public void StartsTransparentAndBecomesIrradiatedOnContact()
        {
            var tracker = new EchoMaterializationTracker();
            Assert.That(tracker.State, Is.EqualTo(EchoMaterializationState.Transparent));
            Assert.That(tracker.Advance(true, .5f, CreateSettings()), Is.True);
            Assert.That(tracker.State, Is.EqualTo(EchoMaterializationState.Irradiated));
            Assert.That(tracker.IrradiationSeconds, Is.EqualTo(.5f).Within(1e-5f));
        }

        [Test]
        public void MaterializesAfterRequiredSeconds()
        {
            var tracker = new EchoMaterializationTracker();
            var settings = CreateSettings();
            tracker.Advance(true, .6f, settings);
            Assert.That(tracker.Advance(true, .6f, settings), Is.True);
            Assert.That(tracker.State, Is.EqualTo(EchoMaterializationState.Materialized));
        }

        [Test]
        public void ZeroRequiredSecondsMaterializesOnFirstContact()
        {
            var tracker = new EchoMaterializationTracker();
            var settings = CreateSettings();
            settings.RequiredSeconds = 0;
            tracker.Advance(true, 0, settings);
            Assert.That(tracker.State, Is.EqualTo(EchoMaterializationState.Materialized));
        }

        [Test]
        public void InterruptionModesHandleAccumulatedTime()
        {
            var reset = new EchoMaterializationTracker();
            var keep = new EchoMaterializationTracker();
            var decay = new EchoMaterializationTracker();
            var resetSettings = CreateSettings(EchoIrradiationInterruptionMode.Reset);
            var keepSettings = CreateSettings(EchoIrradiationInterruptionMode.Keep);
            var decaySettings = CreateSettings(EchoIrradiationInterruptionMode.Decay);
            reset.Advance(true, .8f, resetSettings); reset.Advance(false, .4f, resetSettings);
            keep.Advance(true, .8f, keepSettings); keep.Advance(false, .4f, keepSettings);
            decay.Advance(true, .8f, decaySettings); decay.Advance(false, .4f, decaySettings);
            Assert.That(reset.IrradiationSeconds, Is.EqualTo(0));
            Assert.That(keep.IrradiationSeconds, Is.EqualTo(.8f).Within(1e-5f));
            // 減る速さ0.5のため、0.4秒で0.2秒分だけ減る。
            Assert.That(decay.IrradiationSeconds, Is.EqualTo(.6f).Within(1e-5f));
            Assert.That(keep.State, Is.EqualTo(EchoMaterializationState.Transparent));
            keep.Advance(true, .3f, keepSettings);
            Assert.That(keep.State, Is.EqualTo(EchoMaterializationState.Materialized), "保持した時間に加算して実体化する");
        }

        [Test]
        public void PermanentMaterializationIgnoresIdleTime()
        {
            var tracker = new EchoMaterializationTracker();
            var settings = CreateSettings();
            tracker.Materialize(settings.RequiredSeconds);
            tracker.Advance(false, 100, settings);
            Assert.That(tracker.State, Is.EqualTo(EchoMaterializationState.Materialized));
        }

        [Test]
        public void RevertAfterIdleReturnsToTransparentAndContactRestartsTimer()
        {
            var tracker = new EchoMaterializationTracker();
            var settings = CreateSettings(persistence: EchoMaterializedPersistenceMode.RevertAfterIdle);
            tracker.Materialize(settings.RequiredSeconds);
            tracker.Advance(false, 1.5f, settings);
            tracker.Advance(true, .1f, settings);
            tracker.Advance(false, 1.5f, settings);
            Assert.That(tracker.State, Is.EqualTo(EchoMaterializationState.Materialized), "当たり直すと時間がリセットされる");
            Assert.That(tracker.Advance(false, .6f, settings), Is.True);
            Assert.That(tracker.State, Is.EqualTo(EchoMaterializationState.Transparent));
            Assert.That(tracker.IrradiationSeconds, Is.EqualTo(0));
        }

        [Test]
        public void DisplayRuleSwitchesOnlyWhenNormalRenderingIsHidden()
        {
            var rule = new EchoTagColorRule();
            rule.Irradiated.OutlineColor = Color.red;
            rule.OutlineColor = Color.green;
            Assert.That(rule.GetDisplayRule(EchoMaterializationState.Transparent), Is.SameAs(rule));
            rule.IsNormalRenderingHidden = true;
            var irradiated = rule.GetDisplayRule(EchoMaterializationState.Irradiated);
            Assert.That(irradiated, Is.Not.SameAs(rule));
            Assert.That(irradiated.OutlineColor, Is.EqualTo(Color.red));
            Assert.That(irradiated.IsNormalRenderingHidden, Is.True, "照射中も描画対象に残す");
            Assert.That(rule.GetDisplayRule(EchoMaterializationState.Transparent), Is.SameAs(irradiated), "毎フレーム作り直さない");
            Assert.That(rule.GetDisplayRule(EchoMaterializationState.Materialized), Is.SameAs(rule));
        }

        [Test]
        public void IrradiatedRuleReflectsChangesMadeAfterCreation()
        {
            var rule = new EchoTagColorRule { IsNormalRenderingHidden = true };
            var irradiated = rule.GetDisplayRule(EchoMaterializationState.Irradiated);
            rule.Irradiated.Fill.IsEnabled = true;
            rule.Irradiated.Color = Color.red;
            rule.IsEnabled = false;
            Assert.That(rule.GetDisplayRule(EchoMaterializationState.Irradiated), Is.SameAs(irradiated), "毎フレーム作り直さない");
            Assert.That(irradiated.IsFillEnabled, Is.True);
            Assert.That(irradiated.Color, Is.EqualTo(Color.red));
            Assert.That(irradiated.IsEnabled, Is.False);
        }

        [Test]
        public void NewRuleKeepsHiddenSettingAfterSaveAndLoad()
        {
            var definition = ScriptableObject.CreateInstance<EchoTagDefinition>();
            var restored = ScriptableObject.CreateInstance<EchoTagDefinition>();
            try
            {
                // ③を表示しない新規データは、旧データとして移行されると隠す設定が外れてしまう。
                definition.Settings.Original.IsEnabled = false;
                definition.Settings.IsNormalRenderingHidden = true;
                definition.Settings.Irradiated.OutlineColor = Color.red;
                JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(definition), restored);
                Assert.That(restored.Settings.IsNormalRenderingHidden, Is.True);
                Assert.That(restored.Settings.Irradiated.OutlineColor, Is.EqualTo(Color.red));
            }
            finally { Object.DestroyImmediate(definition); Object.DestroyImmediate(restored); }
        }

        [Test]
        public void SeamlessTransitionBlendsTowardMaterializedDisplay()
        {
            var rule = new EchoTagColorRule { IsNormalRenderingHidden = true };
            rule.Fill.IsEnabled = true; rule.Color = Color.red;
            rule.Outline.IsEnabled = false;
            rule.Irradiated.Fill.IsEnabled = true; rule.Irradiated.Color = Color.blue;
            rule.Irradiated.Fill.IsSeamlessTransition = true;
            rule.Irradiated.Outline.IsEnabled = true; rule.Irradiated.OutlineColor = Color.white;
            rule.Irradiated.Outline.IsSeamlessTransition = true;
            rule.Irradiated.Original.IsEnabled = true; rule.Irradiated.OriginalOpacity = .2f;
            rule.Irradiated.Original.IsSeamlessTransition = true;
            var irradiated = rule.GetDisplayRule(EchoMaterializationState.Irradiated);
            Assert.That(irradiated.GetFillColor(0), Is.EqualTo(Color.blue));
            Assert.That(irradiated.GetFillColor(.5f), Is.EqualTo(Color.Lerp(Color.blue, Color.red, .5f)));
            Assert.That(irradiated.GetFillColor(1), Is.EqualTo(Color.red));
            // 実体化後に②を表示しないため、同じ色のまま透明になっていく。
            Assert.That(irradiated.GetOutlineColor(1).a, Is.EqualTo(0));
            Assert.That(irradiated.GetOriginalOpacity(.5f), Is.EqualTo(.6f).Within(1e-5f));
            Assert.That(irradiated.GetOriginalOpacity(1), Is.EqualTo(1));
        }

        [Test]
        public void TransitionIsIgnoredWithoutSeamlessFlagOrForMaterializedRule()
        {
            var rule = new EchoTagColorRule { IsNormalRenderingHidden = true, Color = Color.red };
            rule.Irradiated.Color = Color.blue;
            var irradiated = rule.GetDisplayRule(EchoMaterializationState.Irradiated);
            Assert.That(irradiated.GetFillColor(1), Is.EqualTo(Color.blue));
            Assert.That(rule.GetFillColor(.5f), Is.EqualTo(Color.red));
        }

        [Test]
        public void EffectCopyOnlyTouchesTheSelectedEffect()
        {
            var rule = new EchoTagColorRule { Color = Color.red, OutlineColor = Color.green, OriginalOpacity = .3f };
            rule.Fill.IsEnabled = true; rule.Outline.IsEnabled = true; rule.Original.IsEnabled = true;
            rule.Irradiated.Color = Color.blue; rule.Irradiated.OutlineColor = Color.white; rule.Irradiated.OriginalOpacity = .9f;
            rule.Irradiated.Outline.IsSeamlessTransition = true;
            rule.CopyMaterializedOutlineToIrradiated();
            Assert.That(rule.Irradiated.OutlineColor, Is.EqualTo(Color.green));
            Assert.That(rule.Irradiated.Outline.IsEnabled, Is.True);
            Assert.That(rule.Irradiated.Outline.IsSeamlessTransition, Is.True, "照射中だけの設定は残す");
            Assert.That(rule.Irradiated.Color, Is.EqualTo(Color.blue));
            Assert.That(rule.Irradiated.OriginalOpacity, Is.EqualTo(.9f));
            rule.CopyMaterializedFillToIrradiated();
            rule.CopyMaterializedOriginalToIrradiated();
            Assert.That(rule.Irradiated.Color, Is.EqualTo(Color.red));
            Assert.That(rule.Irradiated.OriginalOpacity, Is.EqualTo(.3f));
        }

        [Test]
        public void HiddenRuleIsDrawnEvenWithoutDisplayEffects()
        {
            var rule = new EchoTagColorRule();
            rule.Fill.IsEnabled = false; rule.Outline.IsEnabled = false; rule.Original.IsEnabled = false;
            Assert.That(rule.HasAnyEffect, Is.False);
            rule.IsNormalRenderingHidden = true;
            Assert.That(rule.HasAnyEffect, Is.True);
        }

        [Test]
        public void LegacyHiddenOriginalMigratesWithSameAppearance()
        {
            var definition = ScriptableObject.CreateInstance<EchoTagDefinition>();
            var restored = ScriptableObject.CreateInstance<EchoTagDefinition>();
            try
            {
                // 照射中の表示を追加する前のアセットを再現する。新しい項目は存在しない。
                definition.Settings.Original.IsEnabled = true;
                definition.Settings.Outline.IsEnabled = true;
                definition.Settings.OutlineColor = Color.magenta;
                var json = JsonUtility.ToJson(definition);
                json = Regex.Replace(json, "\"_settingsVersion\":\\d+", "\"_settingsVersion\":0");
                json = Regex.Replace(json, "\"_isNormalRenderingHidden\":(true|false)", "\"_isNormalRenderingHidden\":false");
                JsonUtility.FromJsonOverwrite(json, restored);
                var rule = restored.Settings;
                Assert.That(rule.IsNormalRenderingHidden, Is.True);
                Assert.That(rule.Irradiated.Outline.IsEnabled, Is.True);
                Assert.That(rule.Irradiated.Original.IsEnabled, Is.True);
                Assert.That(rule.Irradiated.OutlineColor, Is.EqualTo(Color.magenta));
            }
            finally { Object.DestroyImmediate(definition); Object.DestroyImmediate(restored); }
        }

        [Test]
        public void MigrationDoesNotHideWhenOriginalWasDisabled()
        {
            var rule = new EchoTagColorRule();
            rule.Original.IsEnabled = false;
            rule.MigrateSettings();
            Assert.That(rule.IsNormalRenderingHidden, Is.False);
        }

        [Test]
        public void CopyKeepsIrradiatedAndMaterializationIndependent()
        {
            var original = new EchoTagColorRule();
            var copy = original.Copy();
            copy.Irradiated.Fill.IsEnabled = true;
            copy.Materialization.RequiredSeconds = 9;
            Assert.That(original.Irradiated.Fill.IsEnabled, Is.False);
            Assert.That(original.Materialization.RequiredSeconds, Is.Not.EqualTo(9));
        }
    }
}
