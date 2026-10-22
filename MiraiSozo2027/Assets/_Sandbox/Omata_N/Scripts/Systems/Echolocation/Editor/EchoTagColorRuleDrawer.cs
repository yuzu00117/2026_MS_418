// タグを手入力させずUnityのタグ一覧から選ばせる。
using UnityEditor;
using UnityEngine;

namespace Echo.Echolocation.Editor
{
    [CustomPropertyDrawer(typeof(EchoTagColorRule))]
    public sealed class EchoTagColorRuleDrawer : PropertyDrawer
    {
        private const int BaseLineCount = 4;
        private const int FillLineCount = 2;
        private const int OutlineLineCount = 4;
        private const int OriginalLineCount = 5;
        private const int ContactLineCount = 1;
        private const float LineSpacing = 4;

        public override float GetPropertyHeight(SerializedProperty p, GUIContent l)
        {
            int lines = BaseLineCount;
            if (!p.FindPropertyRelative("_definition").objectReferenceValue) lines++;
            if (p.FindPropertyRelative("_fillEnabled").boolValue) lines += FillLineCount;
            if (p.FindPropertyRelative("_outlineEnabled").boolValue) lines += OutlineLineCount;
            if (p.FindPropertyRelative("_originalEnabled").boolValue) lines += OriginalLineCount;
            if (p.FindPropertyRelative("_originalEnabled").boolValue || p.FindPropertyRelative("_outlineEnabled").boolValue) lines += ContactLineCount;
            return lines * (EditorGUIUtility.singleLineHeight + LineSpacing);
        }
        public override void OnGUI(Rect rect, SerializedProperty p, GUIContent label)
        {
            EditorGUI.BeginProperty(rect, label, p);
            rect.height = EditorGUIUtility.singleLineHeight;
            EditorGUI.PropertyField(new Rect(rect.x, rect.y, 20, rect.height), p.FindPropertyRelative("_isEnabled"), GUIContent.none);
            EditorGUI.PropertyField(new Rect(rect.x + 24, rect.y, rect.width - 24, rect.height),
                p.FindPropertyRelative("_definition"), new GUIContent("専用タグ"));
            if (!p.FindPropertyRelative("_definition").objectReferenceValue)
            {
                rect.y += EditorGUIUtility.singleLineHeight + LineSpacing;
                var tag = p.FindPropertyRelative("_tagName");
                EditorGUI.BeginChangeCheck();
                string value = EditorGUI.TagField(rect, "旧Unityタグ（移行用）", tag.stringValue);
                if (EditorGUI.EndChangeCheck()) tag.stringValue = value;
            }
            void Field(string path, string title)
            {
                rect.y += EditorGUIUtility.singleLineHeight + LineSpacing;
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
                Field("_revealMode", "表示範囲（EchoRevealState）");
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
