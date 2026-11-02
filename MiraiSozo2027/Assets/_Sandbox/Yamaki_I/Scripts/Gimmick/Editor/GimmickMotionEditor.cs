using UnityEditor;

namespace Aura.Gimmick.Editor
{
    /// @brief 動くギミック（GimmickMover、GimmickRotator）の Inspector。ふつうの Inspector と同じ順に全項目を出すが、
    /// 「端で止まる時間（秒）」は、「動き方」が「起動中は往復する」のときだけ出す
    public abstract class GimmickMotionEditor : UnityEditor.Editor
    {
        private const string ScriptPropertyPath = "m_Script";
        private const string MotionModePropertyPath = "_motionMode";
        private const string EndWaitSecondsPropertyPath = "_endWaitSeconds";

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            // 複数を選んで値が違うときは、往復する物もあるかもしれないので出す
            SerializedProperty motionMode = serializedObject.FindProperty(MotionModePropertyPath);
            bool isPingPong = motionMode.hasMultipleDifferentValues || motionMode.intValue == (int)GimmickMotionMode.PingPong;

            SerializedProperty property = serializedObject.GetIterator();
            bool isEnteringChildren = true;
            while (property.NextVisible(isEnteringChildren))
            {
                isEnteringChildren = false;
                if (property.propertyPath == EndWaitSecondsPropertyPath && !isPingPong)
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

    /// @brief GimmickMover の Inspector（中身は GimmickMotionEditor）
    [CustomEditor(typeof(GimmickMover))]
    [CanEditMultipleObjects]
    public sealed class GimmickMoverEditor : GimmickMotionEditor
    {
    }

    /// @brief GimmickRotator の Inspector（中身は GimmickMotionEditor）
    [CustomEditor(typeof(GimmickRotator))]
    [CanEditMultipleObjects]
    public sealed class GimmickRotatorEditor : GimmickMotionEditor
    {
    }
}
