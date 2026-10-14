// 仮想入力と実際のデモを使い、プレイヤー・視点・スキャンの中心の整合を検証する。
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Echo.Echolocation.Samples;

namespace Echo.Echolocation.Tests
{
    public sealed class EchoPlayerTests
    {
        private Scene _scene;
        private EchoDemoPlayer _player;
        private EchoController _scan;
        private Keyboard _keyboard;
        private Mouse _mouse;
        private GameObject _obstruction;
        private InputSettings.BackgroundBehavior _oldBackground;
        private 
#if UNITY_EDITOR
        InputSettings.EditorInputBehaviorInPlayMode _oldEditor;
#endif
        [UnitySetUp]
        public IEnumerator Setup()
        {
            var settings = UnityEngine.InputSystem.InputSystem.settings;
            _oldBackground = settings.backgroundBehavior;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
#if UNITY_EDITOR
            _oldEditor = settings.editorInputBehaviorInPlayMode;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
#endif
            _keyboard = UnityEngine.InputSystem.InputSystem.AddDevice<Keyboard>();
            _mouse = UnityEngine.InputSystem.InputSystem.AddDevice<Mouse>();
            yield return SceneManager.LoadSceneAsync("Scene_EchoDemo", LoadSceneMode.Additive);
            _scene = SceneManager.GetSceneByName("Scene_EchoDemo");
            foreach (var root in _scene.GetRootGameObjects())
                if (root.TryGetComponent<EchoDemoPlayer>(out var p))
                    _player = p;
            Assert.NotNull(_player);
            _scan = _player.GetComponent<EchoController>();
            yield return new WaitForSeconds(.25f);
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (_obstruction)
                Object.Destroy(_obstruction);
            if (_scene.IsValid())
                yield return SceneManager.UnloadSceneAsync(_scene);
            if (_keyboard != null)
                UnityEngine.InputSystem.InputSystem.RemoveDevice(_keyboard);
            if (_mouse != null)
                UnityEngine.InputSystem.InputSystem.RemoveDevice(_mouse);
            var settings = UnityEngine.InputSystem.InputSystem.settings;
            settings.backgroundBehavior = _oldBackground;
#if UNITY_EDITOR
            settings.editorInputBehaviorInPlayMode = _oldEditor;
#endif
        }

        [UnityTest]
        public IEnumerator ZTogglesViewWithoutMovingScanOrigin()
        {
            Assert.False(_player.IsThirdPerson);
            var camera = _scan.TargetCamera;
            var origin = _scan.OriginTransform.position;
            var direction = _scan.DirectionTransform.forward;
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.Z));
            yield return null;
            yield return null;
            Assert.True(_player.IsThirdPerson);
            Assert.That(_player.CurrentCameraDistance, Is.GreaterThan(3));
            Assert.AreSame(camera, _scan.TargetCamera);
            Assert.That(Vector3.Distance(origin, _scan.OriginTransform.position), Is.LessThan(.02));
            Assert.That(Vector3.Angle(direction, _scan.DirectionTransform.forward), Is.LessThan(.01));
            yield return null;
            Assert.True(_player.IsThirdPerson, "長押しで再反転しない");
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            yield return null;
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.Z));
            yield return null;
            yield return null;
            Assert.False(_player.IsThirdPerson);
            Assert.That(_player.CurrentCameraDistance, Is.Zero);
        }

        [UnityTest]
        public IEnumerator MouseRotatesAndWASDMovesThePlayer()
        {
            var before = _player.transform.position;
            float yaw = _player.transform.eulerAngles.y;
            UnityEngine.InputSystem.InputSystem.QueueDeltaStateEvent(_mouse.delta, new Vector2(150, 0));
            yield return null;
            yield return null;
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(yaw, _player.transform.eulerAngles.y)), Is.GreaterThan(5));
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.W));
            yield return new WaitForSeconds(.2f);
            Assert.That(Vector3.Distance(before, _player.transform.position), Is.GreaterThan(.5f));
            Assert.That(_player.transform.position.y, Is.InRange(-.1f, .2f), "地面をすり抜けない");
        }

        [UnityTest]
        public IEnumerator CharacterAndThirdPersonCameraStopAtWalls()
        {
            _obstruction = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _obstruction.transform.position = _player.transform.position + Vector3.forward * 1.5f + Vector3.up;
            _obstruction.transform.localScale = new Vector3(4, 4, .2f);
            Physics.SyncTransforms();
            var before = _player.transform.position;
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(_keyboard, new KeyboardState(Key.W));
            yield return new WaitForSeconds(.45f);
            Assert.That(_player.transform.position.z - before.z, Is.LessThan(1.3f));
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(_keyboard, new KeyboardState());
            yield return null;
            _obstruction.transform.position = _scan.DirectionTransform.position - _scan.DirectionTransform.forward * 2;
            Physics.SyncTransforms();
            _player.SetThirdPerson(true);
            yield return null;
            Assert.That(_player.CurrentCameraDistance, Is.InRange(.1f, 2f), "カメラが背後の壁の手前へ寄る");
        }

        [UnityTest]
        public IEnumerator ThirdPersonAndRangeCanBeCaptured()
        {
            // 波が床の上を進む段階を三人称で撮影する。
            _player.SetThirdPerson(true);
            _scan.Settings.Radius = 12;
            _scan.Settings.Duration = 1;
            _scan.SetScanEnabled(true);
            yield return new WaitForSeconds(.75f);
            var camera = _scan.TargetCamera;
            var output = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32);
            output.Create();
            camera.targetTexture = output;
            yield return null;
            yield return null;
            var old = RenderTexture.active;
            RenderTexture.active = output;
            var image = new Texture2D(960, 540, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 960, 540), 0, 0);
            image.Apply();
            RenderTexture.active = old;
            var folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Artifacts"));
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, "third-person-range.png"), image.EncodeToPNG());
            camera.targetTexture = null;
            output.Release();
            Object.Destroy(output);
            Object.Destroy(image);
            Assert.True(_player.IsThirdPerson);
            Assert.That(_scan.CurrentOuterRadius, Is.GreaterThan(0));
            Assert.NotNull(_player.GetComponent<EchoRangeDisplay>());
        }

        [UnityTest]
        public IEnumerator ThirdPersonHeightIsIndependentAndDoesNotChangeFirstPerson()
        {
            foreach (var root in _scene.GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    foreach (char ch in t.name)
                        Assert.That((int)ch, Is.LessThan(128), "Hierarchyは短い英語名にする");
            _player.ThirdPersonHeight = 0;
            _player.SetThirdPerson(true);
            var initial = _player.ViewCamera.transform.position;
            var rotation = _player.ViewCamera.transform.rotation;
            var scanOrigin = _scan.OriginTransform.position;
            _player.ThirdPersonHeight = 1.25f;
            Assert.That(Vector3.Distance(_player.ViewCamera.transform.position, initial + Vector3.up * 1.25f), Is.LessThan(.001f));
            Assert.That(Quaternion.Angle(_player.ViewCamera.transform.rotation, rotation), Is.LessThan(.001f));
            Assert.That(_scan.OriginTransform.position, Is.EqualTo(scanOrigin));
            _player.ThirdPersonHeight = -.5f;
            Assert.That(Vector3.Distance(_player.ViewCamera.transform.position, initial - Vector3.up * .5f), Is.LessThan(.001f));
            _player.SetThirdPerson(false);
            Assert.That(Vector3.Distance(_player.ViewCamera.transform.position, _scan.DirectionTransform.position), Is.LessThan(.001f));
            _player.ThirdPersonHeight = 3;
            Assert.That(Vector3.Distance(_player.ViewCamera.transform.position, _scan.DirectionTransform.position), Is.LessThan(.001f));
            yield return null;
        }

        [UnityTest]
        public IEnumerator CameraCollisionFollowsTheRaisedDiagonalPath()
        {
            _player.ThirdPersonHeight = 2;
            _player.SetThirdPerson(true);
            var origin = _scan.DirectionTransform.position;
            var desired = _player.ViewCamera.transform.position;
            float fullDistance = _player.CurrentCameraDistance;
            _obstruction = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _obstruction.name = "CameraBlock";
            _obstruction.transform.position = Vector3.Lerp(origin, desired, .65f);
            _obstruction.transform.localScale = Vector3.one;
            Physics.SyncTransforms();
            yield return null;
            Assert.That(_player.CurrentCameraDistance, Is.LessThan(fullDistance - .5f));
            Assert.That(Vector3.Angle(_player.ViewCamera.transform.position - origin, desired - origin), Is.LessThan(.1f));
            Assert.That(Vector3.Distance(_obstruction.GetComponent<Collider>().ClosestPoint(_player.ViewCamera.transform.position), _player.ViewCamera.transform.position), Is.GreaterThan(.1f));
        }
    }
}
