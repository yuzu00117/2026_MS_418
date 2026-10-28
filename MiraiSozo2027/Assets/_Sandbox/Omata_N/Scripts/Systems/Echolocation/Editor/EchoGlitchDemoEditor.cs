using UnityEditor;
using UnityEngine;
namespace Echo.Echolocation.Editor
{
    [CustomEditor(typeof(EchoGlitchDemo)), CanEditMultipleObjects]
    public sealed class EchoGlitchDemoEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_pipeline"), new GUIContent("デモ専用の描画設定"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_controller"), new GUIContent("エコーの管理元"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_isAutomatic"), new GUIContent("スキャンを自動で切り替える"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_cycleSeconds"), new GUIContent("切り替え間隔（秒）"));
            serializedObject.ApplyModifiedProperties();
        }
    }
}
