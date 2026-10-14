// 配布するデモそのものを読み込み、設定と描画が接続されていることを確認する。
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Echo.Echolocation.Tests
{
    public sealed class EchoDemoCaptureTests
    {
        private Scene _demo;
        private RenderTexture _output;
        private Camera _camera;
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (_camera)
                _camera.targetTexture = null;
            if (_output)
            {
                _output.Release();
                Object.Destroy(_output);
            }

            if (_demo.IsValid())
                yield return SceneManager.UnloadSceneAsync(_demo);
        }

        [UnityTest]
        public IEnumerator DemoCanBeOpenedAndScanned()
        {
            yield return SceneManager.LoadSceneAsync("Scene_EchoDemo", LoadSceneMode.Additive);
            _demo = SceneManager.GetSceneByName("Scene_EchoDemo");
            EchoController controller = null;
            foreach (var root in _demo.GetRootGameObjects())
                if (root.TryGetComponent<EchoController>(out var found))
                    controller = found;
            Assert.NotNull(controller);
            Assert.That(controller.GetComponent<EchoPrototypeInput>().Bindings.Count, Is.EqualTo(3));
            _camera = controller.TargetCamera;
            _output = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32);
            _output.Create();
            _camera.targetTexture = _output;
            controller.SetScanEnabled(true);
            yield return new WaitForSeconds(3.2f);
            yield return null;
            Assert.True(controller.IsScanRequested);
            Assert.That(controller.Frames.Count, Is.GreaterThan(0));
            Assert.That(controller.RenderItems.Count, Is.GreaterThan(3));
            var old = RenderTexture.active;
            RenderTexture.active = _output;
            var image = new Texture2D(960, 540, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
            image.Apply();
            RenderTexture.active = old;
            var path = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Artifacts"));
            Directory.CreateDirectory(path);
            File.WriteAllBytes(Path.Combine(path, "demo.png"), image.EncodeToPNG());
            Object.Destroy(image);
        }
    }
}
