using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Aura.Inspector.Editor
{
    /// @brief [InspectorLabel] を付けた欄を、決めた名前で出す
    /// 名前と説明文だけを差し替えて、あとの描き方（Min・Range、なければふつうの欄）は Unity に任せる
    /// 渡した名前を使わない描き方の欄（Input System の操作）は、決めた名前を上の 1 行に出す
    [CustomPropertyDrawer(typeof(InspectorLabelAttribute))]
    public sealed class InspectorLabelDrawer : PropertyDrawer
    {
        /// @brief 渡した名前を使わず、変数名から見出しを作る描き方の型。Input System を参照しないよう、型の名前で比べる
        private static readonly string[] LabelIgnoringTypeNames =
        {
            "UnityEngine.InputSystem.InputAction",
            "UnityEngine.InputSystem.InputActionMap",
        };

        private GUIContent _label;  ///< 差し替えた名前と説明文。欄ごとに変わらないので、最初に 1 回だけ作る

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            GUIContent newLabel = GetLabel(label);
            if (IsLabelLine(newLabel))
            {
                var line = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
                EditorGUI.LabelField(line, newLabel);
                position.yMin += GetLabelLineHeight();
            }
            EditorGUI.PropertyField(position, property, newLabel, true);
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            GUIContent newLabel = GetLabel(label);
            float height = EditorGUI.GetPropertyHeight(property, newLabel, true);
            return IsLabelLine(newLabel) ? height + GetLabelLineHeight() : height;
        }

        /// @brief 差し替えた名前と説明文を返す
        /// @param label Unity から渡された名前
        /// @return 差し替えた名前。名前を出さない欄なら、渡された名前のまま
        private GUIContent GetLabel(GUIContent label)
        {
            // 名前を出さない欄（GUIContent.none で描かれたもの）は、名前を出さないままにする
            if (label == null || string.IsNullOrEmpty(label.text))
            {
                return label;
            }
            if (_label == null)
            {
                // 説明文は、渡された名前ではなく変数の [Tooltip] から取る（渡され方によって説明文が空のことがあるため）
                var tooltip = fieldInfo.GetCustomAttribute<TooltipAttribute>();
                _label = InspectorLabelUtility.CreateLabel(((InspectorLabelAttribute)attribute).Text, tooltip?.tooltip, fieldInfo.Name);
            }
            return _label;
        }

        /// @brief 名前を上の 1 行に出すか。渡した名前を使わない描き方の欄で、名前を出すときだけ
        /// @param label 差し替えた名前
        /// @return 上の 1 行に出すなら true
        private bool IsLabelLine(GUIContent label)
        {
            return label != null && !string.IsNullOrEmpty(label.text)
                && Array.IndexOf(LabelIgnoringTypeNames, fieldInfo.FieldType.FullName) >= 0;
        }

        /// @brief 上の 1 行に出す名前の高さ（行の間を含む）
        private static float GetLabelLineHeight()
        {
            return EditorGUIUtility.singleLineHeight + EditorGUIUtility.standardVerticalSpacing;
        }
    }
}
