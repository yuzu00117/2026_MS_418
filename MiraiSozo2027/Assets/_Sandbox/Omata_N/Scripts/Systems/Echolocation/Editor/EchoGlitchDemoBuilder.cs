using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
namespace Echo.Echolocation.Editor
{
    public static class EchoGlitchDemoBuilder
    {
        public const string ScenePath = "Assets/_Sandbox/Omata_N/Scenes/Scene_EchoGlitchDemo.unity";
        private const string SettingsFolder = "Assets/_Sandbox/Omata_N/Settings/EchoGlitchDemo";
        private const string ShaderFolder = "Assets/_Sandbox/Omata_N/Art/Shaders/Echolocation/";
        [MenuItem("Tools/エコーロケーション/05 新しいグリッチDemoSceneを作成", false, 105)]
        public static void Create()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath))
            { EditorSceneManager.OpenScene(ScenePath); return; }
            EchoSurfaceBaker.EnsureFolder(SettingsFolder);
            var pipeline = CreatePipeline();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, Application.isBatchMode ? NewSceneMode.Single : NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var camera = new GameObject("Demo Camera").AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 2, -14);
            camera.backgroundColor = new Color(.015f, .02f, .035f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.GetUniversalAdditionalCameraData().requiresDepthTexture = true;
            var controller = CreateController(camera);
            CreateTarget(controller, "Always silhouette", -5, EchoGlitchMode.Always, false, false);
            CreateTarget(controller, "Reveal whole object", 0, EchoGlitchMode.OnReveal, true, false);
            CreateTarget(controller, "Contact boundary", 5, EchoGlitchMode.WhileRevealed, true, true);
            var driver = new GameObject("Demo Controls").AddComponent<EchoGlitchDemo>();
            driver.Configure(pipeline, controller);
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
        public static void MigrateDemoSettings()
        {
            MigrateSceneSettings(ScenePath);
            MigrateSceneSettings("Assets/_Sandbox/Omata_N/Scenes/Scene_EchoDemo.unity");
        }
        private static void MigrateSceneSettings(string scenePath)
        {
            var scene = EditorSceneManager.OpenScene(scenePath);
            var controllers = new System.Collections.Generic.List<EchoController>();
            foreach (var root in scene.GetRootGameObjects())
                controllers.AddRange(root.GetComponentsInChildren<EchoController>(true));
            foreach (var root in scene.GetRootGameObjects())
                foreach (var legacy in root.GetComponentsInChildren<EchoGlitch>(true))
                {
                    var tag = legacy.GetComponent<EchoTag>();
                    foreach (var controller in controllers)
                    foreach (var rule in controller.Settings.TagColorRules)
                        if (tag && rule.Definition == tag.Definition)
                        {
                            JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(legacy), rule.Glitch);
                            rule.Glitch.IsEnabled = legacy.enabled;
                            EditorUtility.SetDirty(controller);
                        }
                    // 元の設定は復元用に残す。描画は専用タグの設定だけを参照する。
                }
            EditorSceneManager.SaveScene(scene, scenePath);
        }
        private static UniversalRenderPipelineAsset CreatePipeline()
        {
            var existing = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(SettingsFolder + "/SO_GlitchPipeline.asset");
            if (existing) return existing;
            var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(renderer, SettingsFolder + "/SO_GlitchRenderer.asset");
            var feature = ScriptableObject.CreateInstance<EchoUrpRendererFeature>();
            feature.name = "Echo Glitch Rendering";
            var serialized = new SerializedObject(feature);
            serialized.FindProperty("_highlightShader").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Shader>(ShaderFolder + "EchoHighlight.shader");
            serialized.FindProperty("_compositeShader").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Shader>(ShaderFolder + "EchoComposite.shader");
            serialized.FindProperty("_rangeShader").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Shader>(ShaderFolder + "EchoRange.shader");
            serialized.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.AddObjectToAsset(feature, renderer);
            renderer.rendererFeatures.Add(feature);
            renderer.SetDirty();
            AssetDatabase.SaveAssetIfDirty(renderer);
            var pipeline = UniversalRenderPipelineAsset.Create(renderer);
            pipeline.supportsCameraDepthTexture = true;
            AssetDatabase.CreateAsset(pipeline, SettingsFolder + "/SO_GlitchPipeline.asset");
            AssetDatabase.SaveAssetIfDirty(pipeline);
            return pipeline;
        }
        private static EchoController CreateController(Camera camera)
        {
            var controller = new GameObject("Demo Echo Controller").AddComponent<EchoController>();
            controller.transform.position = new Vector3(0, 2, 0);
            controller.TargetCamera = camera;
            controller.Settings.Duration = 3;
            controller.Settings.Radius = 5.5f;
            controller.Settings.Shadow.IsEnabled = false;
            controller.Settings.OcclusionMode = EchoOcclusionMode.VisibleOnly;
            return controller;
        }
        private static void CreateTarget(EchoController controller, string name, float x, EchoGlitchMode mode, bool isHidden, bool isPartial)
        {
            var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = name;
            target.transform.position = new Vector3(x, 2, 3);
            target.transform.localScale = new Vector3(3, 4, 2);
            var definition = ScriptableObject.CreateInstance<EchoTagDefinition>();
            AssetDatabase.CreateAsset(definition, SettingsFolder + "/SO_" + mode + ".asset");
            target.AddComponent<EchoTag>().Definition = definition;
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetColor("_BaseColor", new Color(.16f, .2f, .25f));
            AssetDatabase.CreateAsset(material, SettingsFolder + "/M_" + mode + ".mat");
            target.GetComponent<Renderer>().sharedMaterial = material;
            var rule = new EchoTagColorRule
            {
                Definition = definition, FillEnabled = false, OriginalEnabled = isHidden,
                RevealMode = isPartial ? EchoOriginalRevealMode.ContactRegion : EchoOriginalRevealMode.WholeObject,
                OriginalHoldDuration = 0, OutlineEnabled = false
            };
            rule.Glitch.IsEnabled = true;
            rule.Glitch.Mode = mode;
            controller.Settings.TagColorRules.Add(rule);
        }
    }
}
