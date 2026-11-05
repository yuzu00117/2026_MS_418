using UnityEditor;

namespace Aura.ObjectInfo.Editor
{
    /// @brief Concealment の Inspector。ふつうの Inspector と同じ順に全項目を出すが、
    /// 化けている見た目（Disguise Form）は、Style が Disguised のときだけ出す
    [CustomEditor(typeof(Concealment))]
    [CanEditMultipleObjects]
    public sealed class ConcealmentEditor : UnityEditor.Editor
    {
        private const string ScriptPropertyPath = "m_Script";
        private const string StylePropertyPath = "_style";
        private const string DisguiseFormPropertyPath = "_disguiseForm";

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            bool isDisguised = IsDisguised(serializedObject);

            SerializedProperty property = serializedObject.GetIterator();
            bool isEnteringChildren = true;
            while (property.NextVisible(isEnteringChildren))
            {
                isEnteringChildren = false;
                if (!IsPropertyVisible(property.propertyPath, isDisguised))
                {
                    continue;
                }
                // スクリプトの欄は、ふつうの Inspector と同じく変えられないようにする
                using (new EditorGUI.DisabledScope(property.propertyPath == ScriptPropertyPath))
                {
                    EditorGUILayout.PropertyField(property, true);
                }
            }
            serializedObject.ApplyModifiedProperties();
        }

        /// @brief 化けている設定か（Style が Disguised か）。複数を選んで値が違うときは、化けている物があるので true
        /// @param serializedObject Concealment の SerializedObject
        /// @return 化けている設定なら true
        public static bool IsDisguised(SerializedObject serializedObject)
        {
            SerializedProperty style = serializedObject.FindProperty(StylePropertyPath);
            return style.hasMultipleDifferentValues
                || style.intValue == (int)ConcealmentStyle.Disguised;
        }

        /// @brief その項目を Inspector に出すか。化けている見た目は、化けている設定のときだけ出す
        /// @param propertyPath 項目の SerializedProperty.propertyPath
        /// @param isDisguised 化けている設定か
        /// @return 出すなら true
        public static bool IsPropertyVisible(string propertyPath, bool isDisguised)
        {
            return isDisguised || propertyPath != DisguiseFormPropertyPath;
        }
    }
}
