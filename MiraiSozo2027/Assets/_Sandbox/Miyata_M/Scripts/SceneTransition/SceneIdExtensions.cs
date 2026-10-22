using System;

namespace SceneFlow
{
    /// <summary>SceneId からシーン名への対応表。シーン名の文字列はここだけで管理する。</summary>
    public static class SceneIdExtensions
    {
        private const string TitleSceneName = "Scene_Title";
        private const string StageSelectSceneName = "Scene_StageSelect";
        private const string GameSceneName = "Scene_Game";

        public static string ToSceneName(this SceneId id)
        {
            switch (id)
            {
                case SceneId.Title: return TitleSceneName;
                case SceneId.StageSelect: return StageSelectSceneName;
                case SceneId.Game: return GameSceneName;
                default: throw new ArgumentOutOfRangeException(nameof(id), id, "未対応の SceneId");
            }
        }
    }
}
