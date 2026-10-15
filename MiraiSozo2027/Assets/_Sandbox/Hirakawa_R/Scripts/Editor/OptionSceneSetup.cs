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
        public const string ScenePath = Root + "/Scenes/OptionTestScene.unity";
        public const string PrefabPath = "Assets/_Project/Prehubs/InGameOptions.prefab";

        [MenuItem("Tools/Hirakawa R/Options/Setup OptionTestScene")]
        public static void Setup()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Scene scene = EditorSceneManager.OpenScene(ScenePath);
            var old = GameObject.Find("Option Test Environment");
            if (old != null) UnityEngine.Object.DestroyImmediate(old);
            foreach (var menu in UnityEngine.Object.FindObjectsByType<InGameOptionMenu>())
                UnityEngine.Object.DestroyImmediate(menu.gameObject);

            Directory.CreateDirectory(Root + "/Art/Materials");
            Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));
            AssetDatabase.Refresh();
            var prefabRoot = new GameObject("InGameOptions");
            prefabRoot.AddComponent<GameOptions>();
            var menuComponent = prefabRoot.AddComponent<InGameOptionMenu>();
            menuComponent.BuildView();
            var prefab = PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath);
            UnityEngine.Object.DestroyImmediate(prefabRoot);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.GetComponent<InGameOptionMenu>().BuildView(true);
            PrefabUtility.RecordPrefabInstancePropertyModifications(instance.GetComponent<InGameOptionMenu>());

            var environment = new GameObject("Option Test Environment");
            var floor = Material("Option Floor", new Color32(37, 57, 70, 255));
            var teal = Material("Option Teal", new Color32(68, 206, 188, 255));
            var orange = Material("Option Orange", new Color32(237, 164, 91, 255));
            var grid = Material("Option Grid", new Color32(70, 101, 112, 255));
            var pale = Material("Option Pale", new Color32(195, 216, 222, 255));
            Primitive("Floor", PrimitiveType.Cube, environment.transform, new Vector3(0, -0.25f, 3), new Vector3(42, 0.5f, 42), floor);
            for (int i = -10; i <= 10; i += 2)
            {
                Primitive("Grid X " + i, PrimitiveType.Cube, environment.transform, new Vector3(i, 0.006f, 3), new Vector3(0.018f, 0.012f, 40), grid, false);
                Primitive("Grid Z " + i, PrimitiveType.Cube, environment.transform, new Vector3(0, 0.006f, i + 3), new Vector3(40, 0.012f, 0.018f), grid, false);
            }
            var player = Primitive("Test Player", PrimitiveType.Capsule, environment.transform, new Vector3(0, 1.08f, 0), Vector3.one, teal);
            UnityEngine.Object.DestroyImmediate(player.GetComponent<Collider>());
            var controller = player.AddComponent<CharacterController>();
            controller.height = 2; controller.radius = 0.5f;
            player.AddComponent<OptionTestPlayer>();
            for (int i = 0; i < 5; i++)
            {
                float x = (i - 2) * 4f;
                Primitive("Distance Marker " + i, PrimitiveType.Cube, environment.transform, new Vector3(x, 1.5f, 10), new Vector3(1f, 3f, 1f), i % 2 == 0 ? teal : pale);
                Primitive("Marker Base " + i, PrimitiveType.Cylinder, environment.transform, new Vector3(x, 0.14f, 10), new Vector3(2f, 0.14f, 2f), floor);
            }
            Primitive("Near Block", PrimitiveType.Cube, environment.transform, new Vector3(-3.5f, 0.7f, 2.5f), new Vector3(1.4f, 1.4f, 1.4f), orange);
            Primitive("Far Block", PrimitiveType.Cube, environment.transform, new Vector3(4, 1f, 5), new Vector3(2, 2, 2), orange);
            var camera = Camera.main;
            if (camera == null) throw new InvalidOperationException("OptionTestSceneにMain Cameraがありません。");
            camera.transform.SetPositionAndRotation(new Vector3(0, 3, -7), Quaternion.Euler(15, 0, 0));
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color32(25, 42, 59, 255);
            camera.fieldOfView = 60;
            camera.GetUniversalAdditionalCameraData().renderPostProcessing = true;
            var behavior = camera.GetComponent<OptionCameraBehavior>();
            if (behavior == null) behavior = camera.gameObject.AddComponent<OptionCameraBehavior>();
            var serialized = new SerializedObject(behavior);
            serialized.FindProperty("_target").objectReferenceValue = player.transform;
            serialized.FindProperty("_targetHeight").floatValue = 0.45f;
            serialized.FindProperty("_distance").floatValue = 7f;
            serialized.FindProperty("_sideOffset").floatValue = 0.6f;
            serialized.FindProperty("_minPitch").floatValue = -60;
            serialized.FindProperty("_maxPitch").floatValue = 80;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            if (camera.GetComponent<OptionCameraShake>() == null) camera.gameObject.AddComponent<OptionCameraShake>();
            RenderSettings.ambientLight = new Color(0.5f, 0.57f, 0.64f);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            ValidateAssets();
            Debug.Log("OPTION_SETUP_OK: " + ScenePath);
        }

        private static UnityEngine.Material Material(string name, Color color)
        {
            string path = Root + "/Art/Materials/" + name.Replace(" ", "") + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<UnityEngine.Material>(path);
            if (material == null)
            {
                material = new UnityEngine.Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            material.SetFloat("_Smoothness", 0.25f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject Primitive(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, UnityEngine.Material material, bool collider = true)
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
            if (prefab.GetComponentsInChildren<Canvas>(true).Length != 1)
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
            Debug.Log("OPTION_ASSETS_OK: Prefab / Scene / Script references");
        }
    }
}
