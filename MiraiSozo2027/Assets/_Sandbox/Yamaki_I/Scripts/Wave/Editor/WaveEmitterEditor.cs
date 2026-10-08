using UnityEditor;

namespace Aura.Wave.Editor
{
    /// @brief WaveEmitter の Inspector。ふつうの Inspector と同じ順に全項目を出すが、
    /// 反射のときだけ使う項目（予測線の ON / OFF）は、Reflection Mode が None でないときだけ出す
    [CustomEditor(typeof(WaveEmitter))]
    [CanEditMultipleObjects]
    public sealed class WaveEmitterEditor : UnityEditor.Editor
    {
        private const string ScriptPropertyPath = "m_Script";
        private const string ReflectionModePropertyPath = "_reflectionMode";
        private const string ReflectionPreviewPropertyPath = "_isReflectionPreviewEnabled";

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            bool isReflecting = IsReflecting(serializedObject);

            SerializedProperty property = serializedObject.GetIterator();
            bool isEnteringChildren = true;
            while (property.NextVisible(isEnteringChildren))
            {
                isEnteringChildren = false;
                if (!IsPropertyVisible(property.propertyPath, isReflecting))
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

        /// @brief 反射する設定か（Reflection Mode が None でないか）。複数を選んで値が違うときは、反射する物があるので true
        /// @param serializedObject WaveEmitter の SerializedObject
        /// @return 反射する設定なら true
        public static bool IsReflecting(SerializedObject serializedObject)
        {
            SerializedProperty reflectionMode = serializedObject.FindProperty(ReflectionModePropertyPath);
            return reflectionMode.hasMultipleDifferentValues
                || reflectionMode.enumValueIndex != (int)WaveReflectionMode.None;
        }

        /// @brief その項目を Inspector に出すか。予測線の ON / OFF は、反射する設定のときだけ出す
        /// @param propertyPath 項目の SerializedProperty.propertyPath
        /// @param isReflecting 反射する設定か
        /// @return 出すなら true
        public static bool IsPropertyVisible(string propertyPath, bool isReflecting)
        {
            return isReflecting || propertyPath != ReflectionPreviewPropertyPath;
        }
    }
}
