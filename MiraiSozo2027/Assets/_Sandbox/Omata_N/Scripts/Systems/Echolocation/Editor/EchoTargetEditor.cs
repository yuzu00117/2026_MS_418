// 親タグでまとめる対象の設定画面。手動所有範囲を検証する。
using UnityEditor;
using UnityEngine;

namespace Echo.Echolocation.Editor
{
    [CustomEditor(typeof(EchoTarget)), CanEditMultipleObjects]
    public sealed class EchoTargetEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.HelpBox("このGameObjectのタグとレイヤーを子Rendererに使用します。子に別のEchoTargetがある場合は子側が優先されます。", MessageType.Info);
            var manual = serializedObject.FindProperty("_isManualRendererSelectionEnabled");
            EditorGUILayout.PropertyField(manual, new GUIContent("Rendererを手動指定"));
            if (manual.boolValue)
            {
                var list = serializedObject.FindProperty("_renderers");
                EditorGUILayout.PropertyField(list, new GUIContent("対象Renderer"), true);
                var seenItems = new System.Collections.Generic.HashSet<Renderer>();
                for (int i = 0; i < list.arraySize; i++)
                {
                    var renderer = list.GetArrayElementAtIndex(i).objectReferenceValue as Renderer;
                    if (!renderer || !seenItems.Add(renderer) || renderer.GetComponentInParent<EchoTarget>(true) != (EchoTarget)target)
                        EditorGUILayout.HelpBox($"行{i + 1}：自身または子のRendererを重複なく指定してください。子EchoTargetの所有物は指定できません。", MessageType.Error);
                }
            }

            serializedObject.ApplyModifiedProperties();
            if (GUILayout.Button("子Rendererの登録を更新"))
                foreach (var t in targets)
                    ((EchoTarget)t).RefreshRenderers();
        }
    }
}
