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
            var c = _go.AddComponent<EchoController>();
            c.Settings.Duration = 0;
            c.ToggleScan();
            yield return null;
            yield return null;
            Assert.That(c.CurrentState, Is.EqualTo(EchoState.Active));
            c.SetScanEnabled(true);
            yield return null;
            Assert.That(c.CurrentState, Is.EqualTo(EchoState.Active));
            c.ToggleScan();
            yield return null;
            Assert.That(c.CurrentState, Is.EqualTo(EchoState.Hidden));
            c.SetScanEnabled(true);
            yield return null;
            c.enabled = false;
            Assert.That(c.CurrentState, Is.EqualTo(EchoState.Hidden));
            Assert.False(c.IsScanRequested);
        }

        [UnityTest]
        public IEnumerator MidRevealOffFreezesOuterRadiusThenRestartBeginsAtZero()
        {
            _go = new GameObject("Wave test");
            var c = _go.AddComponent<EchoController>();
            c.Settings.Radius = 10;
            c.Settings.Duration = 2;
            c.Settings.OffMode = EchoOffMode.Wave;
            c.SetScanEnabled(true);
            yield return new WaitForSeconds(.15f);
            float radius = c.CurrentOuterRadius;
            Assert.That(radius, Is.GreaterThan(0).And.LessThan(10));
            c.SetScanEnabled(false);
            yield return null;
            Assert.That(c.CurrentState, Is.EqualTo(EchoState.Hiding));
            Assert.That(c.CurrentOuterRadius, Is.EqualTo(radius).Within(.1));
            c.SetScanEnabled(true);
            yield return null;
            Assert.That(c.CurrentOuterRadius, Is.LessThan(radius));
        }
    }
}
