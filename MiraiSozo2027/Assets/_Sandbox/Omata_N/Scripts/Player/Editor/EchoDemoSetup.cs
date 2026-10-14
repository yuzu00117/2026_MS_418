// デモとRenderer Featureを冪等に構成する。既存SampleSceneは上書きしない。
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.InputSystem;

namespace Echo.Echolocation.Samples.Editor
{
    public static class EchoDemoSetup
    {
        // 個人フォルダー名や配置先が変わっても、このツール自身の位置から導入先を決める。
        private static string Root
        {
            get
            {
                const string ScriptSuffix = "/Scripts/Player/Editor/EchoDemoSetup.cs";
                foreach (string guid in AssetDatabase.FindAssets("EchoDemoSetup t:MonoScript"))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (path.EndsWith(ScriptSuffix, System.StringComparison.Ordinal))
                        return path.Substring(0, path.Length - ScriptSuffix.Length);
                }
                throw new System.InvalidOperationException("EchoDemoSetup.csの配置先を特定できません。");
            }
        }

        /// <summary>移植で引き継がれないプロジェクト設定を追加する。シーンやPrefabは再生成しない。</summary>
        [MenuItem("Tools/エコーロケーション/移植先の初期設定")]
        public static void InitializeImportedProject()
        {
            if (Application.isPlaying)
                throw new System.InvalidOperationException("再生を停止してから初期設定してください。");
            EnsureDemoTags();
            InstallFeatures();
            Debug.Log("エコー：描画Featureとデモ用タグを登録しました。シーンを開き直して再生してください。");
        }

        private static void EnsureDemoTags()
        {
            EnsureTag("EchoEnemy");
            EnsureTag("EchoItem");
            EnsureTag("EchoStructure");
        }
        /// <summary>デモの既存設定を維持し、ソナー方式と間隔・残存時間だけを設定する。</summary>
        [MenuItem("Tools/エコーロケーション/デモをソナーモードに設定")]
        public static void EnableSonarDemo()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            EditorSceneManager.OpenScene(Root + "/Scenes/Scene_EchoDemo.unity");
            foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                if (root.TryGetComponent<EchoController>(out var c))
                {
                    c.Settings.ScanMode = EchoScanMode.Sonar;
                    c.Settings.PulseInterval = 2;
                    c.Settings.PulseHoldDuration = 3;
                    EditorUtility.SetDirty(c);
                    PrefabUtility.SaveAsPrefabAsset(root, Root + "/Prefabs/PF_EchoPlayer.prefab");
                }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            AssetDatabase.SaveAssets();
        }

        /// <summary>メニューまたはbatchmodeから導入する。</summary>
        [MenuItem("Tools/エコーロケーション/デモと描画設定を作成")]
        public static void SetupProject()
        {
            EnsureDemoTags();
            InstallFeatures();
            EnsureDemoFolders();
            if (!File.Exists(Root + "/Scenes/Scene_EchoDemo.unity"))
                CreateScene();
            else
                EditorSceneManager.OpenScene(Root + "/Scenes/Scene_EchoDemo.unity");
            UpgradeOpenDemo();
            UnityEngine.InputSystem.InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
            EditorUtility.SetDirty(UnityEngine.InputSystem.InputSystem.settings);
            AssetDatabase.SaveAssets();
            Debug.Log("エコー：デモ準備完了。Scene_EchoDemoを開いて再生しEキーを押してください。");
        }

        // アセット生成先を固定し、再セットアップでもSandbox外へ作らない。
        private static void EnsureDemoFolders()
        {
            foreach (string suffix in new[] { "Scenes", "Prefabs", "Art/Materials" })
            {
                string current = "Assets";
                foreach (string segment in (Root.Substring("Assets/".Length) + "/" + suffix).Split('/'))
                {
                    string next = current + "/" + segment;
                    if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, segment);
                    current = next;
                }
            }
        }

        /// <summary>プロジェクト内のUniversal RendererへFeatureを追加する。</summary>
        [MenuItem("Tools/エコーロケーション/Renderer Featureを設定")]
        public static void InstallFeatures()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:UniversalRendererData", new[] { "Assets" }))
            {
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(AssetDatabase.GUIDToAssetPath(guid));
                EchoUrpRendererFeature feature = null;
                foreach (var f in data.rendererFeatures)
                    if (f is EchoUrpRendererFeature echo)
                        feature = echo;
                bool isCreated = !feature;
                if (isCreated)
                {
                    feature = ScriptableObject.CreateInstance<EchoUrpRendererFeature>();
                    feature.name = "Echo Highlight";
                }

                var serialized = new SerializedObject(feature);
                serialized.FindProperty("_highlightShader").objectReferenceValue = Shader.Find("Hidden/Echo/Highlight");
                serialized.FindProperty("_compositeShader").objectReferenceValue = Shader.Find("Hidden/Echo/Composite");
                serialized.FindProperty("_rangeShader").objectReferenceValue = Shader.Find("Hidden/Echo/Range");
                serialized.ApplyModifiedPropertiesWithoutUndo();
                feature.SetActive(true);
                if (isCreated)
                {
                    AssetDatabase.AddObjectToAsset(feature, data);
                    data.rendererFeatures.Add(feature);
                }

                EditorUtility.SetDirty(feature);
                data.SetDirty();
                EditorUtility.SetDirty(data);
            }

            AssetDatabase.SaveAssets();
        }

        /// <summary>初回導入時のパッケージ内Featureを除去し、以後はAssetsだけを編集する。</summary>
        public static void RepairPackageFeature()
        {
            var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Packages/com.unity.render-pipelines.universal/Runtime/Data/UniversalRendererData.asset");
            if (data)
            {
                for (int i = data.rendererFeatures.Count - 1; i >= 0; i--)
                    if (data.rendererFeatures[i] is EchoUrpRendererFeature)
                    {
                        var feature = data.rendererFeatures[i];
                        data.rendererFeatures.RemoveAt(i);
                        Object.DestroyImmediate(feature, true);
                        EditorUtility.SetDirty(data);
                    }

                AssetDatabase.SaveAssets();
            }
        }

        static void EnsureTag(string name)
        {
            var manager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var tags = manager.FindProperty("tags");
            for (int i = 0; i < tags.arraySize; i++)
                if (tags.GetArrayElementAtIndex(i).stringValue == name)
                    return;
            tags.InsertArrayElementAtIndex(tags.arraySize);
            tags.GetArrayElementAtIndex(tags.arraySize - 1).stringValue = name;
            manager.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>既存デモの環境・タグ設定を維持し、プレイヤーと範囲表示だけ追加する。</summary>
        [MenuItem("Tools/エコーロケーション/デモにプレイヤーと範囲表示を追加")]
        public static void UpgradeDemoPlayer()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            BackupDemo();
            InstallFeatures();
            EditorSceneManager.OpenScene(Root + "/Scenes/Scene_EchoDemo.unity");
            UpgradeOpenDemo();
            AssetDatabase.SaveAssets();
        }

        static void BackupDemo()
        {
            string folder = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Artifacts/Before-player-update"));
            Directory.CreateDirectory(folder);
            foreach (string name in new[]
            {
                "Scenes/Scene_EchoDemo.unity",
                "Prefabs/PF_EchoPlayer.prefab"
            }

            )
                if (File.Exists(Root + "/" + name) && !File.Exists(Path.Combine(folder, Path.GetFileName(name))))
                    File.Copy(Root + "/" + name, Path.Combine(folder, Path.GetFileName(name)));
        }

        static void UpgradeOpenDemo()
        {
            EchoController c = null;
            foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                if (root.TryGetComponent<EchoController>(out var candidate))
                {
                    c = candidate;
                    break;
                }

            if (!c)
                throw new System.InvalidOperationException("デモのEchoControllerが見つかりません。");
            if (!c.GetComponent<EchoRangeDisplay>())
                c.gameObject.AddComponent<EchoRangeDisplay>();
            if (!c.GetComponent<EchoFrameRateLimiter>())
                c.gameObject.AddComponent<EchoFrameRateLimiter>();
            if (!c.GetComponent<EchoDemoPlayer>())
            {
                var root = c.gameObject;
                var oldCamera = c.TargetCamera;
                Vector3 previousPosition = root.transform.position;
                float initialYaw = root.transform.eulerAngles.y;
                root.name = "Player";
                root.tag = "Player";
                root.transform.SetPositionAndRotation(new Vector3(previousPosition.x, Mathf.Max(.05f, previousPosition.y - 1.6f), previousPosition.z), Quaternion.Euler(0, initialYaw, 0));
                var pivot = new GameObject("View").transform;
                pivot.SetParent(root.transform, false);
                pivot.localPosition = new Vector3(0, 1.6f, 0);
                pivot.localRotation = Quaternion.Euler(8, 0, 0);
                var cameraObject = new GameObject("Camera", typeof(Camera), typeof(AudioListener));
                cameraObject.transform.SetParent(pivot, false);
                cameraObject.tag = "MainCamera";
                var camera = cameraObject.GetComponent<Camera>();
                if (oldCamera)
                    EditorUtility.CopySerialized(oldCamera, camera);
                camera.nearClipPlane = .05f;
                camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
                c.TargetCamera = camera;
                // 元のカメラを二重稼働させない。プレイヤールートにある旧コンポーネントだけを除去する。
                if (oldCamera && oldCamera.gameObject == root)
                {
                    if (root.TryGetComponent<AudioListener>(out var listener))
                        Object.DestroyImmediate(listener);
                    if (root.TryGetComponent<UniversalAdditionalCameraData>(out var additional))
                        Object.DestroyImmediate(additional);
                    Object.DestroyImmediate(oldCamera);
                }

                var origin = new GameObject("Origin").transform;
                origin.SetParent(root.transform, false);
                origin.localPosition = new Vector3(0, .95f, 0);
                c.OriginTransform = origin;
                c.DirectionTransform = pivot;
                c.ExcludedRoot = root.transform;
                var motor = root.AddComponent<CharacterController>();
                motor.height = 1.9f;
                motor.radius = .35f;
                motor.center = new Vector3(0, .95f, 0);
                motor.stepOffset = .3f;
                motor.skinWidth = .03f;
                motor.minMoveDistance = 0;
                var bodyMat = GetOrCreateMaterial("PlayerBody", new Color(.1f, .27f, .4f));
                var headMat = GetOrCreateMaterial("PlayerHelmet", new Color(.65f, .77f, .85f));
                var visorMat = GetOrCreateMaterial("PlayerVisor", new Color(.06f, .95f, .75f));
                GameObject Part(string name, PrimitiveType type, Vector3 position, Vector3 scale, Material mat)
                {
                    var part = GameObject.CreatePrimitive(type);
                    part.name = name;
                    part.transform.SetParent(root.transform, false);
                    part.transform.localPosition = position;
                    part.transform.localScale = scale;
                    part.GetComponent<Renderer>().sharedMaterial = mat;
                    Object.DestroyImmediate(part.GetComponent<Collider>());
                    return part;
                }

                var body = Part("Body", PrimitiveType.Capsule, new Vector3(0, .82f, 0), new Vector3(.65f, .8f, .65f), bodyMat);
                var head = Part("Head", PrimitiveType.Sphere, new Vector3(0, 1.6f, 0), Vector3.one * .43f, headMat);
                var visor = Part("Visor", PrimitiveType.Cube, new Vector3(0, 1.62f, .2f), new Vector3(.3f, .1f, .06f), visorMat);
                root.AddComponent<EchoDemoPlayer>().ConfigureRig(pivot, camera, new[] { body.GetComponent<Renderer>(), head.GetComponent<Renderer>(), visor.GetComponent<Renderer>() });
            }

            RenameDemoHierarchy();
            EditorUtility.SetDirty(c);
            PrefabUtility.SaveAsPrefabAsset(c.gameObject, Root + "/Prefabs/PF_EchoPlayer.prefab");
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("エコー：プレイヤー・マウス視点・Z視点切替・スキャン範囲表示を準備しました。");
        }

        // 既存シーンも名前だけ移行する。Transformやコンポーネント参照、設定値は保持する。
        static void RenameDemoHierarchy()
        {
            var names = new System.Collections.Generic.Dictionary<string, string>
            {
                {
                    "Echo Player - WASD移動・Z視点切替",
                    "Player"
                },
                {
                    "Player - Eでスキャン",
                    "Player"
                },
                {
                    "Look Pivot - マウス視点",
                    "View"
                },
                {
                    "Scan Origin - 胴体中心",
                    "Origin"
                },
                {
                    "Player Camera",
                    "Camera"
                },
                {
                    "仮プレイヤー・胴体",
                    "Body"
                },
                {
                    "仮プレイヤー・頭",
                    "Head"
                },
                {
                    "正面マーカー",
                    "Visor"
                },
                {
                    "Directional Light",
                    "Light"
                },
                {
                    "Collider無しのアイテム",
                    "Item"
                },
                {
                    "敵カプセル",
                    "Enemy"
                },
                {
                    "巨大な壁 - 表面ごとに波が到達",
                    "Wall"
                },
                {
                    "遮蔽物",
                    "Occluder"
                },
                {
                    "壁の向こうの敵",
                    "HiddenEnemy"
                },
                {
                    "親タグでまとめたアイテム",
                    "Items"
                },
                {
                    "子Renderer 0",
                    "Item0"
                },
                {
                    "子Renderer 1",
                    "Item1"
                },
                {
                    "子Renderer 2",
                    "Item2"
                }
            };
            foreach (var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())
                foreach (var t in root.GetComponentsInChildren<Transform>(true))
                    if (names.TryGetValue(t.name, out var name))
                        t.name = name;
        }

        static Material GetOrCreateMaterial(string name, Color color)
        {
            var path = Root + "/Art/Materials/M_" + name + ".mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat)
                return mat;
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", color);
            AssetDatabase.CreateAsset(mat, path);
            return mat;
        }

        static GameObject CreateShape(string name, PrimitiveType type, Vector3 position, Vector3 scale, Material mat, string tag = "Untagged")
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.position = position;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            go.tag = tag;
            return go;
        }

        static void CreateScene()
        {
            EnsureTag("EchoEnemy");
            EnsureTag("EchoItem");
            EnsureTag("EchoStructure");
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientLight = new Color(.35f, .38f, .45f);
            var cameraGO = new GameObject("Player", typeof(Camera), typeof(AudioListener));
            cameraGO.tag = "MainCamera";
            cameraGO.transform.SetPositionAndRotation(new Vector3(0, 2, -8), Quaternion.Euler(8, 0, 0));
            var camera = cameraGO.GetComponent<Camera>();
            camera.backgroundColor = new Color(.025f, .035f, .055f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.farClipPlane = 100;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = false;
            var c = cameraGO.AddComponent<EchoController>();
            c.TargetCamera = camera;
            c.Settings.Radius = 22;
            c.Settings.Duration = 3;
            c.Settings.ScanMode = EchoScanMode.Sonar;
            c.Settings.TagColorRules.Add(new EchoTagColorRule { TagName = "EchoEnemy", Color = new Color(1, .12f, .22f, .8f) });
            c.Settings.TagColorRules.Add(new EchoTagColorRule { TagName = "EchoItem", Color = new Color(.1f, 1, .65f, .8f) });
            c.Settings.TagColorRules.Add(new EchoTagColorRule { TagName = "EchoStructure", Color = new Color(.1f, .6f, 1, .65f) });
            // 色と遮断は独立。壁・床は影を作り、敵とアイテムは奥への到達を妨げない。
            c.Settings.Shadow.Rules.Add(new EchoBlockingRule { TagName = "EchoStructure", IsBlocking = true });
            c.Settings.Shadow.Rules.Add(new EchoBlockingRule { TagName = "EchoEnemy", IsBlocking = false });
            c.Settings.Shadow.Rules.Add(new EchoBlockingRule { TagName = "EchoItem", IsBlocking = false });
            var input = cameraGO.AddComponent<EchoPrototypeInput>();
            input.Controller = c;
            input.Bindings.Add(new EchoInputBinding { ControlPath = "<Mouse>/backButton" });
            input.Bindings.Add(new EchoInputBinding { ControlPath = "<Gamepad>/buttonSouth" });
            cameraGO.AddComponent<EchoDemoToggle>().Controller = c;
            var light = new GameObject("Light", typeof(Light));
            light.transform.rotation = Quaternion.Euler(50, -30, 0);
            light.GetComponent<Light>().type = LightType.Directional;
            light.GetComponent<Light>().intensity = 1.4f;
            var gray = GetOrCreateMaterial("Neutral", new Color(.22f, .25f, .3f));
            var dark = GetOrCreateMaterial("Ground", new Color(.08f, .1f, .14f));
            CreateShape("Ground", PrimitiveType.Cube, new Vector3(0, -.25f, 6), new Vector3(32, .5f, 36), dark);
            var item = CreateShape("Item", PrimitiveType.Sphere, new Vector3(-3, 1, 1), Vector3.one * 1.5f, gray, "EchoItem");
            Object.DestroyImmediate(item.GetComponent<Collider>());
            CreateShape("Enemy", PrimitiveType.Capsule, new Vector3(3, 1, 4), Vector3.one, gray, "EchoEnemy");
            CreateShape("Wall", PrimitiveType.Cube, new Vector3(-5, 2, 9), new Vector3(1, 4, 14), gray, "EchoStructure");
            CreateShape("Occluder", PrimitiveType.Cube, new Vector3(1, 1.5f, 7), new Vector3(5, 3, .6f), gray);
            CreateShape("HiddenEnemy", PrimitiveType.Capsule, new Vector3(1, 1, 10), Vector3.one, gray, "EchoEnemy");
            var parent = new GameObject("Items");
            parent.tag = "EchoItem";
            for (int i = 0; i < 3; i++)
            {
                var go = CreateShape("Item" + i, PrimitiveType.Cube, new Vector3(-1 + i, 1, 14), Vector3.one * .6f, gray);
                go.transform.SetParent(parent.transform);
            }

            parent.AddComponent<EchoTarget>();
            PrefabUtility.SaveAsPrefabAsset(cameraGO, Root + "/Prefabs/PF_EchoPlayer.prefab");
            EditorSceneManager.SaveScene(scene, Root + "/Scenes/Scene_EchoDemo.unity");
            // デモを先頭に追加し、既存のビルドシーンは残す。
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.Insert(0, new EditorBuildSettingsScene(Root + "/Scenes/Scene_EchoDemo.unity", true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
