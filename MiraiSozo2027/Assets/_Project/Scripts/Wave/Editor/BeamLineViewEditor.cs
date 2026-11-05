using UnityEditor;

namespace Aura.Wave.Editor
{
    /// @brief BeamLineView の Inspector。ふつうの Inspector と同じ順に全項目を出すが、
    /// 「触れた後も見せる秒数」は、「見せ方」が「範囲に触れたら全体を見せる」のときだけ出す
    [CustomEditor(typeof(BeamLineView))]
    [CanEditMultipleObjects]
    public sealed class BeamLineViewEditor : UnityEditor.Editor
    {
        private const string ScriptPropertyPath = "m_Script";
        private const string VisibilityPropertyPath = "_visibility";
        private const string RevealHoldSecondsPropertyPath = "_revealHoldSeconds";

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            bool isWholeWhenTouched = WaveEmitterEditor.HasEnumValue(serializedObject, VisibilityPropertyPath, (int)BeamLineVisibility.WholeWhenTouched);

            SerializedProperty property = serializedObject.GetIterator();
            bool isEnteringChildren = true;
            while (property.NextVisible(isEnteringChildren))
            {
                isEnteringChildren = false;
                if (property.propertyPath == RevealHoldSecondsPropertyPath && !isWholeWhenTouched)
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
    }
}
