using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
namespace Echo.Echolocation.Tests
{
    public sealed class EchoTagAssetRenderingTests
    {
        private const int Resolution = 512;
        private Scene _scene;
        private const int EffectCount = 3;
        private EchoDemoDriver _demo;
        private EchoController _controller;
        private Camera _camera;
        private GameObject _target;
        private EchoTagDefinition _tag;
        private Material _material;
        private RenderTexture _output;
        private readonly List<Object> _created = new List<Object>();
        [UnitySetUp]
        public IEnumerator Setup()
        {
            yield return EchoTestSceneLoader.Load("Scene_EchoTagEffectsDemo");
            _scene = SceneManager.GetSceneByName("Scene_EchoTagEffectsDemo");
            foreach (var root in _scene.GetRootGameObjects())
            {
                if (root.TryGetComponent<EchoDemoDriver>(out var demo)) _demo = demo;
                if (root.name == "元マテリアル") _target = root;
                if (root.TryGetComponent<EchoTarget>(out var tag) && tag.Definition)
                {
                    var copy = Object.Instantiate(tag.Definition); _created.Add(copy); tag.Definition = copy;
                    SetEnabled(copy.Settings, -1);
                    if (root.name != "元マテリアル") root.GetComponent<Renderer>().enabled = false;
                }
            }
            _demo.IsAutomatic = false; _controller = _demo.Controller; _camera = _controller.TargetCamera;
            _controller.Settings.Duration = 0; _controller.Settings.Radius = 5; _controller.Settings.OffMode = EchoOffMode.Immediate;
            _controller.transform.position = new Vector3(-3, 2, 0);
            _target.transform.position = new Vector3(0, 2, 5); _target.transform.localScale = new Vector3(6, 4, 2);
            _tag = _target.GetComponent<EchoTarget>().Definition;
            _material = new Material(Shader.Find("Universal Render Pipeline/Unlit")); _created.Add(_material);
            _material.SetColor("_BaseColor", Color.black); _target.GetComponent<Renderer>().sharedMaterial = _material;
            _output = new RenderTexture(Resolution, Resolution, 24); _output.Create(); _camera.targetTexture = _output;
            _demo.SetScanning(false); yield return new WaitForSeconds(.1f);
        }
        private static EchoEffectSettings GetEffect(EchoTagColorRule rule, int effect) =>
            effect == 0 ? rule.Fill : effect == 1 ? rule.Outline : rule.Original;
        private static void SetEnabled(EchoTagColorRule rule, int effect)
        {
            rule.Fill.IsEnabled = effect == 0; rule.Outline.IsEnabled = effect == 1;
            rule.Original.IsEnabled = effect == 2;
        }
        private Texture2D Read(string name = null)
        {
            var previous = RenderTexture.active; RenderTexture.active = _output;
            var image = new Texture2D(Resolution, Resolution, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, Resolution, Resolution), 0, 0); image.Apply(); RenderTexture.active = previous;
            if (name != null)
            {
                string folder = Path.Combine(Application.dataPath, "_Sandbox/Omata_N/Verification~/Images");
                Directory.CreateDirectory(folder); File.WriteAllBytes(Path.Combine(folder, name + ".png"), image.EncodeToPNG());
            }
            return image;
        }
        private Color Pixel(Texture2D image, float x, float y = 2)
        {
            var screen = _camera.WorldToScreenPoint(new Vector3(x, y, 4));
            return image.GetPixel((int)screen.x, (int)screen.y);
        }
        private int LitPixels(Texture2D image, float fromX, float toX, bool isInterior = false)
        {
            var min = _camera.WorldToScreenPoint(new Vector3(fromX, isInterior ? 1 : .1f, 4));
            var max = _camera.WorldToScreenPoint(new Vector3(toX, isInterior ? 3 : 3.9f, 4));
            int count = 0;
            for (int y = (int)min.y; y < (int)max.y; y++) for (int x = (int)min.x; x < (int)max.x; x++)
                if (image.GetPixel(x, y).maxColorComponent > .25f) count++;
            return count;
        }
        [UnityTest]
        public IEnumerator AllThreeEffectsSupportContactWholeAndIndependentEnable()
        {
            for (int index = 0; index < EffectCount; index++)
            {
                var rule = _tag.Settings; SetEnabled(rule, index);
                var effect = GetEffect(rule, index); effect.HasPersistence = false; effect.DisplayScope = EchoOriginalRevealMode.ContactRegion;
                _material.SetColor("_BaseColor", index == 2 ? Color.red : Color.black);
                _demo.SetScanning(true); yield return new WaitForSeconds(.1f);
                var image = Read("asset-contact-" + index);
                Assert.That(LitPixels(image, -2.9f, -.2f), Is.GreaterThan(10), "接触表示 " + index);
                Assert.That(LitPixels(image, 1.2f, 2.8f), Is.Zero, "未接触を隠す " + index); Object.Destroy(image);
                effect.DisplayScope = EchoOriginalRevealMode.WholeObject;
                yield return new WaitForSeconds(.1f); image = Read("asset-whole-" + index);
                Assert.That(LitPixels(image, 1.2f, 2.99f), Is.GreaterThan(10), "全体表示 " + index); Object.Destroy(image);
                _demo.SetScanning(false); yield return new WaitForSeconds(.1f);
            }
        }
        [UnityTest]
        public IEnumerator AllThreeEffectsChoosePartialOrWholePersistenceAndExpire()
        {
            for (int index = 0; index < EffectCount; index++)
            {
                SetEnabled(_tag.Settings, index); var effect = GetEffect(_tag.Settings, index);
                effect.DisplayScope = EchoOriginalRevealMode.ContactRegion; effect.HasPersistence = true; effect.PersistenceSeconds = .5f;
                _material.SetColor("_BaseColor", index == 2 ? Color.red : Color.black);
                foreach (var scope in new[] { EchoOriginalRevealMode.ContactRegion, EchoOriginalRevealMode.WholeObject })
                {
                    effect.DisplayScope = scope;
                    _demo.SetScanning(true); yield return new WaitForSeconds(.1f);
                    _demo.SetScanning(false); yield return new WaitForSeconds(.1f);
                    var image = Read("asset-persist-" + index + "-" + scope);
                    Assert.That(LitPixels(image, -2.9f, -.2f), Is.GreaterThan(10), "残存 " + index);
                    if (scope == EchoOriginalRevealMode.ContactRegion) Assert.That(LitPixels(image, 1.2f, 2.8f), Is.Zero);
                    else Assert.That(LitPixels(image, 1.2f, 2.99f), Is.GreaterThan(10));
                    Object.Destroy(image);
                    yield return new WaitForSeconds(.55f); image = Read();
                    Assert.That(LitPixels(image, -2.9f, 2.9f), Is.Zero, "残存終了 " + index); Object.Destroy(image);
                }
            }
        }
        [UnityTest]
        public IEnumerator SurfacePersistenceSurvivesCameraAndTargetMovement()
        {
            SetEnabled(_tag.Settings, 2); var effect = _tag.Settings.Original;
            effect.DisplayScope = EchoOriginalRevealMode.ContactRegion; effect.HasPersistence = true;
            effect.PersistenceSeconds = 1;
            _material.SetColor("_BaseColor", Color.red); _demo.SetScanning(true); yield return new WaitForSeconds(.1f);
            _demo.SetScanning(false); _camera.transform.position += Vector3.right;
            _target.transform.position += Vector3.right; yield return new WaitForSeconds(.1f);
            var image = Read("asset-moving-memory");
            Assert.That(Pixel(image, -1).r, Is.GreaterThan(.5f)); Assert.That(Pixel(image, 3).r, Is.LessThan(.05f)); Object.Destroy(image);
        }
        [UnityTest]
        public IEnumerator UntaggedObjectsBlockDespiteLegacyOverridesAndTaggedObjectsCanPass()
        {
            SetEnabled(_tag.Settings, 2); _tag.Settings.Original.HasPersistence = false;
            _tag.Settings.Original.DisplayScope = EchoOriginalRevealMode.WholeObject;
            _tag.Settings.Original.IsThroughWalls = true; _material.SetColor("_BaseColor", Color.red);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); _created.Add(wall);
            wall.transform.position = new Vector3(-1.5f, 2, 2); wall.transform.localScale = new Vector3(12, 12, .2f);
            var wallMaterial = new Material(_material); wallMaterial.SetColor("_BaseColor", Color.black);
            _created.Add(wallMaterial); wall.GetComponent<Renderer>().sharedMaterial = wallMaterial;
            wall.AddComponent<EchoOccluder>().IsBlocking = false;
            _controller.Settings.Shadow.IsEnabled = true; _controller.Settings.Shadow.IsBlockingByDefault = false;
            _controller.RefreshTargets(); _demo.SetScanning(true); yield return new WaitForSeconds(.15f);
            Assert.That(_controller.ShadowMaps.BlockerCount, Is.GreaterThan(0));
            var blocked = Read(); Assert.That(Pixel(blocked, -2).r, Is.LessThan(.05f)); Object.Destroy(blocked);
            var passTag = ScriptableObject.CreateInstance<EchoTagDefinition>(); _created.Add(passTag); passTag.IsBlocking = false;
            SetEnabled(passTag.Settings, -1); wall.AddComponent<EchoTarget>().Definition = passTag;
            yield return new WaitForSeconds(.15f);
            Assert.That(_controller.ShadowMaps.BlockerCount, Is.EqualTo(0));
            var unblocked = Read(); Assert.That(Pixel(unblocked, -2).r, Is.GreaterThan(.5f)); Object.Destroy(unblocked);
        }
        [UnityTest]
        public IEnumerator LegacyUnityTagAloneDoesNotEnableAnEffect()
        {
            SetEnabled(_tag.Settings, 0); _target.tag = "Player";
            _controller.Settings.TagColorRules.Add(new EchoTagColorRule { TagName = "Player", Color = Color.green });
            _target.GetComponent<EchoTarget>().Definition = null; _demo.SetScanning(true); yield return new WaitForSeconds(.1f);
            var image = Read(); Assert.That(LitPixels(image, -2.9f, 2.9f), Is.Zero); Object.Destroy(image);
        }
        [UnityTest]
        public IEnumerator EveryEffectControlsCameraOcclusionIndependently()
        {
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); _created.Add(wall);
            wall.transform.position = new Vector3(0, 2, 0); wall.transform.localScale = new Vector3(20, 20, .2f);
            var wallMaterial = new Material(_material); wallMaterial.SetColor("_BaseColor", Color.black); _created.Add(wallMaterial);
            wall.GetComponent<Renderer>().sharedMaterial = wallMaterial;
            _controller.Settings.Shadow.IsEnabled = false; _demo.SetScanning(true);
            for (int index = 0; index < EffectCount; index++)
            {
                SetEnabled(_tag.Settings, index); var effect = GetEffect(_tag.Settings, index);
                effect.HasPersistence = false; effect.DisplayScope = EchoOriginalRevealMode.WholeObject;
                _material.SetColor("_BaseColor", index == 2 ? Color.red : Color.black);
                effect.IsThroughWalls = false; yield return new WaitForSeconds(.1f);
                var image = Read(); Assert.That(LitPixels(image, -2.99f, 2.99f), Is.Zero, "壁で隠す " + index); Object.Destroy(image);
                effect.IsThroughWalls = true; yield return new WaitForSeconds(.1f);
                image = Read(); Assert.That(LitPixels(image, -2.99f, 2.99f), Is.GreaterThan(10), "壁越し " + index); Object.Destroy(image);
            }
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (_camera) _camera.targetTexture = null;
            if (_output) { _output.Release(); Object.Destroy(_output); }
            if (_scene.IsValid()) yield return SceneManager.UnloadSceneAsync(_scene);
            foreach (var created in _created) if (created) Object.Destroy(created);
            _created.Clear(); yield return null;
        }
    }
}
