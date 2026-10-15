using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LoadingTest.EditorTools
{
    /// <summary>
    /// 3方式を実際に Play して遷移まで確認する自己診断。
    /// バッチ: -executeMethod LoadingTest.EditorTools.LoadingTestVerify.Run (-quit は付けない)
    /// 終了コード 0=全方式OK / 1=失敗。Play 開始でドメインが再読み込みされるため進行状況は SessionState に持つ。
    /// </summary>
    [InitializeOnLoad]
    public static class LoadingTestVerify
    {
        private const string SceneDir = "Assets/_Sandbox/Miyata_M/Scenes/LoadingTest/";
        private const string KeyRun = "LoadingTestVerify.Run";
        private const string KeyCase = "LoadingTestVerify.Case";
        private const string KeyStage = "LoadingTestVerify.Stage";
        private const string KeyDeadline = "LoadingTestVerify.Deadline";
        private const string KeyFailed = "LoadingTestVerify.Failed";
        private const double TimeoutSeconds = 30.0;
        private const int CaseCount = 3;
        private const string Tag = "LTVERIFY: ";

        static LoadingTestVerify()
        {
            if (SessionState.GetBool(KeyRun, false)) EditorApplication.update += Tick;
        }

        public static void Run()
        {
            SessionState.SetBool(KeyRun, true);
            SessionState.SetInt(KeyCase, 0);
            SessionState.SetInt(KeyStage, 0);
            SessionState.SetBool(KeyFailed, false);
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            int c = SessionState.GetInt(KeyCase, 0);
            int s = SessionState.GetInt(KeyStage, 0);

            if (c >= CaseCount)
            {
                Finish();
                return;
            }
            if (s > 0 && s < 90 && EditorApplication.timeSinceStartup > SessionState.GetFloat(KeyDeadline, float.MaxValue))
            {
                Fail($"case{c} stage{s} timeout");
                return;
            }

            switch (s)
            {
                case 0: // シーンを開いて Play 開始
                    EditorSceneManager.OpenScene(SceneDir + StartScene(c) + ".unity");
                    SessionState.SetFloat(KeyDeadline, (float)(EditorApplication.timeSinceStartup + TimeoutSeconds));
                    EditorApplication.isPlaying = true;
                    SessionState.SetInt(KeyStage, 1);
                    break;

                case 1: // Play 中になったら遷移を要求
                    if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
                    if (!RequestTransition(c)) return;
                    SessionState.SetInt(KeyStage, 2);
                    break;

                case 2: // 遷移完了を待つ
                    if (!IsTransitionDone(c, out string error))
                    {
                        if (error != null) Fail(error);
                        return;
                    }
                    Debug.Log(Tag + $"case{c} OK");
                    EditorApplication.isPlaying = false;
                    SessionState.SetInt(KeyStage, 90);
                    break;

                case 90: // Play 終了を待って次の方式へ
                    if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
                    SessionState.SetInt(KeyCase, c + 1);
                    SessionState.SetInt(KeyStage, 0);
                    break;
            }
        }

        private static string StartScene(int c)
        {
            return c == 0 ? "LT_A_Start" : c == 1 ? "LT_B_Boot" : "LT_C_Start";
        }

        private static bool RequestTransition(int c)
        {
            switch (c)
            {
                case 0:
                    var a = Object.FindFirstObjectByType<LoadingTest.A.LoadingScreen>(FindObjectsInactive.Include);
                    if (a == null) return false;
                    a.LoadScene("LT_A_Next");
                    return true;
                case 1:
                    // Boot が起動時に Title を読み込み終えるまで待つ。Title 到着後に Game へ遷移する
                    if (SceneManager.GetActiveScene().name != "LT_B_Title") return false;
                    var b = Object.FindFirstObjectByType<LoadingTest.B.AdditiveSceneLoader>();
                    if (b == null) return false;
                    // 初回ロードのフェードアウト完了前に呼ぶと isLoading で無視されるため待つ
                    if (b.GetComponent<CanvasGroup>().blocksRaycasts) return false;
                    b.Load("LT_B_Game");
                    return true;
                default:
                    if (Object.FindFirstObjectByType<LoadingTest.C.SceneLoader>() == null) return false;
                    LoadingTest.C.SceneLoader.Load("LT_C_Next");
                    return true;
            }
        }

        private static bool IsTransitionDone(int c, out string error)
        {
            error = null;
            switch (c)
            {
                case 0:
                    return SceneManager.GetActiveScene().name == "LT_A_Next";
                case 1:
                    if (SceneManager.GetActiveScene().name != "LT_B_Game") return false;
                    var group = Object.FindFirstObjectByType<LoadingTest.B.AdditiveSceneLoader>()
                        .GetComponent<CanvasGroup>();
                    if (group.alpha > 0f) return false; // フェードアウト完了まで待つ
                    // Boot + Game のみ残り、旧 Title は破棄されていること
                    if (SceneManager.GetSceneByName("LT_B_Title").isLoaded) error = "Title が残っている";
                    else if (SceneManager.sceneCount != 2) error = $"sceneCount={SceneManager.sceneCount}";
                    return error == null;
                default:
                    if (SceneManager.GetActiveScene().name != "LT_C_Next") return false;
                    var loader = Object.FindFirstObjectByType<LoadingTest.C.SceneLoader>();
                    if (loader == null) { error = "SceneLoader が遷移後に消えた"; return false; }
                    return loader.GetComponent<Transform>().GetComponentInChildren<CanvasGroup>().alpha <= 0f;
            }
        }

        private static void Fail(string message)
        {
            Debug.LogError(Tag + "FAIL " + message);
            SessionState.SetBool(KeyFailed, true);
            Finish();
        }

        private static void Finish()
        {
            bool failed = SessionState.GetBool(KeyFailed, false);
            SessionState.SetBool(KeyRun, false);
            EditorApplication.update -= Tick;
            Debug.Log(Tag + (failed ? "RESULT FAILED" : "RESULT ALL OK"));
            EditorApplication.Exit(failed ? 1 : 0);
        }
    }
}
