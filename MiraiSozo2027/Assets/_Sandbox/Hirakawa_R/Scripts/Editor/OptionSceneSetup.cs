using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace MiraiSozo.Options.Editor
{
    public static class OptionSceneSetup
    {
        public const string Root = "Assets/_Sandbox/Hirakawa_R";
        public const string ScenePath = Root + "/Scenes/Scene_OptionTestScene.unity";
        public const string PrefabPath = "Assets/_Project/Prefabs/PF_InGameOptions.prefab";
        private const float MaterialSmoothness = 0.25f;
        private const int ExpectedCanvasCount = 1;

        // テストステージの配置値。再生成時も同じ見た目を保つため、用途別に集約する。
        private static class StageLayout
        {
            public static readonly Color FloorColor = new Color32(37, 57, 70, 255);
            public static readonly Color TealColor = new Color32(68, 206, 188, 255);
            public static readonly Color OrangeColor = new Color32(237, 164, 91, 255);
            public static readonly Color GridColor = new Color32(70, 101, 112, 255);
            public static readonly Color PaleColor = new Color32(195, 216, 222, 255);
            public static readonly Color AmbientColor = new Color(0.5f, 0.57f, 0.64f);
            public static readonly Vector3 FloorPosition = new Vector3(0, -0.25f, 3);
            public static readonly Vector3 FloorScale = new Vector3(42, 0.5f, 42);
            public const int GridMinimum = -10;
            public const int GridMaximum = 10;
            public const int GridStride = 2;
            public const float GridHeight = 0.006f;
            public const float GridCenterZ = 3f;
            public static readonly Vector3 GridXScale = new Vector3(0.018f, 0.012f, 40);
            public static readonly Vector3 GridZScale = new Vector3(40, 0.012f, 0.018f);
            public static readonly Vector3 PlayerPosition = new Vector3(0, 1.08f, 0);
            public const float PlayerHeight = 2f;
            public const float PlayerRadius = 0.5f;
            public const int MarkerCount = 5;
            public const float MarkerCenterIndex = (MarkerCount - 1) * 0.5f;
            public const float MarkerSpacing = 4f;
            public const int MarkerColorCycle = 2;
            public const float MarkerHeight = 1.5f;
            public const float MarkerZ = 10f;
            public static readonly Vector3 MarkerScale = new Vector3(1, 3, 1);
            public const float MarkerBaseHeight = 0.14f;
            public static readonly Vector3 MarkerBaseScale = new Vector3(2, MarkerBaseHeight, 2);
            public static readonly Vector3 NearBlockPosition = new Vector3(-3.5f, 0.7f, 2.5f);
            public static readonly Vector3 NearBlockScale = new Vector3(1.4f, 1.4f, 1.4f);
            public static readonly Vector3 FarBlockPosition = new Vector3(4, 1, 5);
            public static readonly Vector3 FarBlockScale = new Vector3(2, 2, 2);
        }

        private static class CameraLayout
        {
            public static readonly Vector3 Position = new Vector3(0, 3, -7);
            public static readonly Quaternion Rotation = Quaternion.Euler(15, 0, 0);
            public static readonly Color BackgroundColor = new Color32(25, 42, 59, 255);
            public const float TargetHeight = 0.45f;
            public const float Distance = 7f;
            public const float SideOffset = 0.6f;
            public const float MinimumPitch = -60f;
            public const float MaximumPitch = 80f;
        }

        [MenuItem("Tools/Hirakawa R/Options/Setup Scene_OptionTestScene")]
        public static void Setup()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Scene scene = EditorSceneManager.OpenScene(ScenePath);
            RemoveExistingObjects();
            CreateOptionsPrefab(scene);
            var player = CreateTestEnvironment();
            ConfigureTestCamera(player.transform);
            RenderSettings.ambientLight = StageLayout.AmbientColor;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            ValidateAssets();
        }

        private static void RemoveExistingObjects()
        {
            var old = GameObject.Find("Option Test Environment");
            if (old != null) UnityEngine.Object.DestroyImmediate(old);
            foreach (var menu in UnityEngine.Object.FindObjectsByType<InGameOptionMenu>())
                UnityEngine.Object.DestroyImmediate(menu.gameObject);
        }

        private static void CreateOptionsPrefab(Scene scene)
        {
            Directory.CreateDirectory(Root + "/Art/Materials");
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            AssetDatabase.Refresh();
            var prefabRoot = new GameObject("PF_InGameOptions");
            prefabRoot.AddComponent<GameOptions>();
            var menuComponent = prefabRoot.AddComponent<InGameOptionMenu>();
            menuComponent.BuildView();
            var prefab = PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
            UnityEngine.Object.DestroyImmediate(prefabRoot);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.GetComponent<InGameOptionMenu>().BuildView(true);
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.GetComponent<InGameOptionMenu>());
        }

        private static GameObject CreateTestEnvironment()
        {
            var environment = new GameObject("Option Test Environment");
            var floor = GetOrCreateMaterial("Option Floor", StageLayout.FloorColor);
            var teal = GetOrCreateMaterial("Option Teal", StageLayout.TealColor);
            var orange = GetOrCreateMaterial("Option Orange", StageLayout.OrangeColor);
            var grid = GetOrCreateMaterial("Option Grid", StageLayout.GridColor);
            var pale = GetOrCreateMaterial("Option Pale", StageLayout.PaleColor);
            CreateFloorGrid(environment.transform, floor, grid);
            var player = CreateTestPlayer(environment.transform, teal);
            CreateDistanceMarkers(environment.transform, floor, teal, pale);
            CreatePrimitive("Near Block", PrimitiveType.Cube, environment.transform, StageLayout.NearBlockPosition, StageLayout.NearBlockScale, orange);
            CreatePrimitive("Far Block", PrimitiveType.Cube, environment.transform, StageLayout.FarBlockPosition, StageLayout.FarBlockScale, orange);
            return player;
        }

        private static void CreateFloorGrid(Transform parent, Material floor, Material grid)
        {
            CreatePrimitive("Floor", PrimitiveType.Cube, parent, StageLayout.FloorPosition, StageLayout.FloorScale, floor);
            for (int i = StageLayout.GridMinimum; i <= StageLayout.GridMaximum; i += StageLayout.GridStride)
            {
                CreatePrimitive("Grid X " + i, PrimitiveType.Cube, parent, new Vector3(i, StageLayout.GridHeight, StageLayout.GridCenterZ), StageLayout.GridXScale, grid, false);
                CreatePrimitive("Grid Z " + i, PrimitiveType.Cube, parent, new Vector3(0, StageLayout.GridHeight, i + StageLayout.GridCenterZ), StageLayout.GridZScale, grid, false);
            }
        }

        private static GameObject CreateTestPlayer(Transform parent, Material material)
        {
            var player = CreatePrimitive("Test Player", PrimitiveType.Capsule, parent, StageLayout.PlayerPosition, Vector3.one, material);
            UnityEngine.Object.DestroyImmediate(player.GetComponent<Collider>());
            var controller = player.AddComponent<CharacterController>();
            controller.height = StageLayout.PlayerHeight; controller.radius = StageLayout.PlayerRadius;
            player.AddComponent<OptionTestPlayer>();
            return player;
        }

        private static void CreateDistanceMarkers(Transform parent, Material floor, Material teal, Material pale)
        {
            for (int i = 0; i < StageLayout.MarkerCount; i++)
            {
                float x = (i - StageLayout.MarkerCenterIndex) * StageLayout.MarkerSpacing;
                CreatePrimitive("Distance Marker " + i, PrimitiveType.Cube, parent, new Vector3(x, StageLayout.MarkerHeight, StageLayout.MarkerZ), StageLayout.MarkerScale, i % StageLayout.MarkerColorCycle == 0 ? teal : pale);
                CreatePrimitive("Marker Base " + i, PrimitiveType.Cylinder, parent, new Vector3(x, StageLayout.MarkerBaseHeight, StageLayout.MarkerZ), StageLayout.MarkerBaseScale, floor);
            }
        }

        private static void ConfigureTestCamera(Transform player)
        {
            var camera = Camera.main;
            if (camera == null) throw new InvalidOperationException("OptionTestSceneにMain Cameraがありません。");
            camera.transform.SetPositionAndRotation(CameraLayout.Position, CameraLayout.Rotation);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = CameraLayout.BackgroundColor;
            camera.fieldOfView = OptionValues.DefaultFieldOfView;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            var behavior = camera.GetComponent<OptionCameraBehavior>();
            if (behavior == null) behavior = camera.gameObject.AddComponent<OptionCameraBehavior>();
            var serialized = new SerializedObject(behavior);
            serialized.FindProperty("_target").objectReferenceValue = player;
            serialized.FindProperty("_targetHeight").floatValue = CameraLayout.TargetHeight;
            serialized.FindProperty("_distance").floatValue = CameraLayout.Distance;
            serialized.FindProperty("_sideOffset").floatValue = CameraLayout.SideOffset;
            serialized.FindProperty("_minPitch").floatValue = CameraLayout.MinimumPitch;
            serialized.FindProperty("_maxPitch").floatValue = CameraLayout.MaximumPitch;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (camera.GetComponent<OptionCameraShake>() == null) camera.gameObject.AddComponent<OptionCameraShake>();
        }

        private static UnityEngine.Material GetOrCreateMaterial(string name, Color color)
        {
            string path = Root + "/Art/Materials/M_" + name.Replace(" ", "") + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(path);
            if (material == null)
            {
                material = new UnityEngine.Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            material.SetFloat("_Smoothness", MaterialSmoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject CreatePrimitive(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, UnityEngine.Material material, bool collider = true)
        {
            var instance = GameObject.CreatePrimitive(type);
            instance.name = name;
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = position;
            instance.transform.localScale = scale;
            instance.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) UnityEngine.Object.DestroyImmediate(instance.GetComponent<Collider>());
            return instance;
        }

        [MenuItem("Tools/Hirakawa R/Options/Validate Option Assets")]
        public static void ValidateAssets()
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            if (prefab == null || prefab.GetComponent<GameOptions>() == null || prefab.GetComponent<InGameOptionMenu>() == null)
                throw new InvalidOperationException("オプションPrefabが正しく構成されていません。");
            if (prefab.GetComponentsInChildren<Canvas>(true).Length != ExpectedCanvasCount)
                throw new InvalidOperationException("オプションPrefabのCanvas構成が正しくありません。");
            if (new SerializedObject(prefab.GetComponent<InGameOptionMenu>()).FindProperty("_showTestHud").boolValue)
                throw new InvalidOperationException("共通Prefabではテスト用HUDを無効にしてください。");
            var camera = Camera.main;
            var behavior = camera == null ? null : camera.GetComponent<OptionCameraBehavior>();
            if (behavior == null || !AssetDatabase.GetAssetPath(MonoScript.FromMonoBehaviour(behavior)).StartsWith(Root + "/", StringComparison.Ordinal))
                throw new InvalidOperationException("テストカメラにはHirakawa_R内のOptionCameraBehaviorを使用してください。");
            foreach (var root in SceneManager.GetActiveScene().GetRootGameObjects())
                foreach (var transform in root.GetComponentsInChildren<Transform>(true))
                    if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(transform.gameObject) != 0)
                        throw new InvalidOperationException("Missing Script: " + transform.name);
        }
    }
}
