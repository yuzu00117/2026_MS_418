using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;
namespace Echo.Echolocation.Editor
{
    /// <summary>専用タグの3種類の表示を並べた検証シーンを作る。</summary>
    public static class EchoDemoSceneBuilder
    {
        // 既存シーンが参照するアセットの場所。フォルダ名は変更前のまま使う。
        private const string SettingsFolder = "Assets/_Sandbox/Omata_N/Settings/EchoGlitchDemo";
        private const string ShaderFolder = "Assets/_Sandbox/Omata_N/Art/Shaders/Echolocation/";
        private const string PipelinePath = SettingsFolder + "/SO_GlitchPipeline.asset";
        public static void CreateAssetDemo()
        {
            const string path = "Assets/_Sandbox/Omata_N/Scenes/Scene_EchoTagEffectsDemo.unity";
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(path)) return;
            EchoSurfaceBaker.EnsureFolder(SettingsFolder);
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("検証カメラ").AddComponent<Camera>();
            camera.transform.position = new Vector3(0, 2, -14);
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
            camera.GetUniversalAdditionalCameraData().requiresDepthTexture = true;
            var controller = CreateController(camera);
            for (int effect = 0; effect < 3; effect++) CreateAssetTarget(controller, effect);
            var driver = new GameObject("検証操作").AddComponent<EchoDemoDriver>();
            driver.Configure(CreatePipeline(), controller);
            EditorSceneManager.SaveScene(scene, path);
        }
        private static void CreateAssetTarget(EchoController controller, int index)
        {
            string[] names = { "塗りつぶし", "アウトライン", "元マテリアル" };
            var target = GameObject.CreatePrimitive(PrimitiveType.Cube);
            target.name = names[index]; target.transform.position = new Vector3(index * 4 - 4, 2, 3);
            target.transform.localScale = new Vector3(3, 4, 2);
            var definition = ScriptableObject.CreateInstance<EchoTagDefinition>();
            var settings = new EchoTagColorRule { IsFillEnabled = false, IsOutlineEnabled = false, IsOriginalEnabled = false };
            var effect = index == 0 ? settings.Fill : index == 1 ? settings.Outline : settings.Original;
            effect.IsEnabled = true; effect.DisplayScope = EchoOriginalRevealMode.ContactRegion;
            effect.HasPersistence = true; effect.PersistenceSeconds = 1;
            definition.Configure(settings, false);
            AssetDatabase.CreateAsset(definition, SettingsFolder + "/SO_Effect" + index + ".asset");
            target.AddComponent<EchoTarget>().Definition = definition;
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetColor("_BaseColor", index == 2 ? Color.red : Color.black);
            AssetDatabase.CreateAsset(material, SettingsFolder + "/M_Effect" + index + ".mat");
            target.GetComponent<Renderer>().sharedMaterial = material;
        }
        private static UniversalRenderPipelineAsset CreatePipeline()
        {
            var existing = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (existing) return existing;
            var renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
            AssetDatabase.CreateAsset(renderer, SettingsFolder + "/SO_GlitchRenderer.asset");
            var feature = ScriptableObject.CreateInstance<EchoUrpRendererFeature>();
            feature.name = "Echo Rendering";
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
            AssetDatabase.CreateAsset(pipeline, PipelinePath);
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
    }
}
