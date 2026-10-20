// チーム用シーンで使用する描画Featureとタグだけを設定する。
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
namespace Echo.Echolocation.Samples.Editor
{
    public static class EchoDemoSetup
    {
        [MenuItem("Tools/エコーロケーション/移植先の初期設定")]
        public static void InitializeImportedProject()
        {
            if (Application.isPlaying)
                throw new System.InvalidOperationException("再生を停止してから初期設定してください。");
            EnsureTag("EchoEnemy");
            EnsureTag("EchoItem");
            EnsureTag("EchoStructure");
            InstallFeatures();
            Debug.Log("エコー：描画Featureとタグを登録しました。");
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

    }
}
