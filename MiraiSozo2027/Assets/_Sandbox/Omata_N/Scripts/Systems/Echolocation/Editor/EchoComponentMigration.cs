using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Echo.Echolocation.Editor
{
    /// <summary>旧EchoTag・EchoRangeDisplay・EchoRevealWorldの値をEchoTarget・EchoControllerへ移し、旧コンポーネントを外す。</summary>
    public static class EchoComponentMigration
    {
        private const string Root = "Assets/_Sandbox/Omata_N/";
        [MenuItem("Tools/エコーロケーション/既存シーンの移行/コンポーネントを統合（開いているシーン）")]
        public static void MigrateOpenScenes()
        {
            if (Application.isPlaying) { Debug.LogError("再生を停止してから実行してください。"); return; }
            int count = 0;
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                var scene = SceneManager.GetSceneAt(i);
                // 他メンバーの共有シーンは担当者の判断で移行するため、個人フォルダ配下だけを対象にする。
                if (!scene.isLoaded || !scene.path.StartsWith(Root)) continue;
                count += Migrate(scene);
            }
            Debug.Log($"エコー：{count}個の旧コンポーネントを統合しました。シーンを保存するまでファイルには反映されません。");
        }

        [MenuItem("Tools/エコーロケーション/既存シーンの移行/コンポーネントを統合して保存（Omata_Nの全シーン）")]
        public static void MigrateAllScenes()
        {
            if (Application.isPlaying) { Debug.LogError("再生を停止してから実行してください。"); return; }
            // 各シーンを開き直すため、未保存の変更は先に保存するか確認する。
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var setup = EditorSceneManager.GetSceneManagerSetup();
            int total = 0, changedScenes = 0;
            try
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { Root.TrimEnd('/') }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    int count = Migrate(scene);
                    if (count == 0) continue;
                    EditorSceneManager.SaveScene(scene);
                    total += count;
                    changedScenes++;
                }
            }
            finally
            {
                if (setup.Length > 0) EditorSceneManager.RestoreSceneManagerSetup(setup);
            }
            Debug.Log($"エコー：{changedScenes}個のシーンで{total}個の旧コンポーネントを統合して保存しました。");
        }

        public static int Migrate(Scene scene)
        {
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Merge echo components");
            int count = 0;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var tag in root.GetComponentsInChildren<EchoTag>(true))
                {
                    if (!CanRemove(tag)) continue;
                    var target = tag.GetComponent<EchoTarget>();
                    Undo.RecordObject(target, "Merge echo tag");
                    // 無効化していた旧タグは表示しない指定なので、専用タグを移さない。
                    if (!target.OwnDefinition && tag.enabled) target.Definition = tag.Definition;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(target);
                    Undo.DestroyObjectImmediate(tag);
                    target.ForgetLegacyTag();
                    count++;
                }
                foreach (var display in root.GetComponentsInChildren<EchoRangeDisplay>(true))
                {
                    if (!CanRemove(display)) continue;
                    var controller = display.GetComponent<EchoController>();
                    Undo.RecordObject(controller, "Merge echo range display");
                    display.CopyTo(controller.RangeDisplay);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
                    Undo.DestroyObjectImmediate(display);
                    count++;
                }
                foreach (var world in root.GetComponentsInChildren<EchoRevealWorld>(true))
                {
                    if (!CanRemove(world)) continue;
                    Undo.DestroyObjectImmediate(world);
                    count++;
                }
            }
            if (count > 0) EditorSceneManager.MarkSceneDirty(scene);
            Undo.CollapseUndoOperations(group);
            return count;
        }

        // Prefab側に保存された旧コンポーネントはシーンから外せない。Prefabを開いて移行する。
        public static bool IsRemovable(Component component) =>
            !PrefabUtility.IsPartOfPrefabInstance(component) || PrefabUtility.IsAddedComponentOverride(component);

        private static bool CanRemove(Component component)
        {
            if (IsRemovable(component)) return true;
            Debug.LogWarning($"エコー：{component.name} の {component.GetType().Name} はPrefab側にあるため外せません。再生中は値を引き継ぐので動作は変わりません。Prefabの担当者と相談して移行してください。", component);
            return false;
        }

        /// <summary>旧コンポーネントのInspectorに表示する移行ボタン。</summary>
        public static void DrawLegacyNotice(string message)
        {
            EditorGUILayout.HelpBox(message + "\n下のボタンでOmata_Nの全シーンの値を移して削除し、保存します。共有Prefab側のものは外せません。", MessageType.Warning);
            using (new EditorGUI.DisabledScope(Application.isPlaying))
                if (GUILayout.Button("Omata_Nの全シーンを統合して保存"))
                {
                    MigrateAllScenes();
                    // シーンを開き直すとこのInspectorの対象が破棄されるため、描画を打ち切る。
                    GUIUtility.ExitGUI();
                }
        }
    }
}
