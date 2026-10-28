using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace Echo.Echolocation.Tests
{
    public sealed class EchoRevealTests
    {
        private const int Resolution = 512;
        private Scene _scene;
        private EchoGlitchDemo _demo;
        private EchoController _controller;
        private Camera _camera;
        private RenderTexture _output;
        private GameObject _partial;
        private GameObject _whole;
        [UnitySetUp]
        public IEnumerator Setup()
        {
            yield return EchoTestSceneLoader.Load("Scene_EchoGlitchDemo");
            _scene = SceneManager.GetSceneByName("Scene_EchoGlitchDemo");
            foreach (var root in _scene.GetRootGameObjects())
            {
                if (root.TryGetComponent<EchoGlitchDemo>(out var demo)) _demo = demo;
                if (root.name == "Contact boundary") _partial = root;
                if (root.name == "Reveal whole object") _whole = root;
            }
            Assert.That(_demo, Is.Not.Null);
            _demo.IsAutomatic = false;
            _controller = _demo.Controller;
            _controller.Settings.Duration = 0;
            _controller.Settings.OffMode = EchoOffMode.Immediate;
            _camera = _controller.TargetCamera;
            _output = new RenderTexture(Resolution, Resolution, 24);
            _output.Create(); _camera.targetTexture = _output;
            _demo.SetScanning(false);
            yield return new WaitForSeconds(.1f);
        }
        private EchoTagColorRule GetRule(GameObject target)
        {
            var definition = target.GetComponent<EchoTag>().Definition;
            return _controller.Settings.TagColorRules.Find(rule => rule.Definition == definition);
        }
        private Texture2D Read(string name = null)
        {
            var previous = RenderTexture.active;
            RenderTexture.active = _output;
            var image = new Texture2D(Resolution, Resolution, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Resolution, Resolution), 0, 0);
            image.Apply(); RenderTexture.active = previous;
            if (name != null)
            {
                string folder = Path.Combine(Application.dataPath, "_Sandbox/Omata_N/Verification~/Images");
                Directory.CreateDirectory(folder);
                File.WriteAllBytes(Path.Combine(folder, name + ".png"), image.EncodeToPNG());
            }
            return image;
        }
        private Color Pixel(Texture2D image, Vector3 world)
        {
            var p = _camera.WorldToScreenPoint(world);
            return image.GetPixel((int)p.x, (int)p.y);
        }
        private int GlitchPixels(Texture2D image, GameObject target)
        {
            var bounds = target.GetComponent<Renderer>().bounds;
            var min = new Vector2(Resolution, Resolution);
            var max = Vector2.zero;
            for (int corner = 0; corner < 8; corner++)
            {
                var offset = Vector3.Scale(bounds.extents, new Vector3(
                    (corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                Vector2 screen = _camera.WorldToScreenPoint(bounds.center + offset);
                min = Vector2.Min(min, screen); max = Vector2.Max(max, screen);
            }
            int count = 0;
            for (int y = (int)min.y; y < (int)max.y; y++)
                for (int x = (int)min.x; x < (int)max.x; x++)
                {
                    var color = image.GetPixel(x, y);
                    if (color.maxColorComponent - Mathf.Min(color.r, Mathf.Min(color.g, color.b)) > .3f) count++;
                }
            return count;
        }
        [UnityTest]
        public IEnumerator ContactRegionNeedsNoBakeAndLeavesCollidersUnchanged()
        {
            GetRule(_partial).Glitch.IsEnabled = false;
            _demo.SetScanning(true);
            yield return new WaitForSeconds(.15f);
            var image = Read("contact-region");
            Assert.That(Pixel(image, new Vector3(4, 2, 2)).r, Is.GreaterThan(.12f));
            Assert.That(Pixel(image, new Vector3(6, 2, 2)).r, Is.LessThan(.05f));
            Object.Destroy(image);
            Assert.That(_partial.GetComponentsInChildren<MeshCollider>().Length, Is.Zero);
            Assert.That(_partial.GetComponent<BoxCollider>().enabled, Is.True);
            Assert.That(_partial.GetComponentsInChildren<EchoRevealProxy>().Length, Is.Zero);
            _demo.SetScanning(false);
            yield return new WaitForSeconds(.1f);
            image = Read();
            Assert.That(Pixel(image, new Vector3(4, 2, 2)).r, Is.LessThan(.05f));
            Object.Destroy(image);
            Assert.That(_partial.GetComponent<BoxCollider>().enabled, Is.True);
        }
        [UnityTest]
        public IEnumerator WholeObjectGlitchExpiresAndRetriggersOnReveal()
        {
            _demo.SetScanning(true);
            yield return new WaitForSeconds(.1f);
            var image = Read("glitch-reveal");
            Assert.That(GlitchPixels(image, _whole), Is.GreaterThan(10));
            Object.Destroy(image);
            yield return new WaitForSeconds(GetRule(_whole).Glitch.Duration + .2f);
            image = Read("glitch-expired");
            Assert.That(GlitchPixels(image, _whole), Is.Zero);
            Object.Destroy(image);
            _demo.SetScanning(false);
            yield return new WaitForSeconds(.1f);
            _demo.SetScanning(true);
            yield return new WaitForSeconds(.1f);
            image = Read("glitch-retrigger");
            Assert.That(GlitchPixels(image, _whole), Is.GreaterThan(10));
            Object.Destroy(image);
        }
        [UnityTest]
        public IEnumerator PartialGlitchStaysInsideContactAndWorksWithoutOutline()
        {
            _demo.SetScanning(true);
            yield return new WaitForSeconds(.15f);
            var image = Read("glitch-contact-boundary");
            Assert.That(GlitchPixels(image, _partial), Is.GreaterThan(10));
            Assert.That(Pixel(image, new Vector3(6, 2, 2)).maxColorComponent, Is.LessThan(.1f));
            foreach (var rule in _controller.Settings.TagColorRules) Assert.That(rule.OutlineEnabled, Is.False);
            Object.Destroy(image);
        }
        [UnityTest]
        public IEnumerator MovingPartialObjectAndLegacyComponentsDoNotGeneratePhysics()
        {
            _partial.AddComponent<EchoRevealState>();
            _partial.AddComponent<EchoRevealCollider>();
            _partial.transform.position += Vector3.left;
            _demo.SetScanning(true);
            yield return new WaitForSeconds(.15f);
            Assert.That(_partial.GetComponent<BoxCollider>().enabled, Is.True);
            Assert.That(_partial.GetComponentsInChildren<MeshCollider>().Length, Is.Zero);
            Assert.That(_partial.GetComponent<EchoRevealState>().IsOperational, Is.False);
            var image = Read();
            Assert.That(Pixel(image, new Vector3(4, 2, 2)).maxColorComponent, Is.GreaterThan(.12f));
            Object.Destroy(image);
        }
        [UnityTest]
        public IEnumerator SharedTagGlitchSettingsApplyWithoutTargetComponents()
        {
            var rule = GetRule(_whole);
            rule.Glitch.Mode = EchoGlitchMode.WhileRevealed;
            _controller.Settings.Radius = 20;
            _partial.GetComponent<EchoTag>().Definition = rule.Definition;
            _demo.SetScanning(true);
            yield return new WaitForSeconds(.15f);
            var image = Read("glitch-shared-tag");
            Assert.That(GlitchPixels(image, _whole), Is.GreaterThan(10));
            Assert.That(GlitchPixels(image, _partial), Is.GreaterThan(10));
            Object.Destroy(image);
            rule.Glitch.IsEnabled = false;
            yield return new WaitForSeconds(.1f);
            image = Read();
            Assert.That(GlitchPixels(image, _whole), Is.Zero);
            Assert.That(GlitchPixels(image, _partial), Is.Zero);
            Assert.That(_whole.GetComponent<EchoGlitch>(), Is.Null);
            Assert.That(_partial.GetComponent<EchoGlitch>(), Is.Null);
            Object.Destroy(image);
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (_camera) _camera.targetTexture = null;
            if (_output) { _output.Release(); Object.Destroy(_output); }
            if (_scene.IsValid()) yield return SceneManager.UnloadSceneAsync(_scene);
        }
    }
}
