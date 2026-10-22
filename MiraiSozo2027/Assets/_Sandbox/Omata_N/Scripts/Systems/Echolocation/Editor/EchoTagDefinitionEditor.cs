using UnityEditor;
using UnityEngine;
namespace Echo.Echolocation.Editor
{
    [CustomEditor(typeof(EchoTagDefinition)), CanEditMultipleObjects]
    public sealed class EchoTagDefinitionEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_displayName"), new GUIContent("表示名"), true);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_description"), new GUIContent("説明"), true);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
