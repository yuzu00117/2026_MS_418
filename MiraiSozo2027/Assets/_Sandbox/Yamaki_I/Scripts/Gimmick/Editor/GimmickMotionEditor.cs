using UnityEditor;

namespace Aura.Gimmick.Editor
{
    /// @brief 動くギミック（GimmickMotion を継承した GimmickMover、GimmickRotator）の Inspector
    /// 継承先の項目（動く量、速さなど）を先に、親の GimmickMotion の項目（動き方など）を後に出す
    /// （Unity は親の項目を先に並べるので、そのままだと「動き方」が「動く量」より上に来てしまう）
    /// 「端で止まる時間（秒）」は、「動き方」が「起動中は往復する」のときだけ出す
    [CustomEditor(typeof(GimmickMotion), true)]
    [CanEditMultipleObjects]
    public sealed class GimmickMotionEditor : UnityEditor.Editor
    {
        private const string ScriptPropertyPath = "m_Script";
        private const string MotionModePropertyPath = "_motionMode";
        private const string EndWaitSecondsPropertyPath = "_endWaitSeconds";
        private const string IsActiveOnStartPropertyPath = "_isActiveOnStart";

        /// @brief 親の GimmickMotion の項目。この順に、継承先の項目の後に出す
        private static readonly string[] MotionPropertyPaths =
        {
            MotionModePropertyPath,
            EndWaitSecondsPropertyPath,
            IsActiveOnStartPropertyPath,
        };

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            // 複数を選んで値が違うときは、往復する物もあるかもしれないので出す
            SerializedProperty motionMode = serializedObject.FindProperty(MotionModePropertyPath);
            bool isPingPong = motionMode.hasMultipleDifferentValues || motionMode.intValue == (int)GimmickMotionMode.PingPong;

            // 継承先の項目（親の項目以外）
            SerializedProperty property = serializedObject.GetIterator();
            bool isEnteringChildren = true;
            while (property.NextVisible(isEnteringChildren))
            {
                isEnteringChildren = false;
                if (System.Array.IndexOf(MotionPropertyPaths, property.propertyPath) >= 0)
                {
                    continue;
                }
                // スクリプトの欄は、ふつうの Inspector と同じく変えられないようにする
                using (new EditorGUI.DisabledScope(property.propertyPath == ScriptPropertyPath))
                {
                    EditorGUILayout.PropertyField(property, true);
                }
            }

            // 親の項目
            foreach (string path in MotionPropertyPaths)
            {
                if (path == EndWaitSecondsPropertyPath && !isPingPong)
                {
                    continue;
                }
                EditorGUILayout.PropertyField(serializedObject.FindProperty(path), true);
            }
            serializedObject.ApplyModifiedProperties();
        }
    }
}
