using UnityEditor;
using UnityEngine;
namespace Echo.Echolocation.Editor
{
    [CustomEditor(typeof(EchoTagDefinition)), CanEditMultipleObjects]
    public sealed class EchoTagDefinitionEditor : UnityEditor.Editor
    {
        private void DrawField(string path, string label) => EditorGUILayout.PropertyField(serializedObject.FindProperty(path), new GUIContent(label), true);
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUI.BeginChangeCheck();
            DrawField("_displayName", "表示名"); DrawField("_description", "説明");
            DrawField("_isBlocking", "エコーロケーションを遮断する");
            EditorGUILayout.HelpBox("専用タグのない物体は遮断します。このアセットを指定した対象すべてに、以下の表示設定が適用されます。", MessageType.Info);
            DrawEffect("_fill", "① 塗りつぶしによる強調表示");
            DrawEffect("_outline", "② アウトラインによる強調表示");
            DrawEffect("_original", "③ 元のマテリアルによる表示");
            EditorGUILayout.Space();
            DrawField("_settings._contactResolution", "全体接触判定の解像度");
            DrawField("_settings._surfaceMemoryResolution", "接触部分の残存精度");
            EditorGUILayout.HelpBox("残存精度は対象を立体的に分割する記録解像度です。高いほど境界が細かくなり、記録メモリが増えます。部分残存を使用する対象だけに確保します。", MessageType.Info);
            if (EditorGUI.EndChangeCheck()) serializedObject.FindProperty("_hasAssetSettings").boolValue = true;
            serializedObject.ApplyModifiedProperties();
        }
        private void DrawEffect(string name, string title)
        {
            string path = "_settings." + name;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            DrawField(path + "._isEnabled", "表示する");
            if (serializedObject.FindProperty(path + "._isEnabled").boolValue)
            {
                DrawCommon(path);
                DrawAppearance(name, path);
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space();
        }
        private void DrawCommon(string path)
        {
            DrawField(path + "._isThroughWalls", "壁越しに表示する");
            DrawField(path + "._displayScope", "表示範囲");
            DrawField(path + "._hasPersistence", "接触終了後も表示を残す");
            if (!serializedObject.FindProperty(path + "._hasPersistence").boolValue) return;
            DrawField(path + "._persistenceSeconds", "残存時間（秒）");
        }
        private void DrawAppearance(string name, string path)
        {
            if (name == "_fill") DrawField("_settings._color", "強調色 / 不透明度");
            if (name == "_outline")
            { DrawField("_settings._outlineColor", "輪郭の色 / 不透明度"); DrawField("_settings._outlineWidth", "輪郭の幅（ピクセル）"); }
            if (name == "_original")
            { DrawField("_settings._originalOpacity", "表示の不透明度"); DrawField("_settings._isOriginalHiddenOutsideEcho", "通常の描画を隠す"); }
        }
    }
}
