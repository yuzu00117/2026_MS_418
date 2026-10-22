using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
namespace Echo.Echolocation.Tests
{
    public sealed class EchoRevealTests
    {
        private readonly List<Object> _objects = new List<Object>();
        private EchoController _controller;
        private EchoRevealWorld _world;
        private EchoRevealState _state;
        private EchoTagColorRule _rule;
        private Camera _camera;
        private RenderTexture _output;
        private BoxCollider _originalCollider;
        private T Keep<T>(T value) where T : Object { _objects.Add(value); return value; }
        private Mesh Triangle(float x)
        {
            var mesh = Keep(new Mesh());
            mesh.vertices = new[] { new Vector3(x - .5f, -.5f, 5), new Vector3(x, .5f, 5), new Vector3(x + .5f, -.5f, 5) };
            mesh.triangles = new[] { 0, 1, 2 };
            mesh.uv3 = new[] { Vector2.zero, Vector2.right, Vector2.up };
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            return mesh;
        }
        [UnitySetUp]
        public IEnumerator Setup()
        {
            var cameraObject = Keep(new GameObject("Reveal camera", typeof(Camera)));
            _camera = cameraObject.GetComponent<Camera>();
            _camera.backgroundColor = Color.black; _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.fieldOfView = 90; _camera.farClipPlane = 30;
            _output = Keep(new RenderTexture(256, 256, 24)); _output.Create(); _camera.targetTexture = _output;
            _controller = cameraObject.AddComponent<EchoController>();
            _controller.TargetCamera = _camera;
            _controller.Settings.Duration = 0; _controller.Settings.Radius = 5.3f;
            _controller.Settings.Shadow.IsEnabled = false;
            _world = cameraObject.AddComponent<EchoRevealWorld>();
            var definition = Keep(ScriptableObject.CreateInstance<EchoTagDefinition>());
            _rule = new EchoTagColorRule { Definition = definition, OriginalEnabled = true, FillEnabled = false,
                OriginalHoldDuration = .15f, RevealMode = EchoOriginalRevealMode.ContactRegion };
            _controller.Settings.TagColorRules.Add(_rule);
            var target = Keep(new GameObject("Reveal target", typeof(MeshFilter), typeof(MeshRenderer)));
            var left = Triangle(-1); var right = Triangle(4);
            var mesh = Keep(new Mesh());
            mesh.CombineMeshes(new[] { new CombineInstance { mesh = left, transform = Matrix4x4.identity },
                new CombineInstance { mesh = right, transform = Matrix4x4.identity } });
            target.GetComponent<MeshFilter>().sharedMesh = mesh;
            var material = Keep(new Material(Shader.Find("Universal Render Pipeline/Unlit")));
            material.SetColor("_BaseColor", Color.red);
            var renderer = target.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            target.AddComponent<EchoTag>().Definition = definition;
            _state = target.AddComponent<EchoRevealState>();
            var data = Keep(ScriptableObject.CreateInstance<EchoSurfaceData>());
            data.Initialize(mesh, Vector3.one, "", new List<EchoSurfacePatch>
            { new EchoSurfacePatch(left, 0, .5f), new EchoSurfacePatch(right, 0, .5f) });
            _state.Configure(_world, new[] { new EchoSurfaceBinding(renderer, data) });
            _originalCollider = target.AddComponent<BoxCollider>();
            target.AddComponent<EchoRevealCollider>().Configure(EchoRevealCollisionMode.RevealedRegion, new Collider[] { _originalCollider }, 0);
            _controller.RefreshTargets();
            yield return null;
        }
        private IEnumerator WaitActive()
        {
            _controller.SetScanEnabled(true);
            float deadline = Time.realtimeSinceStartup + 5;
            while (!_state.IsActive && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(_state.IsActive, Is.True, "GPU接触結果が物理更新へ反映される");
            yield return null;
        }
        private Color Pixel(Vector3 position)
        {
            var previous = RenderTexture.active; RenderTexture.active = _output;
            var image = new Texture2D(256, 256, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 256, 256), 0, 0); image.Apply();
            var point = _camera.WorldToScreenPoint(position);
            var result = image.GetPixel((int)point.x, (int)point.y);
            Object.Destroy(image); RenderTexture.active = previous; return result;
        }
        [UnityTest]
        public IEnumerator PartialRenderingCollisionAndStateShareTheSameRegion()
        {
            yield return WaitActive();
            Assert.That(_state.RevealedFraction, Is.EqualTo(.5f).Within(.001f));
            Assert.That(_state.HasActiveCollision, Is.True);
            Assert.That(_originalCollider.enabled, Is.False);
            Assert.That(Physics.Raycast(new Vector3(-1, 0, 0), Vector3.forward, 10), Is.True);
            Assert.That(Physics.Raycast(new Vector3(4, 0, 0), Vector3.forward, 10), Is.False);
            Assert.That(Pixel(new Vector3(-1, 0, 5)).r, Is.GreaterThan(.5f));
            Assert.That(Pixel(new Vector3(4, 0, 5)).r, Is.LessThan(.1f));
            uint revision = _state.Revision;
            EchoRevealSnapshot snapshot = default;
            _state.Changed += value => snapshot = value;
            _controller.SetScanEnabled(false);
            yield return new WaitForSeconds(.5f);
            Assert.That(_state.IsActive, Is.False);
            Assert.That(_state.HasActiveCollision, Is.False);
            Assert.That(snapshot.Revision, Is.GreaterThan(revision));
            Assert.That(snapshot.IsActive, Is.False);
            _world.enabled = false;
            Assert.That(_originalCollider.enabled, Is.True, "元のCollider状態を復元する");
        }
        [UnityTest]
        public IEnumerator ExtraBakedInactiveRendererDoesNotPreventReveal()
        {
            var existing = _state.Surfaces[0];
            var child = new GameObject("Inactive baked renderer", typeof(MeshFilter), typeof(MeshRenderer));
            child.transform.SetParent(_state.transform, false);
            child.GetComponent<MeshFilter>().sharedMesh = existing.Data.SourceMesh;
            child.GetComponent<MeshRenderer>().sharedMaterial = _state.GetComponent<MeshRenderer>().sharedMaterial;
            child.SetActive(false);
            _state.Configure(_world, new[] { existing, new EchoSurfaceBinding(child.GetComponent<MeshRenderer>(), existing.Data) });
            yield return WaitActive();
            Assert.That(_state.IsOperational, Is.True);
            Assert.That(_state.RevealedFraction, Is.EqualTo(.5f).Within(.001f));
        }
        [UnityTest]
        public IEnumerator MissingBakeReportsTheRendererAndRecoveryAction()
        {
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("エコー表示状態：「Reveal target」の分割データがありません"));
            _state.Configure(_world, System.Array.Empty<EchoSurfaceBinding>());
            yield return null;
            Assert.That(_state.IsOperational, Is.False);
        }
        [UnityTest]
        public IEnumerator ContactAtlasPreservesPatchIndicesAcrossRows()
        {
            var binding = _state.Surfaces[0];
            var patches = new List<EchoSurfacePatch>();
            for (int i = 0; i < 9; i++)
                patches.Add(new EchoSurfacePatch(Triangle(i == 7 ? -1 : 4), 0, .5f));
            var data = Keep(ScriptableObject.CreateInstance<EchoSurfaceData>());
            data.Initialize(binding.Data.SourceMesh, Vector3.one, "", patches);
            _state.Configure(_world, new[] { new EchoSurfaceBinding(binding.Renderer, data) });
            yield return WaitActive();
            Assert.That(_state.RevealedFraction, Is.EqualTo(1f / 9).Within(.001f));
            Assert.That(Physics.Raycast(new Vector3(-1, 0, 0), Vector3.forward, 10), Is.True);
            Assert.That(Physics.Raycast(new Vector3(4, 0, 0), Vector3.forward, 10), Is.False);
        }
        [UnityTest]
        public IEnumerator WholeObjectAndOffscreenStateAreSupported()
        {
            _state.GetComponent<EchoRevealCollider>().Configure(EchoRevealCollisionMode.WholeObject, new Collider[] { _originalCollider }, 0);
            _rule.RevealMode = EchoOriginalRevealMode.WholeObject;
            yield return WaitActive();
            Assert.That(_state.RevealedFraction, Is.EqualTo(1));
            Assert.That(Pixel(new Vector3(4, 0, 5)).r, Is.GreaterThan(.5f));
            _camera.transform.rotation = Quaternion.Euler(0, 180, 0);
            yield return new WaitForSeconds(.1f);
            Assert.That(_state.IsActive, Is.True, "カメラ外でもワールド状態は維持");
            _state.enabled = false;
            Assert.That(_state.IsActive, Is.False);
            Assert.That(_originalCollider.enabled, Is.True);
        }
        [UnityTest]
        public IEnumerator DisabledSourceAndPauseDoNotLeaveStaleCollision()
        {
            yield return WaitActive();
            var renderer = _state.GetComponent<MeshRenderer>();
            renderer.enabled = false;
            yield return new WaitForSeconds(.1f);
            Assert.That(_state.IsActive, Is.False);
            Assert.That(_state.HasActiveCollision, Is.False);
            renderer.enabled = true;
            yield return WaitActive();
            uint revision = _state.Revision;
            Time.timeScale = 0;
            _controller.SetScanEnabled(false);
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(_state.Revision, Is.EqualTo(revision), "停止中は表示と物理を同じ確定状態に保つ");
            Time.timeScale = 1;
            yield return new WaitForSeconds(.3f);
            Assert.That(_state.IsActive, Is.False);
        }
        [UnityTest]
        public IEnumerator InvalidatedPartialTargetStopsManagedCollisionUntilDisabled()
        {
            yield return WaitActive();
            LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("エコー表示状態：部分表示対象が移動しました"));
            _state.transform.position += Vector3.right;
            yield return null;
            yield return new WaitForFixedUpdate();
            Assert.That(_state.IsOperational, Is.False);
            Assert.That(_state.IsActive, Is.False);
            Assert.That(_originalCollider.enabled, Is.False, "失敗中は透明な通常Colliderを戻さない");
            _state.enabled = false;
            yield return null;
            Assert.That(_originalCollider.enabled, Is.True, "機能の解除時に初期状態へ戻す");
            Assert.That(EchoTargetRegistry.GetInvisibleMaterials(_state.GetComponent<Renderer>()), Is.Null);
        }
        [UnityTest]
        public IEnumerator PenetrationRetainsBothAppearanceAndCollisionUntilClear()
        {
            _state.Release();
            _state.GetComponent<EchoRevealCollider>().Configure(EchoRevealCollisionMode.RevealedRegion,
                new Collider[] { _originalCollider }, 1 << 8);
            var actor = Keep(new GameObject("Occupant", typeof(SphereCollider), typeof(Rigidbody)));
            actor.layer = 8;
            actor.GetComponent<SphereCollider>().radius = .3f;
            actor.GetComponent<Rigidbody>().isKinematic = true;
            actor.transform.position = new Vector3(-1, 0, 4.9f);
            Physics.SyncTransforms();
            yield return WaitActive();
            _controller.SetScanEnabled(false);
            yield return new WaitForSeconds(.5f);
            Assert.That(_state.PendingPatchCount, Is.GreaterThan(0));
            Assert.That(_state.IsActive, Is.True);
            Assert.That(_state.HasActiveCollision, Is.True);
            Assert.That(Pixel(new Vector3(-1, 0, 5)).r, Is.GreaterThan(.5f));
            actor.transform.position = new Vector3(-1, 4, 4.9f);
            Physics.SyncTransforms();
            yield return new WaitForSeconds(.1f);
            Assert.That(_state.PendingPatchCount, Is.Zero);
            Assert.That(_state.IsActive, Is.False);
            Assert.That(_state.HasActiveCollision, Is.False);
        }
        [UnityTest]
        public IEnumerator InitiallyDisabledColliderStaysDisabled()
        {
            _state.Release();
            _originalCollider.enabled = false;
            yield return WaitActive();
            Assert.That(_state.IsActive, Is.True);
            Assert.That(_state.HasActiveCollision, Is.False);
            _state.enabled = false;
            Assert.That(_originalCollider.enabled, Is.False);
        }
        [UnityTest]
        public IEnumerator SourceOcclusionPreventsPartialReveal()
        {
            var wall = Keep(GameObject.CreatePrimitive(PrimitiveType.Cube));
            wall.transform.position = new Vector3(0, 0, 2.5f); wall.transform.localScale = new Vector3(10, 10, .2f);
            wall.AddComponent<EchoOccluder>();
            _controller.Settings.Shadow.IsEnabled = true;
            _controller.SetScanEnabled(true);
            yield return new WaitForSeconds(.5f);
            Assert.That(_state.IsOperational, Is.True);
            Assert.That(_state.IsActive, Is.False);
            Assert.That(_state.HasActiveCollision, Is.False);
        }
        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            Time.timeScale = 1;
            if (_state) _state.Release();
            if (_camera) _camera.targetTexture = null;
            foreach (var value in _objects) if (value) Object.Destroy(value);
            _objects.Clear();
            yield return null;
        }
    }
}
