using UnityEditor;
using UnityEngine;
namespace Echo.Echolocation.Editor
{
    [CustomEditor(typeof(EchoRevealWorld)), CanEditMultipleObjects]
    public sealed class EchoRevealWorldEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_staleTimeout"), new GUIContent("判定結果の有効期限（秒）"), true);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
