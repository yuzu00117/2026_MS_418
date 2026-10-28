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

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            int lines = BaseLineCount;
            if (!property.FindPropertyRelative("_definition").objectReferenceValue) lines++;
            if (property.FindPropertyRelative("_isFillEnabled").boolValue) lines += FillLineCount;
            if (property.FindPropertyRelative("_isOutlineEnabled").boolValue) lines += OutlineLineCount;
            if (property.FindPropertyRelative("_isOriginalEnabled").boolValue) lines += OriginalLineCount;
            if (property.FindPropertyRelative("_isOriginalEnabled").boolValue || property.FindPropertyRelative("_isOutlineEnabled").boolValue) lines += ContactLineCount;
            return lines * (EditorGUIUtility.singleLineHeight + LineSpacing) + LineSpacing;
        }
        public override void OnGUI(Rect rect, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(rect, label, property);
            rect.height = EditorGUIUtility.singleLineHeight;
            EditorGUI.PropertyField(new Rect(rect.x, rect.y, 20, rect.height), property.FindPropertyRelative("_isEnabled"), GUIContent.none);
            EditorGUI.PropertyField(new Rect(rect.x + 24, rect.y, rect.width - 24, rect.height),
                property.FindPropertyRelative("_definition"), new GUIContent("専用タグ"));
            if (!property.FindPropertyRelative("_definition").objectReferenceValue)
            {
                rect.y += EditorGUIUtility.singleLineHeight + LineSpacing;
                var tag = property.FindPropertyRelative("_tagName");
                EditorGUI.BeginChangeCheck();
                string value = EditorGUI.TagField(rect, "旧Unityタグ（移行用）", tag.stringValue);
                if (EditorGUI.EndChangeCheck()) tag.stringValue = value;
            }
            void DrawField(string path, string title)
            {
                rect.y += EditorGUIUtility.singleLineHeight + LineSpacing;
                EditorGUI.PropertyField(rect, property.FindPropertyRelative(path), new GUIContent(title), true);
            }
            DrawField("_isFillEnabled", "表面を色で塗る");
            if (property.FindPropertyRelative("_isFillEnabled").boolValue)
            {
                DrawField("_color", "塗りの色 / 不透明度");
                DrawField("_fillOcclusion", "塗りの壁越し表示");
            }
            DrawField("_isOutlineEnabled", "全体の輪郭を表示");
            if (property.FindPropertyRelative("_isOutlineEnabled").boolValue)
            {
                DrawField("_outlineColor", "輪郭の色 / 不透明度");
                DrawField("_outlineWidth", "輪郭幅（ピクセル）");
                DrawField("_outlineHoldDuration", "接触後の輪郭残存（秒）");
                DrawField("_outlineOcclusion", "輪郭の壁越し表示");
            }
            DrawField("_isOriginalEnabled", "元のマテリアルを表示");
            if (property.FindPropertyRelative("_isOriginalEnabled").boolValue)
            {
                DrawField("_revealMode", "表示範囲");
                DrawField("_isOriginalHiddenOutsideEcho", "通常時は非表示にする");
                DrawField("_originalHoldDuration", "全体表示の残存（秒）");
                DrawField("_originalOpacity", "元の見た目の不透明度");
                DrawField("_originalOcclusion", "元マテリアルの壁越し表示");
            }
            if (property.FindPropertyRelative("_isOriginalEnabled").boolValue || property.FindPropertyRelative("_isOutlineEnabled").boolValue)
                DrawField("_contactResolution", "接触判定の解像度");
            EditorGUI.EndProperty();
        }
    }
}
