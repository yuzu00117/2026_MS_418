using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SceneFlow.EditorTools
{
    /// <summary>
    /// Title → StageSelect → Game → Title を、画面のボタンを押して実際に遷移させて確認する自己診断。
    /// バッチ: -executeMethod SceneFlow.EditorTools.SceneTransitionVerify.Run (-quit は付けない)
    /// 終了コード 0=OK / 1=失敗。Play 開始でドメインが再読み込みされるため進行状況は SessionState に持つ。
    /// </summary>
    [InitializeOnLoad]
    public static class SceneTransitionVerify
    {
        private const string SceneDir = "Assets/_Sandbox/Miyata_M/Scenes/SceneTransition/";
        private const string KeyRun = "SceneTransitionVerify.Run";
        private const string KeyStage = "SceneTransitionVerify.Stage";
        private const string KeyDeadline = "SceneTransitionVerify.Deadline";
        private const double TimeoutSeconds = 30.0;
        private const string Tag = "STVERIFY: ";
        private const string TitleName = "Scene_Title";
        private const string StageSelectName = "Scene_StageSelect";
        private const string GameName = "Scene_Game";

        // 遷移の順序。現在のシーン上の該当ボタンを押し、そのシーンがアクティブになるまで待つ
        private static readonly string[] Sequence = { StageSelectName, GameName, TitleName };

        // Play 中のみ有効(ドメインは Play 開始時に再読み込みされ、Play 中は維持される)
        private static int _index;
        private static int _beforeCount;
        private static int _afterCount;

        static SceneTransitionVerify()
        {
            if (SessionState.GetBool(KeyRun, false)) EditorApplication.update += Tick;
        }

        public static void Run()
        {
            SessionState.SetBool(KeyRun, true);
            SessionState.SetInt(KeyStage, 0);
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            int stage = SessionState.GetInt(KeyStage, 0);
            if (stage > 0 && EditorApplication.timeSinceStartup > SessionState.GetFloat(KeyDeadline, float.MaxValue))
            {
                Finish(false, $"stage{stage} index{_index} timeout");
                return;
            }

            switch (stage)
            {
                case 0: // Title を開いて Play 開始
                    EditorSceneManager.OpenScene(SceneDir + TitleName + ".unity");
                    ResetDeadline();
                    EditorApplication.isPlaying = true;
                    SessionState.SetInt(KeyStage, 1);
                    break;

                case 1: // ロード画面(SceneLoader)が生成・登録され Title が出たら、最初の遷移を要求する
                    if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
                    if (!SceneTransition.HasLoader) return;
                    if (SceneManager.GetActiveScene().name != TitleName) return;

                    _index = 0;
                    _beforeCount = 0;
                    _afterCount = 0;
                    SceneTransition.OnBeforeTransition += _ => _beforeCount++;
                    SceneTransition.OnAfterTransition += _ => _afterCount++;
                    RequestClick(Sequence[_index], true);
                    SessionState.SetInt(KeyStage, 2);
                    break;

                case 2: // 遷移の完了(シーン到着 + ロード画面が消える)を待つ
                    if (SceneTransition.IsTransitioning) return;
                    if (SceneManager.GetActiveScene().name != Sequence[_index]) return;

                    Debug.Log(Tag + $"{Sequence[_index]} OK");
                    _index++;
                    if (_index < Sequence.Length)
                    {
                        RequestClick(Sequence[_index], false);
                        return;
                    }
                    // 二重呼び出しは無視されるため、通知は遷移回数ちょうどになる
                    if (_beforeCount != Sequence.Length || _afterCount != Sequence.Length)
                    {
                        Finish(false, $"通知回数が不正 before={_beforeCount} after={_afterCount}");
                        return;
                    }
                    EditorApplication.isPlaying = false;
                    SessionState.SetInt(KeyStage, 90);
                    break;

                case 90: // Play 終了を待って完了
                    if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
                    Finish(true, "ALL OK");
                    break;
            }
        }

        // 現在のシーンから目的のシーンへ遷移する要素を探して実行する。
        // SceneTransitionTrigger(イベント用)があればそれを優先し、無ければボタンを実際に押す
        private static void RequestClick(string targetScene, bool alsoDouble)
        {
            ResetDeadline();
            foreach (var t in Object.FindObjectsByType<SceneTransitionTrigger>())
            {
                if (t.Target.SceneName != targetScene) continue;
                Debug.Log(Tag + $"{targetScene} へ Trigger で遷移");
                t.Go();
                if (alsoDouble) t.Go();
                return;
            }
            foreach (var b in Object.FindObjectsByType<SceneTransitionButton>())
            {
                if (b.Target.SceneName != targetScene) continue;
                var button = b.GetComponent<UnityEngine.UI.Button>();
                button.onClick.Invoke();
                if (alsoDouble) button.onClick.Invoke(); // 二重遷移が無視されること(通知が増えない)を確認する
                return;
            }
            Finish(false, $"{targetScene} への遷移要素が見つからない");
        }

        private static void ResetDeadline()
        {
            SessionState.SetFloat(KeyDeadline, (float)(EditorApplication.timeSinceStartup + TimeoutSeconds));
        }

        private static void Finish(bool ok, string message)
        {
            SessionState.SetBool(KeyRun, false);
            EditorApplication.update -= Tick;
            if (ok) Debug.Log(Tag + "RESULT " + message);
            else Debug.LogError(Tag + "RESULT FAILED " + message);
            // 対話モードで実行した場合に Editor を閉じて未保存の作業を失わせないよう、バッチ時のみ終了する
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
        }
    }
}
