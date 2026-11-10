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
            EditorGUILayout.HelpBox("エコー対象にはこのコンポーネントだけを付けます。表示と遮断の設定は専用タグアセットで編集します。子に別のEchoTargetがある場合は子側が優先されます。", MessageType.Info);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_definition"), new GUIContent("専用タグ"));
            foreach (var selected in targets)
            {
                var echoTarget = (EchoTarget)selected;
                if (!echoTarget.LegacyTag) continue;
                string inherited = echoTarget.OwnDefinition ? "" : "専用タグが未設定のため、旧EchoTagの指定を使用しています。";
                EchoComponentMigration.DrawLegacyNotice($"{echoTarget.name} に旧EchoTagが残っています。{inherited}");
                break;
            }
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
                foreach (var selected in targets)
                    ((EchoTarget)selected).RefreshRenderers();
            DrawRuntimeState();
        }

        // 実体化の進み具合は実行中にしか変わらないため、再生中だけ毎フレーム描き直す。
        public override bool RequiresConstantRepaint() => Application.isPlaying;

        private void DrawRuntimeState()
        {
            if (!Application.isPlaying || targets.Length != 1) return;
            var echoTarget = (EchoTarget)target;
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("実行中の状態", EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.EnumPopup("状態", echoTarget.State);
            var rect = EditorGUILayout.GetControlRect();
            EditorGUI.ProgressBar(rect, echoTarget.MaterializationProgress, $"実体化 {echoTarget.MaterializationProgress:P0}（照射 {echoTarget.IrradiationSeconds:F2}秒）");
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("実体化させる")) echoTarget.Materialize();
                if (GUILayout.Button("透明に戻す")) echoTarget.ResetToTransparent();
            }
        }
    }
}
