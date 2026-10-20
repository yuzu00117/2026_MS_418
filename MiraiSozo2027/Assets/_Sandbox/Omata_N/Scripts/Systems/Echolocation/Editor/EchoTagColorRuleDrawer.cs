// タグを手入力させずUnityのタグ一覧から選ばせる。
using UnityEditor;
using UnityEngine;

namespace Echo.Echolocation.Editor
{
    [CustomPropertyDrawer(typeof(EchoTagColorRule))]
    public sealed class EchoTagColorRuleDrawer : PropertyDrawer
    {
        public override float GetPropertyHeight(SerializedProperty p, GUIContent l)
        {
            int lines = 4;
            if (p.FindPropertyRelative("_fillEnabled").boolValue) lines += 2;
            if (p.FindPropertyRelative("_outlineEnabled").boolValue) lines += 4;
            if (p.FindPropertyRelative("_originalEnabled").boolValue) lines += 4;
            if (p.FindPropertyRelative("_originalEnabled").boolValue || p.FindPropertyRelative("_outlineEnabled").boolValue) lines++;
            return lines * (EditorGUIUtility.singleLineHeight + 4);
        }
        public override void OnGUI(Rect rect, SerializedProperty p, GUIContent label)
        {
            EditorGUI.BeginProperty(rect, label, p);
            rect.height = EditorGUIUtility.singleLineHeight;
            EditorGUI.PropertyField(new Rect(rect.x, rect.y, 20, rect.height), p.FindPropertyRelative("_isEnabled"), GUIContent.none);
            var tag = p.FindPropertyRelative("_tagName");
            EditorGUI.BeginChangeCheck();
            string value = EditorGUI.TagField(new Rect(rect.x + 24, rect.y, rect.width - 24, rect.height), tag.stringValue);
            if (EditorGUI.EndChangeCheck())
                tag.stringValue = value;
            void Field(string path, string title)
            {
                rect.y += EditorGUIUtility.singleLineHeight + 4;
                EditorGUI.PropertyField(rect, p.FindPropertyRelative(path), new GUIContent(title), true);
            }
            Field("_fillEnabled", "表面を色で塗る");
            if (p.FindPropertyRelative("_fillEnabled").boolValue)
            {
                Field("_color", "塗りの色 / 不透明度");
                Field("_fillOcclusion", "塗りの壁越し表示");
            }
            Field("_outlineEnabled", "全体の輪郭を表示");
            if (p.FindPropertyRelative("_outlineEnabled").boolValue)
            {
                Field("_outlineColor", "輪郭の色 / 不透明度");
                Field("_outlineWidth", "輪郭幅（px）");
                Field("_outlineHoldDuration", "接触後の輪郭残存（秒）");
                Field("_outlineOcclusion", "輪郭の壁越し表示");
            }
            Field("_originalEnabled", "元のマテリアルを表示");
            if (p.FindPropertyRelative("_originalEnabled").boolValue)
            {
                Field("_hideOriginalOutsideEcho", "通常時は非表示にする");
                Field("_originalHoldDuration", "接触後の表示残存（秒）");
                Field("_originalOpacity", "元の見た目の不透明度");
                Field("_originalOcclusion", "元マテリアルの壁越し表示");
            }
            if (p.FindPropertyRelative("_originalEnabled").boolValue || p.FindPropertyRelative("_outlineEnabled").boolValue)
                Field("_contactResolution", "接触判定の解像度");
            EditorGUI.EndProperty();
        }
    }
}
