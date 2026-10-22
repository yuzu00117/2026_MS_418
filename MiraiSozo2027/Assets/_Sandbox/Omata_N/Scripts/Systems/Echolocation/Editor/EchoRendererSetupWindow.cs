using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
namespace Echo.Echolocation.Editor
{
    /// <summary>利用者が指定した個人用Rendererだけに描画Featureを追加する。</summary>
    public sealed class EchoRendererSetupWindow : EditorWindow
    {
        private UniversalRendererData _renderer;
        [MenuItem("Tools/エコーロケーション/01 描画機能を準備", false, 101)]
        public static void Open() => GetWindow<EchoRendererSetupWindow>("描画機能の準備");
        private void OnGUI()
        {
            EditorGUILayout.HelpBox("エコーを画面に描くための機能をURP Rendererへ追加します。シーンごとではなく、使用するRendererごとに一度設定します。カメラのRenderer設定とURP AssetのRenderer Listを確認し、実際に使用するアセットを指定してください。", MessageType.Info);
            _renderer = (UniversalRendererData)EditorGUILayout.ObjectField("使用するRenderer", _renderer, typeof(UniversalRendererData), false);
            bool canEdit = _renderer && AssetDatabase.GetAssetPath(_renderer).StartsWith("Assets/_Sandbox/Omata_N/");
            if (_renderer && !canEdit) EditorGUILayout.HelpBox("共有設定の変更を避けるため、Omata_N配下のRendererのみ設定できます。", MessageType.Warning);
            using (new EditorGUI.DisabledScope(Application.isPlaying || !canEdit))
                if (GUILayout.Button("このRendererにエコー描画を設定")) Install(_renderer);
        }
        public static void Install(UniversalRendererData renderer)
        {
            if (Application.isPlaying || !renderer || !AssetDatabase.GetAssetPath(renderer).StartsWith("Assets/_Sandbox/Omata_N/"))
                throw new System.InvalidOperationException("編集モードでOmata_N配下のRendererを指定してください。");
            var highlight = Shader.Find("Hidden/Echo/Highlight");
            var composite = Shader.Find("Hidden/Echo/Composite");
            var range = Shader.Find("Hidden/Echo/Range");
            if (!highlight || !composite || !range) throw new System.InvalidOperationException("エコー用シェーダーが見つかりません。");
            EchoUrpRendererFeature feature = null;
            foreach (var existing in renderer.rendererFeatures)
                if (existing is EchoUrpRendererFeature echo) { feature = echo; break; }
            if (!feature)
            {
                feature = CreateInstance<EchoUrpRendererFeature>();
                feature.name = "Echo Highlight";
                AssetDatabase.AddObjectToAsset(feature, renderer);
                renderer.rendererFeatures.Add(feature);
            }
            var serialized = new SerializedObject(feature);
            serialized.FindProperty("_highlightShader").objectReferenceValue = highlight;
            serialized.FindProperty("_compositeShader").objectReferenceValue = composite;
            serialized.FindProperty("_rangeShader").objectReferenceValue = range;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            feature.SetActive(true);
            renderer.SetDirty();
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(renderer);
            AssetDatabase.SaveAssetIfDirty(renderer);
            Debug.Log("指定したRendererのエコー描画設定を保存しました。", renderer);
        }
    }
}
