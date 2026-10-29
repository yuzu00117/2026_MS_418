using UnityEditor;
using UnityEngine;
namespace Echo.Echolocation.Editor
{
    [CustomEditor(typeof(EchoTag)), CanEditMultipleObjects]
    public sealed class EchoTagEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EchoComponentMigration.DrawLegacyNotice("旧方式の互換コンポーネントです。専用タグはEchoTargetで指定します。EchoTargetが未設定の間だけこの指定を使用します。");
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_definition"), new GUIContent("専用タグ（旧）"), true);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
