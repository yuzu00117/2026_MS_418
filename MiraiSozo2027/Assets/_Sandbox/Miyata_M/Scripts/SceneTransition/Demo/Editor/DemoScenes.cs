namespace SceneFlow.EditorTools
{
    /// <summary>動作確認用シーンの名前と置き場所。Setup と Verify で共有する。</summary>
    internal static class DemoScenes
    {
        public const string Directory = "Assets/_Sandbox/Miyata_M/Scenes/SceneTransition";
        public const string TitleName = "Scene_Title";
        public const string StageSelectName = "Scene_StageSelect";
        public const string GameName = "Scene_Game";

        public static string PathOf(string sceneName)
        {
            return $"{Directory}/{sceneName}.unity";
        }
    }
}
