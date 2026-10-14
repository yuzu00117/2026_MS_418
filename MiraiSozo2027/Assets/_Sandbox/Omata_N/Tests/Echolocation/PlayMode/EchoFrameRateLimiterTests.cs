using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Echo.Echolocation.Tests
{
    public sealed class EchoFrameRateLimiterTests
    {
        private GameObject _root;
        private int _originalFps;
        private int _originalVSync;
        [SetUp]
        public void SetUp()
        {
            _originalFps = Application.targetFrameRate;
            _originalVSync = QualitySettings.vSyncCount;
            _root = new GameObject("FrameRateTest");
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(_root);
            Application.targetFrameRate = _originalFps;
            QualitySettings.vSyncCount = _originalVSync;
        }

        [Test]
        public void AppliesCapAndRestoresPreviousGlobalSettings()
        {
            // VSyncが元々有効でもFPS指定を優先し、終了後に両方を戻せることを確認する。
            Application.targetFrameRate = 90;
            QualitySettings.vSyncCount = 1;
            var limiter = _root.AddComponent<EchoFrameRateLimiter>();
            Assert.AreEqual(60, Application.targetFrameRate);
            Assert.AreEqual(0, QualitySettings.vSyncCount);
            limiter.MaxFps = 144;
            Assert.AreEqual(144, Application.targetFrameRate);
            limiter.IsLimitEnabled = false;
            Assert.AreEqual(-1, Application.targetFrameRate);
            limiter.enabled = false;
            Assert.AreEqual(90, Application.targetFrameRate);
            Assert.AreEqual(1, QualitySettings.vSyncCount);
        }

        [UnityTest]
        public IEnumerator InspectorChangesApplyDuringPlay()
        {
            var limiter = _root.AddComponent<EchoFrameRateLimiter>();
            // Inspectorと同様にフィールドを書き換え、プロパティ経由でなくても更新されることを確認する。
            JsonUtility.FromJsonOverwrite("{\"_maxFps\":120}", limiter);
            yield return null;
            yield return null;
            Assert.AreEqual(120, Application.targetFrameRate);
            limiter.MaxFps = 0;
            Assert.AreEqual(1, limiter.MaxFps);
            limiter.MaxFps = 60;
        }

        [Test]
        public void DoesNotOverwriteSettingsChangedByAnotherSystemOnDisable()
        {
            var limiter = _root.AddComponent<EchoFrameRateLimiter>();
            Application.targetFrameRate = 75;
            QualitySettings.vSyncCount = 2;
            limiter.enabled = false;
            Assert.AreEqual(75, Application.targetFrameRate);
            Assert.AreEqual(2, QualitySettings.vSyncCount);
        }
    }
}
