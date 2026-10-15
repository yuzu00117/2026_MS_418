using UnityEditor;
using UnityEditor.SceneManagement;

namespace LoadingTest.B.Editor
{
    /// <summary>
    /// 方式B用: どのシーンを開いていても Boot から再生する。
    /// リポジトリ共有のため、他メンバーに影響しないよう既定は OFF(メニューで切り替える)。
    /// </summary>
    [InitializeOnLoad]
    public static class PlayFromBoot
    {
        private const string BootScenePath = "Assets/_Sandbox/Miyata_M/Scenes/LoadingTest/Boot.unity"; // 環境に合わせて変更
        private const string MenuPath = "Tools/LoadingTest/Play From Boot";
        private const string PrefsKey = "LoadingTest.PlayFromBoot";

        static PlayFromBoot()
        {
            Apply();
        }

        [MenuItem(MenuPath)]
        private static void Toggle()
        {
            EditorPrefs.SetBool(PrefsKey, !EditorPrefs.GetBool(PrefsKey, false));
            Apply();
        }

        [MenuItem(MenuPath, true)]
        private static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, EditorPrefs.GetBool(PrefsKey, false));
            return true;
        }

        private static void Apply()
        {
            // OFF のときは null に戻し、通常の「開いているシーンから再生」にする
            SceneAsset boot = EditorPrefs.GetBool(PrefsKey, false)
                ? AssetDatabase.LoadAssetAtPath<SceneAsset>(BootScenePath)
                : null;
            EditorSceneManager.playModeStartScene = boot;
        }
    }
}
