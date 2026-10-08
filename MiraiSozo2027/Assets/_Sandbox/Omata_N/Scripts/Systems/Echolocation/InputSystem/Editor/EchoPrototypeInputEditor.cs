// 入力の一覧編集と実行状態を日本語で表示する。
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Echo.Echolocation.Editor
{
    [CustomEditor(typeof(EchoPrototypeInput)), CanEditMultipleObjects]
    public sealed class EchoPrototypeInputEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("登録したいずれかの操作を押すたびにON/OFFが切り替わります。複数の同時押しは1回として扱います。\n例：Eキー＋マウス右ボタン＋ゲームパッド下側ボタン", MessageType.Info);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_controller"), new GUIContent("操作先"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_bindings"), new GUIContent("切り替え操作の割り当て"), true);
            if (serializedObject.ApplyModifiedProperties())
                foreach (var t in targets)
                    ((EchoPrototypeInput)t).RebuildBindings();
            if (UnityEngine.InputSystem.InputSystem.settings.updateMode != InputSettings.UpdateMode.ProcessEventsInDynamicUpdate)
                EditorGUILayout.HelpBox("Input SystemのUpdate ModeをDynamic Updateへ変更してください。", MessageType.Error);
            if (Application.isPlaying)
            {
                var input = (EchoPrototypeInput)target;
                EditorGUILayout.LabelField("入力状態", input.Status);
                EditorGUILayout.LabelField("最後の操作", input.LastInput);
                Repaint();
            }
        }
    }
}
