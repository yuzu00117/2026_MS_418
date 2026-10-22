// GPU画像で壁越し・深度遮蔽・表面単位の範囲判定を検証する。Colliderなしでも同じ経路を使う。
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Rendering.Universal;

namespace Echo.Echolocation.Tests
{
    public sealed class EchoRenderingTests
    {
        private GameObject _cameraObject;
        private GameObject _target;
        private GameObject _wall;
        private Material _mat;
        private RenderTexture _output;
        private Camera _camera;
        private EchoController _controller;
        [UnitySetUp]
        public IEnumerator Setup()
        {
            _cameraObject = new GameObject("Render test camera", typeof(Camera));
            _camera = _cameraObject.GetComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Color.black;
            _camera.nearClipPlane = .1f;
            _camera.farClipPlane = 30;
            _camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            _output = new RenderTexture(256, 256, 24, RenderTextureFormat.ARGB32);
            _output.Create();
            _camera.targetTexture = _output;
            _mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            _mat.SetColor("_BaseColor", new Color(.15f, .15f, .15f));
            _target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _target.name = "Echo image target";
            _target.tag = "Player";
            _target.transform.position = new Vector3(0, 0, 5);
            _target.transform.localScale = new Vector3(5, 5, .2f);
            _target.GetComponent<Renderer>().sharedMaterial = _mat;
            Object.Destroy(_target.GetComponent<Collider>());
            _controller = _cameraObject.AddComponent<EchoController>();
            _controller.TargetCamera = _camera;
            _controller.Settings.Duration = 0;
            _controller.Settings.Radius = 10;
            _controller.Settings.Shadow.IsEnabled = false;
            _controller.Settings.TagColorRules.Add(new EchoTagColorRule { TagName = "Player", Color = Color.green });
            _controller.RefreshTargets();
            yield return null;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator Cleanup()
        {
            if (_camera)
                _camera.targetTexture = null;
            Object.Destroy(_cameraObject);
            Object.Destroy(_target);
            if (_wall)
                Object.Destroy(_wall);
            Object.Destroy(_mat);
            _output.Release();
            Object.Destroy(_output);
            yield return null;
        }

        Texture2D Read()
        {
            var old = RenderTexture.active;
            RenderTexture.active = _output;
            var image = new Texture2D(256, 256, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
            image.Apply();
            RenderTexture.active = old;
            return image;
        }

        int GetGreenCount(string file)
        {
            var image = Read();
            int count = 0;
            foreach (var c in image.GetPixels32())
                if (c.g > 180 && c.r < 80 && c.b < 80)
                    count++;
            var root = Path.GetFullPath(Path.Combine(Application.dataPath, "_Sandbox/Omata_N/Verification~/Images"));
            Directory.CreateDirectory(root);
            File.WriteAllBytes(Path.Combine(root, file + ".png"), image.EncodeToPNG());
            Object.Destroy(image);
            return count;
        }

        void CreateShadowWall(Vector3 position, Vector3 size)
        {
            _wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _wall.name = "Blocker";
            _wall.transform.position = position;
            _wall.transform.localScale = size;
            _wall.GetComponent<Renderer>().sharedMaterial = _mat;
            Object.Destroy(_wall.GetComponent<Collider>());
            _controller.Settings.Shadow.IsEnabled = true;
        }

        float GetGreenAt(Vector3 point)
        {
            var image = Read();
            var screen = _camera.WorldToScreenPoint(point);
            float green = image.GetPixel((int)screen.x, (int)screen.y).g;
            Object.Destroy(image);
            return green;
        }

        [UnityTest]
        public IEnumerator EachEffectChoosesWallVisibilityIndependently()
        {
            CreateShadowWall(new Vector3(0, 0, 2.5f), new Vector3(20, 20, .2f));
            _controller.Settings.Shadow.IsEnabled = false;
            // 壁はカメラだけを遮る。エコーの遮断設定とは独立に検証する。
            var red = new Material(_mat);
            red.SetColor("_BaseColor", Color.red);
            _target.GetComponent<Renderer>().sharedMaterial = red;
            try
            {
                var rule = _controller.Settings.TagColorRules[0];
                rule.FillEnabled = false;
                rule.OutlineEnabled = true;
                rule.OutlineColor = Color.green;
                rule.OriginalEnabled = true;
                rule.OutlineOcclusion = EchoEffectOcclusionMode.ThroughWalls;
                rule.OriginalOcclusion = EchoEffectOcclusionMode.VisibleOnly;
                _controller.Settings.OcclusionMode = EchoOcclusionMode.VisibleOnly;
                _controller.SetScanEnabled(true);
                yield return null;
                yield return null;
                yield return null;
                var image = Read();
                Assert.That(image.GetPixel(128, 128).r, Is.LessThan(.7f), "元マテリアルは壁で隠す");
                int green = 0;
                foreach (var c in image.GetPixels32()) if (c.g > 180 && c.r < 80) green++;
                Object.Destroy(image);
                Assert.That(green, Is.GreaterThan(100), "輪郭のみ壁越しに表示");

                rule.OutlineOcclusion = EchoEffectOcclusionMode.VisibleOnly;
                rule.OriginalOcclusion = EchoEffectOcclusionMode.ThroughWalls;
                yield return null;
                yield return null;
                image = Read();
                Assert.That(image.GetPixel(128, 128).r, Is.GreaterThan(.9f), "元マテリアルのみ壁越しに表示");
                green = 0;
                foreach (var c in image.GetPixels32()) if (c.g > 180 && c.r < 80) green++;
                Object.Destroy(image);
                Assert.That(green, Is.Zero, "輪郭は壁で隠す");

                rule.OriginalOcclusion = EchoEffectOcclusionMode.VisibleOnly;
                rule.FillEnabled = true;
                rule.FillOcclusion = EchoEffectOcclusionMode.ThroughWalls;
                yield return null;
                yield return null;
                image = Read();
                Assert.That(image.GetPixel(128, 128).g, Is.GreaterThan(.9f), "塗りだけ壁越しに表示");
                Object.Destroy(image);
                _controller.Settings.OcclusionMode = EchoOcclusionMode.ThroughWalls;
                rule.FillOcclusion = EchoEffectOcclusionMode.VisibleOnly;
                yield return null;
                yield return null;
                image = Read();
                Assert.That(image.GetPixel(128, 128).g, Is.LessThan(.7f), "共通設定が壁越しでも機能別設定を優先");
                Object.Destroy(image);
            }
            finally
            {
                _controller.enabled = false;
                Object.Destroy(red);
            }
        }

        [UnityTest]
        public IEnumerator FullyBlockedObjectNeverActivatesWholeOutline()
        {
            var rule = _controller.Settings.TagColorRules[0];
            rule.FillEnabled = false;
            rule.OutlineEnabled = true;
            rule.OutlineColor = Color.green;
            rule.OutlineHoldDuration = 0;
            _target.transform.position = new Vector3(1.5f, 1.5f, 8);
            _target.transform.localScale = Vector3.one * 3;
            _controller.Settings.Radius = 15;
            CreateShadowWall(new Vector3(0, 0, 2.5f), new Vector3(30, 30, .2f));
            _controller.Settings.Shadow.Resolution = 128;
            _controller.SetScanEnabled(true);
            foreach (float angle in new[] { 0f, .1f, 1f, 15f, 45f, 89f })
            {
                _target.transform.rotation = Quaternion.Euler(angle, angle, angle);
                yield return null;
                yield return null;
                yield return null;
                var image = Read();
                int green = 0;
                foreach (var pixel in image.GetPixels32()) if (pixel.g > 180 && pixel.r < 80) green++;
                Object.Destroy(image);
                Assert.That(green, Is.Zero, "完全遮蔽した物体の輪郭を起動しない。角度=" + angle);
            }
            _target.transform.rotation = Quaternion.identity;
            _controller.Settings.Shadow.Rules.Add(new EchoBlockingRule { TagName = "Untagged", IsBlocking = false });
            yield return null;
            yield return null;
            yield return null;
            var visible = Read();
            int revealed = 0;
            foreach (var pixel in visible.GetPixels32()) if (pixel.g > 180 && pixel.r < 80) revealed++;
            Object.Destroy(visible);
            Assert.That(revealed, Is.GreaterThan(100), "遮断しないタグなら実際の接触で輪郭を表示する");
        }

        [UnityTest]
        public IEnumerator OffscreenContactIsRememberedWhenCameraReturns()
        {
            var rule = _controller.Settings.TagColorRules[0];
            rule.FillEnabled = false;
            rule.OriginalEnabled = true;
            rule.OriginalHoldDuration = 1;
            _mat.SetColor("_BaseColor", Color.red);
            _camera.transform.rotation = Quaternion.Euler(0, 180, 0);
            _controller.SetScanEnabled(true);
            yield return null;
            yield return null;
            yield return null;
            _controller.SetScanEnabled(false);
            _camera.transform.rotation = Quaternion.identity;
            yield return null;
            yield return null;
            var image = Read();
            Assert.That(image.GetPixel(128, 128).r, Is.GreaterThan(.9f), "画面外で当たった物体も残存時間内は表示");
            Object.Destroy(image);
        }

        [UnityTest]
        public IEnumerator RecontactExtendsOriginalLifetime()
        {
            var rule = _controller.Settings.TagColorRules[0];
            rule.FillEnabled = false;
            rule.OriginalEnabled = true;
            rule.OriginalHoldDuration = .6f;
            _mat.SetColor("_BaseColor", Color.red);
            _controller.SetScanEnabled(true);
            yield return null;
            yield return null;
            yield return null;
            _controller.SetScanEnabled(false);
            yield return new WaitForSeconds(.35f);
            _controller.SetScanEnabled(true);
            yield return null;
            yield return null;
            yield return null;
            _controller.SetScanEnabled(false);
            yield return new WaitForSeconds(.35f);
            var image = Read();
            Assert.That(image.GetPixel(128, 128).r, Is.GreaterThan(.9f), "初回の残存期限を過ぎても再接触分だけ延長");
            Object.Destroy(image);
            yield return new WaitForSeconds(.4f);
            image = Read();
            Assert.That(image.GetPixel(128, 128).r, Is.LessThan(.1f));
            Object.Destroy(image);
        }

        [UnityTest]
        public IEnumerator OriginalZeroHoldAndTagChangeRestoreRenderer()
        {
            var rule = _controller.Settings.TagColorRules[0];
            rule.FillEnabled = false;
            rule.OriginalEnabled = true;
            rule.OriginalHoldDuration = 0;
            _mat.SetColor("_BaseColor", Color.red);
            _controller.SetScanEnabled(true);
            yield return null;
            yield return null;
            yield return null;
            var image = Read();
            Assert.That(image.GetPixel(128, 128).r, Is.GreaterThan(.9f));
            Object.Destroy(image);
            _controller.SetScanEnabled(false);
            yield return null;
            yield return null;
            image = Read();
            Assert.That(image.GetPixel(128, 128).r, Is.LessThan(.1f));
            Object.Destroy(image);
            _target.tag = "Respawn";
            yield return null;
            yield return null;
            Assert.True(_target.GetComponent<Renderer>().enabled);
            image = Read();
            Assert.That(image.GetPixel(128, 128).r, Is.GreaterThan(.9f));
            Object.Destroy(image);
        }

        [UnityTest]
        public IEnumerator OriginalLitMaterialKeepsItsAppearance()
        {
            _mat.shader = Shader.Find("Universal Render Pipeline/Lit");
            _mat.SetColor("_BaseColor", Color.red);
            _wall = new GameObject("Test directional light", typeof(Light));
            var light = _wall.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1;
            yield return null;
            yield return null;
            var image = Read();
            var expected = image.GetPixel(128, 128);
            Object.Destroy(image);
            Assert.That(expected.r, Is.GreaterThan(.2f));
            var rule = _controller.Settings.TagColorRules[0];
            rule.FillEnabled = false;
            rule.OriginalEnabled = true;
            _controller.SetScanEnabled(true);
            yield return null;
            yield return null;
            yield return null;
            image = Read();
            var actual = image.GetPixel(128, 128);
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(.06f));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(.06f));
            Object.Destroy(image);
        }

        [UnityTest]
        public IEnumerator ContactWithOneChildRevealsWholeEchoTarget()
        {
            _wall = new GameObject("Grouped target", typeof(EchoTarget));
            _wall.tag = "Player";
            _target.transform.SetParent(_wall.transform, true);
            _target.transform.position = new Vector3(-1.5f, 0, 5);
            _target.transform.localScale = Vector3.one;
            var child = GameObject.CreatePrimitive(PrimitiveType.Cube);
            child.transform.SetParent(_wall.transform, false);
            child.transform.position = new Vector3(1.5f, 0, 5);
            child.GetComponent<Renderer>().sharedMaterial = _mat;
            _mat.SetColor("_BaseColor", Color.red);
            var origin = new GameObject("Group contact origin").transform;
            origin.SetParent(_cameraObject.transform, false);
            origin.position = new Vector3(-1.5f, 0, 4);
            _controller.OriginTransform = origin;
            _controller.Settings.Radius = .75f;
            var rule = _controller.Settings.TagColorRules[0];
            rule.FillEnabled = false;
            rule.OriginalEnabled = true;
            rule.OutlineEnabled = true;
            rule.OutlineColor = Color.green;
            _controller.RefreshTargets();
            _controller.SetScanEnabled(true);
            yield return null;
            yield return null;
            yield return null;
            var image = Read();
            var point = _camera.WorldToScreenPoint(child.transform.position);
            Assert.That(image.GetPixel((int)point.x, (int)point.y).r, Is.GreaterThan(.9f), "接触していない子も同じ物体として表示");
            Object.Destroy(image);
        }

        [UnityTest]
        public IEnumerator WholeOutlineDoesNotFillCenterAndReachesBeyondWave()
        {
            var rule = _controller.Settings.TagColorRules[0];
            rule.FillEnabled = false;
            rule.OutlineEnabled = true;
            rule.OutlineColor = Color.green;
            rule.OutlineWidth = 5;
            _controller.Settings.Radius = 5; // 中央だけに接触。端は半径外。
            _controller.SetScanEnabled(true);
            yield return null;
            yield return null;
            yield return null;
            var image = Read();
            Assert.That(image.GetPixel(128, 128).g, Is.LessThan(.7f), "中心を塗らない");
            int green = 0;
            foreach (var pixel in image.GetPixels32()) if (pixel.g > 180 && pixel.r < 80) green++;
            Assert.That(green, Is.GreaterThan(100), "波が当たれば半径外も含めて外周を描く");
            Object.Destroy(image);
            _controller.Settings.Radius = 4;
            yield return null;
            yield return null;
            image = Read();
            green = 0;
            foreach (var pixel in image.GetPixels32()) if (pixel.g > 180 && pixel.r < 80) green++;
            Assert.That(green, Is.Zero, "未接触なら輪郭を出さない");
            Object.Destroy(image);
        }

        [UnityTest]
        public IEnumerator OriginalMaterialCoexistsAndExpiresAfterLastContact()
        {
            var rule = _controller.Settings.TagColorRules[0];
            rule.FillEnabled = false;
            rule.OutlineEnabled = true;
            rule.OutlineColor = Color.green;
            rule.OriginalEnabled = true;
            rule.OriginalHoldDuration = .3f;
            _mat.SetColor("_BaseColor", Color.red);
            yield return null;
            yield return null;
            yield return null;
            var image = Read();
            Assert.That(image.GetPixel(128, 128).r, Is.LessThan(.1f), "通常時は透明");
            Object.Destroy(image);
            _controller.SetScanEnabled(true);
            yield return null;
            yield return null;
            yield return null;
            image = Read();
            Assert.That(image.GetPixel(128, 128).r, Is.GreaterThan(.9f), "元のマテリアルの赤を表示");
            int green = 0;
            foreach (var pixel in image.GetPixels32()) if (pixel.g > 180 && pixel.r < 80) green++;
            Assert.That(green, Is.GreaterThan(100), "輪郭と同時表示");
            Object.Destroy(image);
            rule.FillEnabled = true;
            yield return null;
            yield return null;
            image = Read();
            Assert.That(image.GetPixel(128, 128).g, Is.GreaterThan(.9f), "従来の色塗りも併用");
            Object.Destroy(image);
            rule.FillEnabled = false;
            _controller.SetScanEnabled(false);
            yield return null;
            yield return null;
            image = Read();
            Assert.That(image.GetPixel(128, 128).r, Is.GreaterThan(.9f), "接触が終わっても残存");
            Object.Destroy(image);
            yield return new WaitForSeconds(.4f);
            image = Read();
            Assert.That(image.GetPixel(128, 128).r, Is.LessThan(.1f), "残存時間後は非表示");
            Object.Destroy(image);
            _controller.enabled = false;
            Assert.That(_target.GetComponent<Renderer>().sharedMaterial, Is.SameAs(_mat));
            Assert.True(_target.GetComponent<Renderer>().enabled, "無効化時は元の描画状態へ復元");
        }

        [UnityTest]
        public IEnumerator SourceShadowBlocksAndTagPassThroughRevealsWithoutDistanceLoss()
        {
            CreateShadowWall(new Vector3(0, 0, 2.5f), new Vector3(20, 20, .2f));
            _controller.SetScanEnabled(true);
            yield return null;
            yield return null;
            yield return null;
            Assert.True(_controller.ShadowMaps.IsReady);
            var readback = UnityEngine.Rendering.AsyncGPUReadback.Request(_controller.ShadowMaps.Texture);
            while (!readback.done)
                yield return null;
            Assert.False(readback.hasError);
            int resolution = _controller.Settings.Shadow.Resolution;
            Assert.That(readback.GetData<float>(4)[resolution / 2 * resolution + resolution / 2], Is.EqualTo(2.4f).Within(.02f), "正面マップに壁までの距離を保存する");
            Assert.That(GetGreenCount("source-shadow-blocked"), Is.Zero);
            _controller.Settings.Shadow.Rules.Add(new EchoBlockingRule { TagName = "Untagged", IsBlocking = false });
            yield return null;
            yield return null;
            yield return null;
            Assert.That(GetGreenCount("source-shadow-pass"), Is.GreaterThan(500));
            Assert.That(_controller.CurrentOuterRadius, Is.EqualTo(10));
            // 通過した壁より先に遮断物があれば、そこで影になる。
            var second = GameObject.CreatePrimitive(PrimitiveType.Cube);
            second.name = "SecondBlocker";
            second.tag = "Respawn";
            second.transform.position = new Vector3(0, 0, 3.5f);
            second.transform.localScale = new Vector3(20, 20, .2f);
            second.transform.SetParent(_wall.transform, true);
            second.GetComponent<Renderer>().sharedMaterial = _mat;
            second.AddComponent<EchoOccluder>(); // 動的な遮蔽物を登録する。
            yield return null;
            yield return null;
            yield return null;
            Assert.That(GetGreenCount("source-shadow-second-blocker"), Is.Zero);
            _controller.SetScanEnabled(false);
            yield return null;
            yield return null;
            Assert.False(_controller.ShadowMaps.IsReady);
            Assert.That(GetGreenCount("source-shadow-off"), Is.Zero);
        }

        [UnityTest]
        public IEnumerator ShadowUsesEchoOriginInsteadOfViewingCamera()
        {
            CreateShadowWall(new Vector3(0, 0, 2.5f), new Vector3(.8f, 3, .2f));
            _target.transform.localScale = new Vector3(.5f, .5f, .2f);
            var origin = new GameObject("Origin").transform;
            origin.SetParent(_cameraObject.transform, false);
            origin.position = Vector3.right * 2;
            _controller.OriginTransform = origin;
            _controller.SetScanEnabled(true);
            yield return null;
            yield return null;
            yield return null;
            Assert.That(GetGreenCount("source-shadow-side-origin"), Is.GreaterThan(20), "カメラからは壁裏でも、エコー地点から届くなら強調する");
            origin.position = Vector3.zero;
            yield return null;
            yield return null;
            yield return null;
            Assert.That(GetGreenCount("source-shadow-front-origin"), Is.Zero);
        }

        [UnityTest]
        public IEnumerator MovingBlockerHidesOnlyShadowedPixelsOnOneRenderer()
        {
            CreateShadowWall(new Vector3(.75f, 0, 2.5f), new Vector3(1.5f, 5, .2f));
            _controller.SetScanEnabled(true);
            yield return null;
            yield return null;
            yield return null;
            Assert.That(GetGreenAt(new Vector3(-1, 0, 4.9f)), Is.GreaterThan(.9f));
            Assert.That(GetGreenAt(new Vector3(1, 0, 4.9f)), Is.LessThan(.7f));
            _wall.transform.position = new Vector3(-.75f, 0, 2.5f);
            yield return null;
            yield return null;
            yield return null;
            Assert.That(GetGreenAt(new Vector3(-1, 0, 4.9f)), Is.LessThan(.7f));
            Assert.That(GetGreenAt(new Vector3(1, 0, 4.9f)), Is.GreaterThan(.9f));
            GetGreenCount("source-shadow-partial");
            _wall.transform.position = new Vector3(0, .75f, 2.5f);
            _wall.transform.localScale = new Vector3(5, 1.5f, .2f);
            yield return null;
            yield return null;
            yield return null;
            Assert.That(GetGreenAt(new Vector3(0, -1, 4.9f)), Is.GreaterThan(.9f));
            Assert.That(GetGreenAt(new Vector3(0, 1, 4.9f)), Is.LessThan(.7f), "距離マップの上下を反転させない");
        }

        [UnityTest]
        public IEnumerator ReadOnlyMeshAndLayerAndRootExclusionAreSupported()
        {
            CreateShadowWall(new Vector3(0, 0, 2.5f), new Vector3(20, 20, .2f));
            var mesh = Object.Instantiate(_wall.GetComponent<MeshFilter>().sharedMesh);
            _wall.GetComponent<MeshFilter>().sharedMesh = mesh;
            mesh.UploadMeshData(true);
            _controller.SetScanEnabled(true);
            yield return null;
            yield return null;
            yield return null;
            Assert.That(GetGreenAt(new Vector3(0, 0, 4.9f)), Is.LessThan(.7f), "Read/Write無効でも遮断する");
            _wall.layer = 8;
            _controller.Settings.Shadow.Layers = ~(1 << 8);
            yield return null;
            yield return null;
            yield return null;
            Assert.That(GetGreenAt(new Vector3(0, 0, 4.9f)), Is.GreaterThan(.9f));
            _controller.Settings.Shadow.Layers = ~0;
            _controller.ExcludedRoot = _wall.transform;
            yield return null;
            yield return null;
            yield return null;
            Assert.That(GetGreenAt(new Vector3(0, 0, 4.9f)), Is.GreaterThan(.9f));
            Object.Destroy(mesh);
        }

        [UnityTest]
        public IEnumerator SkinnedBlockerMovementUpdatesShadow()
        {
            CreateShadowWall(new Vector3(0, 0, 2.5f), new Vector3(3, 3, .2f));
            var mesh = Object.Instantiate(_wall.GetComponent<MeshFilter>().sharedMesh);
            Object.Destroy(_wall.GetComponent<MeshRenderer>());
            Object.Destroy(_wall.GetComponent<MeshFilter>());
            yield return null;
            var bone = new GameObject("Bone").transform;
            bone.SetParent(_wall.transform, false);
            mesh.bindposes = new[]
            {
                Matrix4x4.identity
            };
            var weights = new BoneWeight[mesh.vertexCount];
            for (int i = 0; i < weights.Length; i++)
                weights[i] = new BoneWeight
                {
                    boneIndex0 = 0,
                    weight0 = 1
                };
            mesh.boneWeights = weights;
            var skin = _wall.AddComponent<SkinnedMeshRenderer>();
            skin.sharedMesh = mesh;
            skin.sharedMaterial = _mat;
            skin.bones = new[]
            {
                bone
            };
            skin.rootBone = bone;
            skin.updateWhenOffscreen = true;
            skin.localBounds = new Bounds(Vector3.zero, Vector3.one * 30);
            _controller.SetScanEnabled(true);
            yield return null;
            yield return null;
            yield return null;
            Assert.That(GetGreenAt(new Vector3(0, 0, 4.9f)), Is.LessThan(.7f));
            bone.localPosition = Vector3.right * 5;
            yield return null;
            yield return null;
            yield return null;
            Assert.That(GetGreenAt(new Vector3(0, 0, 4.9f)), Is.GreaterThan(.9f));
            Object.Destroy(mesh);
        }

        [UnityTest]
        public IEnumerator AllSixSourceDirectionsUseTheirOwnShadowFace()
        {
            CreateShadowWall(Vector3.zero, Vector3.one);
            _target.transform.localScale = new Vector3(.5f, .5f, .2f);
            _controller.Settings.Shadow.Rules.Add(new EchoBlockingRule { TagName = "Player", IsBlocking = false });
            var rule = new EchoBlockingRule
            {
                TagName = "Untagged",
                IsBlocking = true
            };
            _controller.Settings.Shadow.Rules.Add(rule);
            var origin = new GameObject("Origin").transform;
            origin.SetParent(_cameraObject.transform, false);
            _controller.OriginTransform = origin;
            var point = new Vector3(0, 0, 4.9f);
            _controller.SetScanEnabled(true);
            foreach (var direction in new[]
            {
                Vector3.right,
                Vector3.left,
                Vector3.up,
                Vector3.down,
                Vector3.forward,
                Vector3.back
            }

            )
            {
                origin.position = point - direction * 3;
                _wall.transform.position = point - direction * 1.5f;
                rule.IsBlocking = true;
                yield return null;
                yield return null;
                yield return null;
                Assert.That(GetGreenAt(point), Is.LessThan(.7f), "遮断方向：" + direction);
                rule.IsBlocking = false;
                yield return null;
                yield return null;
                yield return null;
                Assert.That(GetGreenAt(point), Is.GreaterThan(.9f), "透過方向：" + direction);
            }
        }

        [UnityTest]
        public IEnumerator GrazingFloorDoesNotShadowItselfInStripes()
        {
            _target.transform.position = new Vector3(0, -1, 10);
            _target.transform.localScale = new Vector3(30, .1f, 30);
            _camera.transform.rotation = Quaternion.Euler(10, 0, 0);
            _controller.Settings.Radius = 25;
            _controller.Settings.Shadow.IsEnabled = true;
            _controller.SetScanEnabled(true);
            yield return null;
            yield return null;
            yield return null;
            for (int z = 4; z <= 18; z++)
                for (int x = -2; x <= 2; x++)
                    Assert.That(GetGreenAt(new Vector3(x * .6f, -.95f, z)), Is.GreaterThan(.9f), "斜め方向の床を自分自身の影にしない：" + x + "," + z);
            GetGreenCount("source-shadow-grazing-floor");
        }

        [UnityTest]
        public IEnumerator OlderSonarOriginCanIlluminateCurrentOriginsShadow()
        {
            CreateShadowWall(new Vector3(0, 0, 2.5f), new Vector3(.8f, 3, .2f));
            _target.transform.localScale = new Vector3(.5f, .5f, .2f);
            var origin = new GameObject("Origin").transform;
            origin.SetParent(_cameraObject.transform, false);
            origin.position = Vector3.right * 2;
            _controller.OriginTransform = origin;
            _controller.Settings.ScanMode = EchoScanMode.Sonar;
            _controller.Settings.PulseInterval = .15f;
            _controller.Settings.PulseHoldDuration = .8f;
            _controller.SetScanEnabled(true);
            yield return null;
            yield return null;
            yield return null;
            origin.position = Vector3.zero;
            yield return new WaitForSeconds(.2f);
            yield return null;
            Assert.That(_controller.ShadowMaps.OriginCount, Is.EqualTo(2));
            Assert.That(GetGreenAt(new Vector3(0, 0, 4.9f)), Is.GreaterThan(.9f));
            yield return new WaitForSeconds(.7f);
            yield return null;
            Assert.That(GetGreenAt(new Vector3(0, 0, 4.9f)), Is.LessThan(.7f));
        }

        void AddOverlappingHighlight()
        {
            _wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _wall.name = "FrontHighlight";
            _wall.transform.position = new Vector3(0, 0, 3);
            _wall.transform.localScale = new Vector3(1, 1, .2f);
            _wall.GetComponent<Renderer>().sharedMaterial = _mat;
            _controller.Settings.TagColorRules.Add(new EchoTagColorRule { TagName = "Untagged", Color = Color.red });
            _controller.RefreshTargets();
        }

        [UnityTest]
        public IEnumerator OpaqueHighlightsBothContributeAndDepthOrderDoesNotChangeMixture()
        {
            AddOverlappingHighlight();
            _controller.SetScanEnabled(true);
            yield return null;
            yield return null;
            yield return null;
            var mixed = Read();
            Color original = mixed.GetPixel(128, 128);
            Assert.That(original.r, Is.GreaterThan(.5f), "手前の赤も残る");
            Assert.That(original.g, Is.GreaterThan(.5f), "アルファ1の対象の後ろの緑も残る");
            Assert.That(original.b, Is.LessThan(.1f));
            var folder = Path.GetFullPath(Path.Combine(Application.dataPath, "_Sandbox/Omata_N/Verification~/Images"));
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, "overlapping-highlights.png"), mixed.EncodeToPNG());
            Object.Destroy(mixed);
            _target.transform.position = new Vector3(0, 0, 3);
            _wall.transform.position = new Vector3(0, 0, 5);
            yield return null;
            yield return null;
            yield return null;
            var reversed = Read();
            Color next = reversed.GetPixel(128, 128);
            Object.Destroy(reversed);
            Assert.That(next.r, Is.EqualTo(original.r).Within(.01f));
            Assert.That(next.g, Is.EqualTo(original.g).Within(.01f));
            _wall.GetComponent<Renderer>().enabled = false;
            yield return null;
            yield return null;
            yield return null;
            var single = Read();
            Color alone = single.GetPixel(128, 128);
            Object.Destroy(single);
            Assert.That(alone.r, Is.LessThan(.1f));
            Assert.That(alone.g, Is.GreaterThan(.9f), "前フレームの赤を蓄積し続けない");
        }

        [UnityTest]
        public IEnumerator VisibleOnlyStillRespectsSceneDepthBetweenHighlights()
        {
            AddOverlappingHighlight();
            _controller.Settings.OcclusionMode = EchoOcclusionMode.VisibleOnly;
            _controller.SetScanEnabled(true);
            yield return null;
            yield return null;
            yield return null;
            var image = Read();
            Color center = image.GetPixel(128, 128);
            Object.Destroy(image);
            Assert.That(center.r, Is.GreaterThan(.9f));
            Assert.That(center.g, Is.LessThan(.1f), "深度で隠す設定は引き続き尊重する");
        }

        [UnityTest]
        public IEnumerator PassedSurfaceDoesNotLeaveBrightFrontWhileWaveContinues()
        {
            // 壁に到達した後も親波は展開中。終端へのクランプによる先端の残留を再現する。
            _cameraObject.AddComponent<EchoRangeDisplay>().IsBoundaryVisible = false;
            _controller.Settings.TagColorRules.Clear();
            _controller.Settings.AngleDegrees = 10;
            _controller.Settings.Duration = 2;
            _controller.Settings.Radius = 10;
            _target.transform.position = new Vector3(0, 0, 2.5f);
            _target.AddComponent<BoxCollider>();
            Physics.SyncTransforms();
            _controller.SetScanEnabled(true);
            yield return new WaitForSeconds(1);
            yield return null;
            Assert.That(_controller.CurrentState, Is.EqualTo(EchoState.Revealing));
            var image = Read();
            var color = image.GetPixel(128, 128);
            Assert.That(color.g, Is.LessThan(.7f), "衝突点を動く先端として光らせ続けない");
            Assert.That(color.b, Is.GreaterThan(color.r + .02f), "到達済みの薄い範囲色は残す");
            Object.Destroy(image);
        }

        [UnityTest]
        public IEnumerator CompletedWaveRetainsFillWithoutBrightRim()
        {
            _cameraObject.AddComponent<EchoRangeDisplay>().IsBoundaryVisible = false;
            _controller.Settings.TagColorRules.Clear();
            _controller.Settings.Radius = 4.95f;
            _controller.Settings.AngleDegrees = 10;
            _controller.Settings.Duration = 0;
            _controller.SetScanEnabled(true);
            yield return null;
            yield return null;
            yield return null;
            var image = Read();
            var color = image.GetPixel(128, 128);
            Assert.That(color.g, Is.LessThan(.7f), "最大距離の境界にも停止した先端を残さない");
            Assert.That(color.b, Is.GreaterThan(color.r + .02f));
            Object.Destroy(image);
        }

        [UnityTest]
        public IEnumerator ThroughWallsAndVisibleOnlyUseDifferentOcclusion()
        {
            _wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _wall.name = "Occluder";
            _wall.transform.position = new Vector3(0, 0, 2);
            _wall.transform.localScale = new Vector3(5, 5, .2f);
            _wall.GetComponent<Renderer>().sharedMaterial = _mat;
            _controller.SetScanEnabled(true);
            yield return null;
            yield return null;
            yield return null;
            Assert.That(GetGreenCount("through-walls"), Is.GreaterThan(500), "壁裏の対象をGPUで描画する");
            _controller.Settings.OcclusionMode = EchoOcclusionMode.VisibleOnly;
            yield return null;
            yield return null;
            yield return null;
            Assert.That(GetGreenCount("visible-only"), Is.EqualTo(0), "シーン深度で隠れる");
        }

        [UnityTest]
        public IEnumerator LargeSurfaceIsClippedPerPixelAndOffClearsIt()
        {
            _controller.Settings.AngleDegrees = 30;
            _controller.SetScanEnabled(true);
            yield return null;
            yield return null;
            yield return null;
            int clipped = GetGreenCount("cone-30-degrees");
            Assert.That(clipped, Is.GreaterThan(100));
            _controller.Settings.AngleDegrees = 360;
            yield return null;
            yield return null;
            yield return null;
            int full = GetGreenCount("sphere-360-degrees");
            Assert.That(full, Is.GreaterThan(clipped * 2), "同じ大きな面の一部だけを表示する");
            _controller.SetScanEnabled(false);
            yield return null;
            yield return null;
            yield return null;
            Assert.That(GetGreenCount("off"), Is.EqualTo(0));
        }

        [UnityTest]
        public IEnumerator SkinnedBoneMovementChangesTheHighlightedSurface()
        {
            var mesh = Object.Instantiate(_target.GetComponent<MeshFilter>().sharedMesh);
            Object.Destroy(_target.GetComponent<MeshRenderer>());
            Object.Destroy(_target.GetComponent<MeshFilter>());
            yield return null;
            _target.transform.localScale = Vector3.one;
            var bone = new GameObject("Bone").transform;
            bone.SetParent(_target.transform, false);
            var weights = new BoneWeight[mesh.vertexCount];
            for (int i = 0; i < weights.Length; i++)
                weights[i] = new BoneWeight
                {
                    boneIndex0 = 0,
                    weight0 = 1
                };
            mesh.boneWeights = weights;
            mesh.bindposes = new[]
            {
                bone.worldToLocalMatrix * _target.transform.localToWorldMatrix
            };
            var skin = _target.AddComponent<SkinnedMeshRenderer>();
            skin.sharedMesh = mesh;
            skin.sharedMaterial = _mat;
            skin.bones = new[]
            {
                bone
            };
            skin.rootBone = bone;
            skin.updateWhenOffscreen = true;
            skin.localBounds = new Bounds(Vector3.zero, Vector3.one * 10);
            _controller.RefreshTargets();
            _controller.SetScanEnabled(true);
            yield return null;
            yield return null;
            yield return null;
            Assert.That(GetGreenCount("skinned-before"), Is.GreaterThan(100));
            var before = Read();
            bone.localPosition = Vector3.right * 2;
            yield return null;
            yield return null;
            yield return null;
            Assert.That(GetGreenCount("skinned-after"), Is.GreaterThan(100));
            var after = Read();
            int changed = 0;
            var aItems = before.GetPixels32();
            var bItems = after.GetPixels32();
            for (int i = 0; i < aItems.Length; i++)
                if (aItems[i].g != bItems[i].g)
                    changed++;
            Assert.That(changed, Is.GreaterThan(100));
            Object.Destroy(before);
            Object.Destroy(after);
            Object.Destroy(mesh);
        }

        [UnityTest]
        public IEnumerator RangeShowsUntaggedSurfacesAndClearsOnOff()
        {
            var display = _cameraObject.AddComponent<EchoRangeDisplay>();
            display.IsBoundaryVisible = false;
            _controller.Settings.TagColorRules.Clear();
            _controller.Settings.AngleDegrees = 30;
            _controller.SetScanEnabled(true);
            yield return null;
            yield return null;
            yield return null;
            Assert.That(_controller.RenderItems.Count, Is.Zero, "タグ対象なしでも範囲を描く");
            var on = Read();
            int cyan = 0;
            foreach (var color in on.GetPixels32())
                if (color.g > color.r + 10 && color.b > color.r + 10)
                    cyan++;
            Assert.That(cyan, Is.GreaterThan(100));
            Object.Destroy(on);
            _controller.SetScanEnabled(false);
            yield return null;
            yield return null;
            yield return null;
            var off = Read();
            cyan = 0;
            foreach (var color in off.GetPixels32())
                if (color.g > color.r + 10 && color.b > color.r + 10)
                    cyan++;
            Assert.That(cyan, Is.Zero);
            Object.Destroy(off);
        }

        [UnityTest]
        public IEnumerator SonarKeepsOldAndNewPositionsUntilEachLifetimeEnds()
        {
            _wall = new GameObject("PulseOrigin");
            _wall.transform.position = new Vector3(-1.5f, 0, 5);
            _controller.OriginTransform = _wall.transform;
            _controller.Settings.ScanMode = EchoScanMode.Sonar;
            _controller.Settings.Radius = 1;
            _controller.Settings.Duration = 0;
            _controller.Settings.PulseInterval = .2f;
            _controller.Settings.PulseHoldDuration = .8f;
            _controller.SetScanEnabled(true);
            yield return null;
            yield return null;
            yield return null;
            _wall.transform.position = new Vector3(1.5f, 0, 5);
            yield return new WaitForSeconds(.3f);
            yield return null;
            Assert.That(_controller.Frames.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(_controller.Frames[0].Origin.x, Is.EqualTo(-1.5f));
            Color At(Texture2D image, float x)
            {
                var p = _camera.WorldToScreenPoint(new Vector3(x, 0, 4.9f));
                return image.GetPixel((int)p.x, (int)p.y);
            }

            var both = Read();
            Assert.That(At(both, -1.5f).g, Is.GreaterThan(.9f));
            Assert.That(At(both, 1.5f).g, Is.GreaterThan(.9f));
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "_Sandbox/Omata_N/Verification~/Images"));
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, "sonar-two-origins.png"), both.EncodeToPNG());
            Object.Destroy(both);
            yield return new WaitForSeconds(.6f);
            yield return null;
            var expired = Read();
            Assert.That(At(expired, -1.5f).g, Is.LessThan(.7f));
            Assert.That(At(expired, 1.5f).g, Is.GreaterThan(.9f));
            Object.Destroy(expired);
            _controller.SetScanEnabled(false);
            yield return null;
            yield return null;
            Assert.That(_controller.Frames.Count, Is.Zero);
        }

        [UnityTest]
        public IEnumerator OverlappingSonarWavesDoNotAccumulateHighlightOpacity()
        {
            _controller.Settings.ScanMode = EchoScanMode.Sonar;
            _controller.Settings.PulseInterval = .1f;
            _controller.Settings.PulseHoldDuration = 1;
            _controller.Settings.TagColorRules[0].Color = new Color(0, 1, 0, .5f);
            _controller.SetScanEnabled(true);
            yield return null;
            yield return null;
            yield return null;
            var before = Read();
            Color original = before.GetPixel(128, 128);
            Object.Destroy(before);
            yield return new WaitForSeconds(.35f);
            yield return null;
            Assert.That(_controller.Frames.Count, Is.GreaterThanOrEqualTo(3));
            var after = Read();
            Color overlapping = after.GetPixel(128, 128);
            Object.Destroy(after);
            Assert.That(overlapping.g, Is.EqualTo(original.g).Within(.01));
            Assert.That(overlapping.r, Is.EqualTo(original.r).Within(.01));
        }
    }
}
