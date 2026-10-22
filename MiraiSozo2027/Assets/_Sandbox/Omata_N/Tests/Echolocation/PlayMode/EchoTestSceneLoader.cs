using System;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Echo.Echolocation.Tests
{
    internal static class EchoTestSceneLoader
    {
        public static AsyncOperation Load(string name)
        {
#if UNITY_EDITOR
            return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                "Assets/_Sandbox/Omata_N/Scenes/" + name + ".unity", new LoadSceneParameters(LoadSceneMode.Additive));
#else
            return SceneManager.LoadSceneAsync(name, LoadSceneMode.Additive);
#endif
        }
        public static void StopTeamInput()
        {
            // 共有コードには依存を追加せず、シーンテスト終了時だけInputActionを停止する。
            var manager = Type.GetType("GameManager, Assembly-CSharp");
            var actions = manager?.GetProperty("Actions")?.GetValue(null);
            actions?.GetType().GetMethod("Disable")?.Invoke(actions, null);
            Time.timeScale = 1;
        }
    }
}
