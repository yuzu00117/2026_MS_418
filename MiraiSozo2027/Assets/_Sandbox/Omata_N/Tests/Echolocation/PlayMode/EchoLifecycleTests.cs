// 実際のLateUpdateを通して公開API、反転、無効化を確認する。
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Echo.Echolocation.Tests
{
    public sealed class EchoLifecycleTests
    {
        private GameObject _go;
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (_go)
                Object.Destroy(_go);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PublicCommandsAndDisableClearTheScan()
        {
            _go = new GameObject("Controller test");
            var controller = _go.AddComponent<EchoController>();
            controller.Settings.Duration = 0;
            controller.ToggleScan();
            yield return null;
            yield return null;
            Assert.That(controller.CurrentState, Is.EqualTo(EchoState.Active));
            controller.SetScanEnabled(true);
            yield return null;
            Assert.That(controller.CurrentState, Is.EqualTo(EchoState.Active));
            controller.ToggleScan();
            yield return null;
            Assert.That(controller.CurrentState, Is.EqualTo(EchoState.Hidden));
            controller.SetScanEnabled(true);
            yield return null;
            controller.enabled = false;
            Assert.That(controller.CurrentState, Is.EqualTo(EchoState.Hidden));
            Assert.False(controller.IsScanRequested);
        }

        [UnityTest]
        public IEnumerator MidRevealOffFreezesOuterRadiusThenRestartBeginsAtZero()
        {
            _go = new GameObject("Wave test");
            var controller = _go.AddComponent<EchoController>();
            controller.Settings.Radius = 10;
            controller.Settings.Duration = 2;
            controller.Settings.OffMode = EchoOffMode.Wave;
            controller.SetScanEnabled(true);
            yield return new WaitForSeconds(.15f);
            float radius = controller.CurrentOuterRadius;
            Assert.That(radius, Is.GreaterThan(0).And.LessThan(10));
            controller.SetScanEnabled(false);
            yield return null;
            Assert.That(controller.CurrentState, Is.EqualTo(EchoState.Hiding));
            Assert.That(controller.CurrentOuterRadius, Is.EqualTo(radius).Within(.1));
            controller.SetScanEnabled(true);
            yield return null;
            Assert.That(controller.CurrentOuterRadius, Is.LessThan(radius));
        }

        [UnityTest]
        public IEnumerator OnceModePropagatesBandOnceThenTurnsOff()
        {
            _go = new GameObject("Once scan test");
            var controller = _go.AddComponent<EchoController>();
            controller.Settings.ScanMode = EchoScanMode.Once;
            controller.Settings.Radius = 2;
            controller.Settings.Duration = .5f;
            controller.Settings.OnceRechargeSeconds = 0;
            bool hasStopped = false;
            controller.UsageChanged += isInUse => hasStopped |= !isInUse;
            controller.SetScanEnabled(true);
            yield return null;
            yield return new WaitForSeconds(.3f);
            Assert.That(controller.IsEchoInUse, Is.True, "伝播中は起動中");
            Assert.That(controller.CurrentState, Is.EqualTo(EchoState.Revealing));
            Assert.That(controller.Frames.Count, Is.EqualTo(1));
            var wave = controller.Frames[0];
            Assert.That(wave.IsBand, Is.True);
            Assert.That(wave.OuterRadius - wave.EraseRadius, Is.LessThanOrEqualTo(controller.RangeDisplay.WaveWidth + .001f), "当たり判定は帯の幅だけ");
            // 発信後にプレイヤーが動いても波の中心は変わらない。
            var origin = wave.Origin;
            _go.transform.position += Vector3.right * 5;
            yield return null;
            Assert.That(controller.Frames[0].Origin, Is.EqualTo(origin));
            float timeout = Time.time + 2;
            while (controller.IsEchoInUse && Time.time < timeout) yield return null;
            Assert.That(controller.IsEchoInUse, Is.False, "帯が最大半径を抜けたら終了");
            Assert.That(controller.IsScanRequested, Is.False, "自動でOFFに戻る");
            Assert.That(controller.CurrentState, Is.EqualTo(EchoState.Hidden));
            Assert.That(hasStopped, Is.True);
            // 終了後のONで再び1回発信できる。
            controller.SetScanEnabled(true);
            yield return null;
            Assert.That(controller.CurrentState, Is.EqualTo(EchoState.Revealing));
        }

        [UnityTest]
        public IEnumerator OnceModeStockLimitsRapidFireAndRecharges()
        {
            _go = new GameObject("Once stock test");
            var controller = _go.AddComponent<EchoController>();
            controller.Settings.ScanMode = EchoScanMode.Once;
            controller.Settings.Radius = 10;
            controller.Settings.Duration = 5;
            controller.Settings.OnceStockCapacity = 2;
            controller.Settings.OnceRechargeSeconds = .5f;
            yield return null;
            Assert.That(controller.OnceStock, Is.EqualTo(2));
            // 連打してもストックの数だけ発信する。
            controller.ToggleScan();
            controller.ToggleScan();
            controller.ToggleScan();
            yield return null;
            Assert.That(controller.Frames.Count, Is.EqualTo(2));
            Assert.That(controller.OnceStock, Is.Zero);
            Assert.That(controller.OnceRechargeProgress, Is.LessThan(1));
            float timeout = Time.time + 2;
            while (controller.OnceStock < 1 && Time.time < timeout) yield return null;
            Assert.That(controller.OnceStock, Is.EqualTo(1), "指定秒数で1回復する");
            controller.ToggleScan();
            yield return null;
            Assert.That(controller.Frames.Count, Is.EqualTo(3), "回復した分だけ追加で打てる");
            Assert.That(controller.IsEchoInUse, Is.True);
        }

        [UnityTest]
        public IEnumerator FollowModeRepeatsExpandingWaveAfterInterval()
        {
            _go = new GameObject("Follow wave test");
            var controller = _go.AddComponent<EchoController>();
            // 帯が端を抜ける0.3秒間を低いフレームレートでも観測できるよう、1m/秒で進める。
            controller.Settings.Radius = 1;
            controller.Settings.Duration = 1;
            controller.Settings.FollowWaveInterval = 1.5f;
            controller.SetScanEnabled(true);
            float timeout = Time.time + 2;
            while (controller.CurrentState != EchoState.Active && Time.time < timeout) yield return null;
            Assert.That(controller.CurrentState, Is.EqualTo(EchoState.Active));
            Assert.That(controller.FollowWaveRadius, Is.LessThan(0), "最初の展開の直後は繰り返しの波を出さない");
            // 間隔の後に中心から広がる。
            timeout = Time.time + 2;
            while (controller.FollowWaveRadius < 0 && Time.time < timeout) yield return null;
            Assert.That(controller.FollowWaveRadius, Is.GreaterThanOrEqualTo(0));
            Assert.That(controller.CurrentOuterRadius, Is.EqualTo(1), "検出範囲は変えない");
            // 帯が端を抜けきるまでは、先端が最大半径を超えても描き続ける。
            float maximum = 0;
            timeout = Time.time + 2;
            while (controller.FollowWaveRadius >= 0 && Time.time < timeout) { maximum = Mathf.Max(maximum, controller.FollowWaveRadius); yield return null; }
            Assert.That(maximum, Is.GreaterThan(1).And.LessThan(1 + controller.RangeDisplay.WaveWidth));
            controller.SetScanEnabled(false);
            yield return null;
            Assert.That(controller.FollowWaveRadius, Is.LessThan(0));
        }
    }
}
