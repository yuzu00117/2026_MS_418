using UnityEditor;
using UnityEngine;
namespace Echo.Echolocation.Editor
{
    [CustomPropertyDrawer(typeof(EchoSurfaceBinding))]
    public sealed class EchoSurfaceBindingDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
            => EditorGUIUtility.singleLineHeight * 2 + EditorGUIUtility.standardVerticalSpacing;
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            position.height = EditorGUIUtility.singleLineHeight;
            EditorGUI.PropertyField(position, property.FindPropertyRelative("_renderer"), new GUIContent("対象Renderer"));
            position.y += position.height + EditorGUIUtility.standardVerticalSpacing;
            EditorGUI.PropertyField(position, property.FindPropertyRelative("_data"), new GUIContent("分割データ"));
            EditorGUI.EndProperty();
        }
    }
}
