// 発生位置固定・残存時間・OFFの優先規則を描画やフレームレートから独立して検証する。
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace Echo.Echolocation.Tests
{
    public sealed class EchoPulseTimelineTests
    {
        private EchoSettings _settings;
        private EchoPulseTimeline _timeline;
        private List<EchoFrameData> _frames;
        [SetUp]
        public void Setup()
        {
            _settings = new EchoSettings
            {
                Radius = 10,
                Duration = 2,
                PulseInterval = 1,
                PulseHoldDuration = 3,
                ScanMode = EchoScanMode.Sonar
            };
            _timeline = new EchoPulseTimeline();
            _frames = new List<EchoFrameData>();
            _timeline.Start(Vector3.zero, Vector3.forward, _settings);
        }

        void Tick(float delta, Vector3 origin) => _timeline.Advance(delta, _settings, origin, Vector3.right, _frames);
        [Test]
        public void OldWaveKeepsItsOriginAndForwardWhenNextWaveStarts()
        {
            Tick(0, Vector3.right * 10);
            Assert.That(_frames.Count, Is.EqualTo(1));
            Tick(1, Vector3.right * 10);
            Assert.That(_frames.Count, Is.EqualTo(2));
            Assert.That(_frames[0].Origin, Is.EqualTo(Vector3.zero));
            Assert.That(_frames[0].Forward, Is.EqualTo(Vector3.forward));
            Assert.That(_frames[0].OuterRadius, Is.EqualTo(5));
            Assert.That(_frames[1].Origin, Is.EqualTo(Vector3.right * 10));
            Assert.That(_frames[1].Forward, Is.EqualTo(Vector3.right));
            Assert.That(_frames[1].OuterRadius, Is.Zero);
        }

        [Test]
        public void LifetimeIsExpansionPlusHoldAndDoesNotResetOnNewEmission()
        {
            Tick(2, Vector3.right);
            Assert.That(_frames[0].State, Is.EqualTo(EchoState.Active));
            Tick(2.99f, Vector3.right);
            Assert.That(_frames[0].Origin, Is.EqualTo(Vector3.zero));
            Tick(.02f, Vector3.right);
            foreach (var f in _frames)
                Assert.That(f.Origin, Is.Not.EqualTo(Vector3.zero));
        }

        [Test]
        public void ImmediateOffRemovesAllWavesAndStopsEmission()
        {
            Tick(1, Vector3.right);
            _timeline.Stop(_settings);
            Tick(5, Vector3.right);
            Assert.That(_frames, Is.Empty);
        }

        [Test]
        public void WaveOffFreezesEachOuterRadiusAndErasesFromItsOwnOrigin()
        {
            Tick(1, Vector3.right);
            Tick(.5f, Vector3.right);
            _settings.OffMode = EchoOffMode.Wave;
            _timeline.Stop(_settings);
            Tick(.2f, Vector3.right * 100);
            Assert.That(_frames.Count, Is.EqualTo(2));
            Assert.That(_frames[0].OuterRadius, Is.EqualTo(7.5f));
            Assert.That(_frames[1].OuterRadius, Is.EqualTo(2.5f));
            Assert.That(_frames[0].EraseRadius, Is.EqualTo(1).Within(.001));
            Assert.That(_frames[0].Origin, Is.EqualTo(Vector3.zero));
            Tick(2, Vector3.right * 100);
            Assert.That(_frames, Is.Empty);
        }

        [Test]
        public void RestartDiscardsPreviousWaves()
        {
            Tick(1, Vector3.right);
            _timeline.Start(Vector3.up, Vector3.back, _settings);
            Tick(0, Vector3.right);
            Assert.That(_frames.Count, Is.EqualTo(1));
            Assert.That(_frames[0].Origin, Is.EqualTo(Vector3.up));
        }

        [Test]
        public void NoDeltaDoesNotAdvanceTime()
        {
            Tick(0, Vector3.zero);
            Tick(0, Vector3.right);
            Assert.That(_frames.Count, Is.EqualTo(1));
            Assert.That(_frames[0].OuterRadius, Is.Zero);
        }

        [Test]
        public void IntervalLongerThanLifetimeLeavesAnIntentionalGap()
        {
            _settings.Duration = .5f;
            _settings.PulseHoldDuration = .5f;
            _settings.PulseInterval = 2;
            Tick(1.1f, Vector3.right);
            Assert.That(_frames, Is.Empty);
            Tick(.9f, Vector3.right);
            Assert.That(_frames.Count, Is.EqualTo(1));
        }

        [Test]
        public void LongFrameDoesNotCreateUnboundedCatchupWaves()
        {
            Tick(1000, Vector3.right);
            Assert.That(_frames.Count, Is.EqualTo(1));
            Assert.That(_frames[0].OuterRadius, Is.Zero);
        }

        [Test]
        public void ZeroLifetimeNeverProducesAVisibleFrame()
        {
            _settings.Duration = 0;
            _settings.PulseHoldDuration = 0;
            _timeline.Start(Vector3.zero, Vector3.forward, _settings);
            Tick(0, Vector3.zero);
            Assert.That(_frames, Is.Empty);
            Tick(1, Vector3.right);
            Assert.That(_frames, Is.Empty);
        }

        [Test]
        public void CapacityValidationPreservesLifetimeRatherThanEvictingYoungWaves()
        {
            _settings.Duration = 2;
            _settings.PulseHoldDuration = 100;
            _settings.PulseInterval = .05f;
            _settings.Validate();
            Assert.That(_settings.PulseInterval, Is.GreaterThanOrEqualTo(102f / EchoPulseTimeline.MaxPulses));
        }
    }
}
