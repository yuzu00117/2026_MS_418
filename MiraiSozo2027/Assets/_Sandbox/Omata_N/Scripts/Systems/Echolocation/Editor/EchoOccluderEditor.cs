using UnityEditor;
using UnityEngine;
namespace Echo.Echolocation.Editor
{
    [CustomEditor(typeof(EchoOccluder)), CanEditMultipleObjects]
    public sealed class EchoOccluderEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_isBlocking"), new GUIContent("エコーを遮る"), true);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
