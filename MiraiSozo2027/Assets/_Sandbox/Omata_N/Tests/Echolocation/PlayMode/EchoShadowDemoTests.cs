// 現在のデモで発生地点基準の影を描き、画像とCPU準備時間を保存する。
using System.Collections;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Echo.Echolocation.Tests
{
    public sealed class EchoShadowDemoTests
    {
        private Scene _scene;
        private Camera _camera;
        private RenderTexture _output;
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

            if (_scene.IsValid())
                yield return SceneManager.UnloadSceneAsync(_scene);
        }

        [UnityTest]
        public IEnumerator SourceShadowDemoRendersWithoutAddingHierarchyObjects()
        {
            yield return SceneManager.LoadSceneAsync("Scene_EchoDemo", LoadSceneMode.Additive);
            _scene = SceneManager.GetSceneByName("Scene_EchoDemo");
            EchoController controller = null;
            foreach (var root in _scene.GetRootGameObjects())
                if (root.TryGetComponent<EchoController>(out var found))
                    controller = found;
            Assert.NotNull(controller);
            int initialObjects = Object.FindObjectsByType<Transform>().Length;
            Assert.True(controller.Settings.Shadow.IsEnabled);
            controller.Settings.Duration = 4;
            controller.Settings.PulseInterval = 6;
            controller.Settings.PulseHoldDuration = 1;
            _camera = controller.TargetCamera;
            _output = new RenderTexture(640, 360, 24);
            _output.Create();
            _camera.targetTexture = _output;
            controller.SetScanEnabled(true);
            double maximum = 0, total = 0;
            int samples = 0;
            float timeout = Time.unscaledTime + 15;
            do
            {
                yield return null;
                double ms = controller.ShadowMaps.CpuMilliseconds;
                maximum = System.Math.Max(maximum, ms);
                total += ms;
                samples++;
            }
            while (controller.CurrentOuterRadius < 12 && Time.unscaledTime < timeout);
            Assert.True(controller.ShadowMaps.IsReady);
            Assert.That(controller.ShadowMaps.DrawCalls, Is.GreaterThan(0));
            Assert.That(Object.FindObjectsByType<Transform>().Length, Is.EqualTo(initialObjects));
            var previous = RenderTexture.active;
            RenderTexture.active = _output;
            var image = new Texture2D(640, 360, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 640, 360), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Artifacts"));
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, "source-shadow-demo.png"), image.EncodeToPNG());
            Object.Destroy(image);
            var shadow = controller.ShadowMaps;
            string report = string.Format(CultureInfo.InvariantCulture, "Shadow CPU preparation average: {0:F3} ms\nMaximum (including allocation): {1:F3} ms\nSamples: {2}\nUnique origins: {3}\nBlocker renderers: {4}\nShadow draw calls: {5}\nFace resolution: {6}\n", total / samples, maximum, samples, shadow.OriginCount, shadow.BlockerCount, shadow.DrawCalls, controller.Settings.Shadow.Resolution);
            File.WriteAllText(Path.Combine(folder, "source-shadow-cpu.txt"), report);
            Debug.Log(report);
        }
    }
}
