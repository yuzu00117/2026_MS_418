using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Echo.Echolocation.Editor
{
    /// <summary>保存済み個人シーンの旧ルールを参照型へ移す。表示と遮断設定を専用タグアセットに保存する。</summary>
    public static class EchoTagMigration
    {
        private const string TagFolder = "Assets/_Sandbox/Omata_N/Settings/EchoTags";
        [MenuItem("Tools/エコーロケーション/既存シーンの移行/旧Unityタグ設定を専用タグへ変換")]
        public static void MigrateActiveScene()
        {
            var scene = SceneManager.GetActiveScene();
            EchoController selected = null;
            foreach (var controller in UnityEngine.Object.FindObjectsByType<EchoController>(FindObjectsInactive.Include))
                if (controller.gameObject.scene == scene)
                {
                    if (selected) { Debug.LogError("Controllerが複数あります。個別に移行してください。"); return; }
                    selected = controller;
                }
            if (!selected) { Debug.LogError("現在のシーンにEchoControllerがありません。"); return; }
            Migrate(selected);
        }
        private static void ImportAssetSettings(EchoController controller, EchoTagColorRule rule)
        {
            if (rule == null || !rule.Definition || rule.Definition.HasAssetSettings) return;
            if (!AssetDatabase.GetAssetPath(rule.Definition).StartsWith("Assets/_Sandbox/Omata_N/")) return;
            Undo.RecordObject(rule.Definition, "専用タグの設定を移行");
            var settings = rule.Copy();
            settings.ImportLegacySettings(controller.Settings.OcclusionMode);
            rule.Definition.Configure(settings, controller.Settings.Shadow.IsBlockingTag(rule.TagName));
            EditorUtility.SetDirty(rule.Definition);
        }
        public static void Migrate(EchoController controller)
        {
            if (Application.isPlaying || !controller.gameObject.scene.path.StartsWith("Assets/_Sandbox/Omata_N/"))
                throw new InvalidOperationException("編集モードのOmata_N配下の保存済みシーンだけ移行できます。");
            EchoSurfaceBaker.EnsureFolder(TagFolder);
            Undo.IncrementCurrentGroup();
            int group = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Migrate echo classification");
            var definitions = new Dictionary<string, EchoTagDefinition>();
            Undo.RecordObject(controller, "Migrate echo rules");
            foreach (var rule in controller.Settings.TagColorRules)
            {
                if (rule == null) continue;
                if (!rule.Definition)
                {
                    if (!definitions.TryGetValue(rule.TagName, out var definition))
                    {
                        definition = ScriptableObject.CreateInstance<EchoTagDefinition>();
                        definition.name = "SO_EchoTag_" + rule.TagName;
                        string safeName = rule.TagName;
                        foreach (char invalid in System.IO.Path.GetInvalidFileNameChars()) safeName = safeName.Replace(invalid, '_');
                        AssetDatabase.CreateAsset(definition, AssetDatabase.GenerateUniqueAssetPath(TagFolder + "/SO_EchoTag_" + safeName + ".asset"));
                        definitions.Add(rule.TagName, definition);
                    }
                    rule.Definition = definition;
                }
                else if (!definitions.ContainsKey(rule.TagName)) definitions.Add(rule.TagName, rule.Definition);
            }
            foreach (var rule in controller.Settings.TagColorRules) ImportAssetSettings(controller, rule);
            var world = controller.GetComponent<EchoRevealWorld>();
            var owners = new HashSet<GameObject>();
            foreach (var renderer in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include))
            {
                if (renderer.gameObject.scene != controller.gameObject.scene || renderer.GetComponent<EchoRevealProxy>()) continue;
                var owner = renderer.GetComponentInParent<EchoTarget>(true);
                owners.Add(owner ? owner.gameObject : renderer.gameObject);
            }
            foreach (var owner in owners)
            {
                if (!definitions.TryGetValue(owner.tag, out var definition)) continue;
                EchoTagBatchWindow.Apply(new[] { owner }, definition, world, false);
            }
            PrefabUtility.RecordPrefabInstancePropertyModifications(controller);
            EditorSceneManager.MarkSceneDirty(controller.gameObject.scene);
            foreach (var definition in definitions.Values) AssetDatabase.SaveAssetIfDirty(definition);
            Undo.CollapseUndoOperations(group);
            controller.RefreshTargets();
        }
    }
}
