using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Aura.Inspector.Editor
{
    /// @brief [InspectorLabel] の名前を作る。専用の Inspector（CustomEditor）で欄を自分で描くとき（EditorGUI.LayerField など）にも使う
    public static class InspectorLabelUtility
    {
        private const string FieldNameFormat = "（変数名: {0}）";  ///< 説明文の最後に添える、元の変数名の書き方
        private const BindingFlags FieldFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;

        /// @brief Inspector に出す名前と説明文を作る。説明文の最後に、元の変数名を添える
        /// @param text Inspector に出す名前
        /// @param tooltip 説明文（[Tooltip] の文）。空でもよい
        /// @param fieldName 元の変数名
        /// @return 名前と説明文
        public static GUIContent CreateLabel(string text, string tooltip, string fieldName)
        {
            string fieldNote = string.Format(FieldNameFormat, fieldName);
            string fullTooltip = string.IsNullOrEmpty(tooltip) ? fieldNote : tooltip + "\n" + fieldNote;
            return new GUIContent(text, fullTooltip);
        }

        /// @brief 欄を自分で描くときに使う名前と説明文を返す
        /// [InspectorLabel] があればその名前、なければ Unity のふつうの名前（変数名を読みやすくしたもの）
        /// @param property 描く項目。いちばん外側の変数だけに使える（配列の要素や、入れ子の中の項目は Unity のふつうの名前になる）
        /// @return 名前と説明文
        public static GUIContent GetLabel(SerializedProperty property)
        {
            FieldInfo field = FindField(property);
            InspectorLabelAttribute label = field?.GetCustomAttribute<InspectorLabelAttribute>();
            if (label == null)
            {
                return new GUIContent(property.displayName, property.tooltip);
            }
            return CreateLabel(label.Text, property.tooltip, field.Name);
        }

        /// @brief 項目の元になっている変数を探す
        /// @param property 探す項目
        /// @return 見つかった変数。いちばん外側の変数でないときや、見つからないときは null
        private static FieldInfo FindField(SerializedProperty property)
        {
            UnityEngine.Object target = property.serializedObject.targetObject;
            if (target == null || property.depth != 0)
            {
                return null;
            }
            for (Type type = target.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(property.name, FieldFlags);
                if (field != null)
                {
                    return field;
                }
            }
            return null;
        }
    }
}
