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
        public IEnumerator DemoUsesDedicatedTagsAndNoBakedRegions()
        {
            yield return EchoTestSceneLoader.Load("Scene_EchoGlitchDemo");
            _scene = SceneManager.GetSceneByName("Scene_EchoGlitchDemo");
            int count = 0;
            foreach (var root in _scene.GetRootGameObjects())
            {
                if (root.TryGetComponent<EchoTag>(out _))
                {
                    count++;
                    Assert.That(root.GetComponent<EchoGlitch>(), Is.Null);
                    Assert.That(root.GetComponent<EchoTag>().Definition, Is.Not.Null);
                    Assert.That(root.GetComponent<EchoRevealState>(), Is.Null);
                    Assert.That(root.GetComponent<EchoRevealCollider>(), Is.Null);
                }
            }
            Assert.That(count, Is.EqualTo(3));
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        { if (_scene.IsValid()) yield return SceneManager.UnloadSceneAsync(_scene); }
    }
}
