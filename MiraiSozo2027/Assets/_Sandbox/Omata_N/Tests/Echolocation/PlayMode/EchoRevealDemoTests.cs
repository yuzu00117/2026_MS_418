using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace Echo.Echolocation.Tests
{
    public sealed class EchoRevealDemoTests
    {
        private Scene _scene;
        [UnityTest]
        public IEnumerator DemoHasBoundDedicatedTagsAndBakedRegions()
        {
            yield return EchoTestSceneLoader.Load("Scene_EchoRevealDemo");
            _scene = SceneManager.GetSceneByName("Scene_EchoRevealDemo");
            EchoController controller = null;
            EchoRevealState partial = null;
            foreach (var root in _scene.GetRootGameObjects())
            {
                if (root.TryGetComponent<EchoController>(out var found)) controller = found;
                if (root.name == "RevealRegionExample") partial = root.GetComponent<EchoRevealState>();
            }
            Assert.That(controller, Is.Not.Null);
            Assert.That(partial, Is.Not.Null);
            foreach (var rule in controller.Settings.TagColorRules) Assert.That(rule.Definition, Is.Not.Null);
            Assert.That(partial.World.Controller, Is.SameAs(controller));
            controller.Settings.Duration = 0;
            controller.Settings.ScanMode = EchoScanMode.Follow;
            controller.Settings.Radius = 20;
            controller.Settings.Shadow.IsEnabled = false;
            controller.SetScanEnabled(true);
            float deadline = Time.realtimeSinceStartup + 5;
            while (!partial.IsActive && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(partial.IsActive, Is.True);
            Assert.That(partial.HasActiveCollision, Is.True);
            Assert.That(partial.RevealedFraction, Is.GreaterThan(.99f));
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            EchoTestSceneLoader.StopTeamInput();
            if (_scene.IsValid()) yield return SceneManager.UnloadSceneAsync(_scene);
        }
    }
}
