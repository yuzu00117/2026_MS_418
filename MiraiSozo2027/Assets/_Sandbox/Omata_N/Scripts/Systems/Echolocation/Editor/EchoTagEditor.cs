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
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_definition"), new GUIContent("専用タグ"), true);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
