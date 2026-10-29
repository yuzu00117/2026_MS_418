using UnityEditor;
using UnityEngine;
namespace Echo.Echolocation.Editor
{
    [CustomPropertyDrawer(typeof(EchoGlitchSettings))]
    public sealed class EchoGlitchSettingsDrawer : PropertyDrawer
    {
        private const float Spacing = 4;
        private static readonly string[] Fields = { "_mode", "_color", "_intensity", "_width", "_displacement", "_blockHeight", "_speed", "_duration", "_isContactBoundaryEnabled" };
        private static readonly string[] Labels = { "発生するタイミング", "色 / 不透明度", "グリッチの強度", "境界の幅（ピクセル）", "横ずれの幅（ピクセル）", "帯の高さ（ピクセル）", "変化速度（回 / 秒）", "表示開始時の継続時間（秒）", "接触部分の境界だけに限定" };
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label) =>
            (property.FindPropertyRelative("_isEnabled").boolValue ? Fields.Length + 1 : 1) * (EditorGUIUtility.singleLineHeight + Spacing) - Spacing;
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);
            position.height = EditorGUIUtility.singleLineHeight;
            var enabled = property.FindPropertyRelative("_isEnabled");
            EditorGUI.PropertyField(position, enabled, new GUIContent("輪郭のグリッチを表示"));
            if (enabled.boolValue)
            {
                EditorGUI.indentLevel++;
                for (int i = 0; i < Fields.Length; i++)
                {
                    position.y += EditorGUIUtility.singleLineHeight + Spacing;
                    EditorGUI.PropertyField(position, property.FindPropertyRelative(Fields[i]), new GUIContent(Labels[i]));
                }
                EditorGUI.indentLevel--;
            }
            EditorGUI.EndProperty();
        }
    }
}
