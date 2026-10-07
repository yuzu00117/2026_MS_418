// タグを手入力させずUnityのタグ一覧から選ばせる。
using UnityEditor;
using UnityEngine;

namespace Echo.Echolocation.Editor
{
    [CustomPropertyDrawer(typeof(EchoTagColorRule))]
    public sealed class EchoTagColorRuleDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty p, GUIContent l) => EditorGUIUtility.singleLineHeight + 4;
        public override void OnGUI(Rect rect, SerializedProperty p, GUIContent label)
        {
            EditorGUI.BeginProperty(rect, label, p);
            rect.height = EditorGUIUtility.singleLineHeight;
            EditorGUI.PropertyField(new Rect(rect.x, rect.y, 20, rect.height), p.FindPropertyRelative("_isEnabled"), GUIContent.none);
            var tag = p.FindPropertyRelative("_tagName");
            EditorGUI.BeginChangeCheck();
            string value = EditorGUI.TagField(new Rect(rect.x + 24, rect.y, rect.width * .5f - 24, rect.height), tag.stringValue);
            if (EditorGUI.EndChangeCheck())
                tag.stringValue = value;
            EditorGUI.PropertyField(new Rect(rect.x + rect.width * .5f + 4, rect.y, rect.width * .5f - 4, rect.height), p.FindPropertyRelative("_color"), GUIContent.none);
            EditorGUI.EndProperty();
        }
    }
}
