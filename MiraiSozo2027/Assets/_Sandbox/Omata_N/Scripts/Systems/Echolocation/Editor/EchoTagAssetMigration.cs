using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace Echo.Echolocation.Editor
{
    public static class EchoTagAssetMigration
    {
        private const string Root = "Assets/_Sandbox/Omata_N/";
        public static void MigrateDemos()
        {
            MigrateScene(Root + "Scenes/Scene_EchoDemo.unity");
            EchoDemoSceneBuilder.CreateAssetDemo();
        }
        private static void MigrateScene(string path)
        {
            var scene = EditorSceneManager.OpenScene(path);
            foreach (var root in scene.GetRootGameObjects())
                foreach (var controller in root.GetComponentsInChildren<EchoController>(true))
                    foreach (var legacy in controller.Settings.TagColorRules) Import(controller, legacy);
        }
        private static void Import(EchoController controller, EchoTagColorRule legacy)
        {
            if (legacy == null || !legacy.Definition || legacy.Definition.HasAssetSettings) return;
            var tag = legacy.Definition;
            if (!AssetDatabase.GetAssetPath(tag).StartsWith(Root, StringComparison.Ordinal)) return;
            var settings = legacy.Copy(); settings.ImportLegacySettings(controller.Settings.OcclusionMode);
            bool isBlocking = controller.Settings.Shadow.IsBlockingDefinition(tag);
            tag.Configure(settings, isBlocking);
            EditorUtility.SetDirty(tag); AssetDatabase.SaveAssetIfDirty(tag);
        }
    }
}
