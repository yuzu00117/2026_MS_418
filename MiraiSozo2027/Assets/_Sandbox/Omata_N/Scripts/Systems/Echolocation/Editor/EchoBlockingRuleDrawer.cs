// タグと遮断・透過を同じ行で選び、チェックの意味を日本語で明示する。
using UnityEditor;
using UnityEngine;

namespace Echo.Echolocation.Editor
{
    [CustomPropertyDrawer(typeof(EchoBlockingRule))]
    public sealed class EchoBlockingRuleDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) => EditorGUIUtility.singleLineHeight + 4;
        public override void OnGUI(Rect rect, SerializedProperty p, GUIContent label)
        {
            EditorGUI.BeginProperty(rect, label, p);
            rect.height = EditorGUIUtility.singleLineHeight;
            EditorGUI.PropertyField(new Rect(rect.x, rect.y, 20, rect.height), p.FindPropertyRelative("_isEnabled"), GUIContent.none);
            var tag = p.FindPropertyRelative("_tagName");
            EditorGUI.BeginChangeCheck();
            var value = EditorGUI.TagField(new Rect(rect.x + 24, rect.y, rect.width * .6f - 24, rect.height), tag.stringValue);
            if (EditorGUI.EndChangeCheck())
                tag.stringValue = value;
            var blocks = p.FindPropertyRelative("_isBlocking");
            EditorGUI.BeginChangeCheck();
            int selected = EditorGUI.Popup(new Rect(rect.x + rect.width * .6f + 4, rect.y, rect.width * .4f - 4, rect.height), blocks.boolValue ? 0 : 1, new[] { "遮断する", "遮断しない（透過）" });
            if (EditorGUI.EndChangeCheck())
                blocks.boolValue = selected == 0;
            EditorGUI.EndProperty();
        }
    }
}
