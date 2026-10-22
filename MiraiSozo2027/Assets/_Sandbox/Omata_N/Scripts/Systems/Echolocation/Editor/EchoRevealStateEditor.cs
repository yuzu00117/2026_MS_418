using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace Echo.Echolocation.Editor
{
    [CustomEditor(typeof(EchoRevealState))]
    public sealed class EchoRevealStateEditor : UnityEditor.Editor
    {
        private float _maxEdge = 1;
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_world"), new GUIContent("表示状態の管理元"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("_surfaces"), new GUIContent("Rendererごとの分割データ"), true);
            serializedObject.ApplyModifiedProperties();
            var state = (EchoRevealState)target;
            var owner = state.GetComponent<EchoTarget>();
            foreach (var renderer in state.GetComponentsInChildren<Renderer>(true))
            {
                if (!owner.Owns(renderer) || renderer.GetComponent<EchoRevealProxy>()) continue;
                bool hasBinding = false;
                foreach (var binding in state.Surfaces)
                    hasBinding |= binding != null && binding.Renderer == renderer && binding.Data;
                if (!hasBinding)
                    EditorGUILayout.HelpBox($"「{renderer.name}」は未ベイクです。接触部分表示を使用する場合は、再生を停止して下の「所有Rendererを分割ベイク」を実行してください。全体表示では不要です。", MessageType.Warning);
            }
            foreach (var binding in state.Surfaces)
                if (!EchoSurfaceBuildValidator.IsCurrent(binding))
                {
                    EditorGUILayout.HelpBox("Meshまたはスケールと分割データが一致しません。再ベイクしてください。", MessageType.Error);
                    break;
                }
            EditorGUILayout.HelpBox("全体表示はベイク不要。接触部分表示は静的な不透明メッシュ専用です。最大辺長が表示と衝突の精度です。状態はカメラから見えるかには依存しません。", MessageType.Info);
            _maxEdge = EditorGUILayout.FloatField("分割の最大辺長（m）", _maxEdge);
            using (new EditorGUI.DisabledScope(Application.isPlaying || !state.World))
                if (GUILayout.Button("所有Rendererを分割ベイク"))
                {
                    if (!state.gameObject.scene.path.StartsWith("Assets/_Sandbox/Omata_N/"))
                    { Debug.LogError("Omata_N配下の保存済みシーンで実行してください。", state); return; }
                    try
                    {
                        var bindings = EchoSurfaceBaker.Bake(state.GetComponent<EchoTarget>(), _maxEdge);
                        Undo.RecordObject(state, "Bake echo surface bindings");
                        state.Configure(state.World, bindings);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(state);
                        EditorSceneManager.MarkSceneDirty(state.gameObject.scene);
                    }
                    catch (System.Exception exception) { Debug.LogError(exception.Message, state); }
                }
            if (Application.isPlaying)
                EditorGUILayout.LabelField($"表示中: {state.IsActive} / 衝突有効: {state.HasActiveCollision} / 判定稼働中: {state.IsOperational}\n表示割合: {state.RevealedFraction:P1} / 更新番号: {state.Revision}");
        }
    }
}

