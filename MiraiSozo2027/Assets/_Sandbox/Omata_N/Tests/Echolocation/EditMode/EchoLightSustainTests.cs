using NUnit.Framework;

namespace Echo.Echolocation.Tests
{
    public sealed class EchoLightSustainTests
    {
        private static EchoLightSustainSettings CreateSettings(EchoLightRecoveryMode recovery = EchoLightRecoveryMode.WhileOff,
            EchoLightDepletionMode depletion = EchoLightDepletionMode.TurnOff) =>
            new EchoLightSustainSettings { HasSustainLimit = true, SustainSeconds = 4, RecoveryMode = recovery, DepletionMode = depletion, RecoverySpeed = .5f };

        [Test]
        public void UnlimitedSettingNeverDepletes()
        {
            var sustain = new EchoLightSustain();
            var settings = new EchoLightSustainSettings();
            Assert.That(sustain.Advance(true, 100, settings), Is.False);
            Assert.That(sustain.GetRatio(settings), Is.EqualTo(1));
            Assert.That(sustain.CanTurnOn(settings), Is.True);
        }

        [Test]
        public void ConsumesWhileOnAndReportsDepletionOnce()
        {
            var sustain = new EchoLightSustain();
            var settings = CreateSettings();
            Assert.That(sustain.Advance(true, 1, settings), Is.False);
            Assert.That(sustain.GetRatio(settings), Is.EqualTo(.75f).Within(1e-5f));
            Assert.That(sustain.Advance(true, 3, settings), Is.True, "使い切ったフレームで通知");
            Assert.That(sustain.Advance(true, 1, settings), Is.False, "使い切った後は通知しない");
            Assert.That(sustain.CanTurnOn(settings), Is.False, "自動でOFFになる設定は回復までONにできない");
            settings.DepletionMode = EchoLightDepletionMode.KeepMinimum;
            Assert.That(sustain.CanTurnOn(settings), Is.True, "最小の範囲で照らし続ける設定はONにできる");
        }

        [Test]
        public void RecoveryModesRestoreRemainingTime()
        {
            var gradual = new EchoLightSustain();
            var refill = new EchoLightSustain();
            var none = new EchoLightSustain();
            var gradualSettings = CreateSettings(EchoLightRecoveryMode.WhileOff);
            var refillSettings = CreateSettings(EchoLightRecoveryMode.RefillOnOff);
            var noneSettings = CreateSettings(EchoLightRecoveryMode.None);
            gradual.Advance(true, 3, gradualSettings); gradual.Advance(false, 2, gradualSettings);
            refill.Advance(true, 3, refillSettings); refill.Advance(false, 0, refillSettings);
            none.Advance(true, 3, noneSettings); none.Advance(false, 100, noneSettings);
            // 回復の速さ0.5のため、2秒で1秒分だけ回復する。
            Assert.That(gradual.RemainingSeconds, Is.EqualTo(2).Within(1e-5f));
            Assert.That(refill.RemainingSeconds, Is.EqualTo(4));
            Assert.That(none.RemainingSeconds, Is.EqualTo(1).Within(1e-5f));
            none.Recover(10, noneSettings);
            Assert.That(none.RemainingSeconds, Is.EqualTo(4), "持続可能な時間を超えて回復しない");
        }

        [Test]
        public void ChangingSustainSecondsStartsFull()
        {
            var sustain = new EchoLightSustain();
            var settings = CreateSettings();
            sustain.Advance(true, 3, settings);
            settings.SustainSeconds = 8;
            Assert.That(sustain.GetRatio(settings), Is.EqualTo(1));
            Assert.That(sustain.RemainingSeconds, Is.EqualTo(8));
        }

        [Test]
        public void TurningLimitOnStartsFullAndOffRemovesLimit()
        {
            var sustain = new EchoLightSustain();
            var settings = CreateSettings();
            sustain.Advance(true, 3, settings);
            settings.HasSustainLimit = false;
            Assert.That(sustain.Advance(true, 100, settings), Is.False, "時間制限なしでは使い切らない");
            Assert.That(sustain.GetRatio(settings), Is.EqualTo(1));
            settings.HasSustainLimit = true;
            Assert.That(sustain.RemainingSeconds, Is.EqualTo(0), "切り替えるまでは前の状態のまま");
            Assert.That(sustain.GetRatio(settings), Is.EqualTo(1), "時間制限をオンにしたら満タンから始める");
            Assert.That(sustain.RemainingSeconds, Is.EqualTo(4));
        }

        [Test]
        public void RadiusAndAngleShrinkOnlyWhenChecked()
        {
            var settings = new EchoLightSustainSettings { MinimumRadius = 2, MinimumAngleDegrees = 60 };
            Assert.That(settings.GetRadius(10, 0), Is.EqualTo(10), "チェックしていなければ狭めない");
            Assert.That(settings.GetAngleDegrees(360, 0), Is.EqualTo(360));
            settings.IsRadiusShrinking = true;
            settings.IsAngleShrinking = true;
            Assert.That(settings.GetRadius(10, 1), Is.EqualTo(10));
            Assert.That(settings.GetRadius(10, .5f), Is.EqualTo(6).Within(1e-5f));
            Assert.That(settings.GetRadius(10, 0), Is.EqualTo(2));
            Assert.That(settings.GetAngleDegrees(360, 0), Is.EqualTo(60));
            Assert.That(settings.GetRadius(1, 0), Is.EqualTo(1), "最小値が最大値より大きければ最大値に合わせる");
        }
    }
}
