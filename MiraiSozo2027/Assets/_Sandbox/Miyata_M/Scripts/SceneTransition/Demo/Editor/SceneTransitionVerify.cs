using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace SceneFlow.EditorTools
{
    /// <summary>
    /// Title -> StageSelect -> Game -> Title を、画面のボタンを押して実際に遷移させて確認する自己診断。
    /// バッチ: -executeMethod SceneFlow.EditorTools.SceneTransitionVerify.Run (-quit は付けない)
    /// 終了コード 0=OK / 1=失敗。Play 開始でドメインが再読み込みされるため進行状況は SessionState に持つ。
    /// </summary>
    [InitializeOnLoad]
    public static class SceneTransitionVerify
    {
        private const string KeyRun = "SceneTransitionVerify.Run";
        private const string KeyStage = "SceneTransitionVerify.Stage";
        private const string KeyDeadline = "SceneTransitionVerify.Deadline";
        private const double TimeoutSeconds = 30.0;
        private const string Tag = "STVERIFY: ";

        // 進行状況(Play 開始でドメインが再読み込みされるため、SessionState に保存する)
        private const int StageOpen = 0;  // Title を開いて Play を始める
        private const int StageStart = 1; // 最初の遷移を要求する
        private const int StageWait = 2;  // 遷移の完了を待つ
        private const int StageFailure = 3; // ローダーが失敗したときの後始末を待つ
        private const int StageStop = 90; // Play の終了を待つ
        private const string TitleName = DemoScenes.TitleName;
        private const string StageSelectName = DemoScenes.StageSelectName;
        private const string GameName = DemoScenes.GameName;

        // 遷移の順序。現在のシーン上の該当ボタンを押し、そのシーンがアクティブになるまで待つ
        private static readonly string[] TargetScenes = { StageSelectName, GameName, TitleName };

        // Play 中のみ有効(ドメインは Play 開始時に再読み込みされ、Play 中は維持される)
        private static int _index;
        private static int _beforeCount;
        private static int _afterCount;
        private static Keyboard _testKeyboard;       // 入力停止の確認用の仮想キーボード
        private static Keyboard _lateKeyboard;       // 遷移中に接続されるデバイスの確認用
        private static bool _isLateKeyboardBlocked;             // 遷移中に接続されたデバイスが無効になったか
        private static bool _hasBlockedDuringTransition; // 遷移中(OnAfterTransition 時点)に無効になっていたか

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
            int stage = SessionState.GetInt(KeyStage, StageOpen);
            if (HasTimedOut(stage))
            {
                Finish(false, $"stage{stage} index{_index} timeout");
                return;
            }

            switch (stage)
            {
                case StageOpen: OpenTitleAndPlay(); break;
                case StageStart: StartFirstTransition(); break;
                case StageWait: WaitTransition(); break;
                case StageFailure: WaitLoaderFailure(); break;
                case StageStop: WaitPlayStopped(); break;
            }
        }

        private static bool HasTimedOut(int stage)
        {
            return stage > StageOpen
                && EditorApplication.timeSinceStartup > SessionState.GetFloat(KeyDeadline, float.MaxValue);
        }

        private static void OpenTitleAndPlay()
        {
            EditorSceneManager.OpenScene(DemoScenes.PathOf(TitleName));
            ResetDeadline();
            EditorApplication.isPlaying = true;
            SessionState.SetInt(KeyStage, StageStart);
        }

        // ロード画面(SceneLoader)が生成・登録され Title が出たら、最初の遷移を要求する
        private static void StartFirstTransition()
        {
            if (!EditorApplication.isPlaying || EditorApplication.isCompiling) return;
            if (!SceneTransition.HasLoader) return;
            if (SceneManager.GetActiveScene().name != TitleName) return;

            _index = 0;
            _beforeCount = 0;
            _afterCount = 0;
            SubscribeTestHandlers();

            // 異常系: 遷移先が未指定なら遷移せず、通知も出ないこと
            SceneTransition.Go(null);
            SceneTransition.Go(new SceneReference());
            if (SceneTransition.IsTransitioning || _beforeCount != 0)
            {
                Finish(false, "遷移先が未指定でも遷移が始まった");
                return;
            }
            RequestClick(TargetScenes[_index], true);
            SessionState.SetInt(KeyStage, StageWait);
        }

        // 通知の購読。登録順が意味を持つ(例外 -> 件数 -> 入力停止の判定 -> 抜く、の順)
        private static void SubscribeTestHandlers()
        {
            // 通知側の例外で遷移が止まらないこと(後続の購読者にも通知が届くこと)の確認用。先に登録する
            SceneTransition.OnAfterTransition += _ => throw new System.InvalidOperationException("意図的な例外(自己診断)");
            SceneTransition.OnBeforeTransition += _ => _beforeCount++;
            SceneTransition.OnAfterTransition += _ => _afterCount++;
            _testKeyboard = InputSystem.AddDevice<Keyboard>("STVerifyKeyboard");
            SceneTransition.OnAfterTransition += _ =>
            {
                if (!_testKeyboard.added) return; // 抜いた後は判定しない
                _hasBlockedDuringTransition |= !_testKeyboard.enabled;
            };
            // 遷移中(OnAfterTransition はまだ入力停止中)にデバイスを新たに接続し、止まることを確認する
            SceneTransition.OnAfterTransition += _ =>
            {
                if (_lateKeyboard != null) return;
                _lateKeyboard = InputSystem.AddDevice<Keyboard>("STVerifyLateKeyboard");
                _isLateKeyboardBlocked = !_lateKeyboard.enabled;
            };
            // 遷移中(入力停止中)にデバイスを抜く。判定用の購読より後に登録し、抜く前の状態を先に見せる
            SceneTransition.OnAfterTransition += _ =>
            {
                if (_testKeyboard.added) InputSystem.RemoveDevice(_testKeyboard);
            };
        }

        // 遷移の完了(シーン到着 + ロード画面が消える)を待つ
        private static void WaitTransition()
        {
            if (SceneTransition.IsTransitioning) return;
            if (SceneManager.GetActiveScene().name != TargetScenes[_index]) return;

            Debug.Log(Tag + $"{TargetScenes[_index]} OK");
            _index++;
            if (_index < TargetScenes.Length)
            {
                RequestClick(TargetScenes[_index], false);
                return;
            }

            string error = CheckFinalResults();
            if (error != null)
            {
                Finish(false, error);
                return;
            }
            StartLoaderFailureTest();
        }

        // ロード画面側が遷移の途中で例外を出しても、入力とロード画面が元に戻ることの確認。
        // 進捗バーの参照を意図的に外して(読み込み途中で NullReferenceException になる)遷移を始める
        private static void StartLoaderFailureTest()
        {
            var loader = Object.FindAnyObjectByType<LoadingTest.C.SceneLoader>();
            SetLoaderField(loader, "_progressBar", null);

            foreach (var button in Object.FindObjectsByType<SceneTransitionButton>())
            {
                ResetDeadline();
                SceneTransition.Go(button.Target);
                SessionState.SetInt(KeyStage, StageFailure);
                return;
            }
            Finish(false, "失敗時の確認に使う遷移ボタンが見つからない");
        }

        // 失敗した遷移が終わる(遷移中でなくなる)のを待ち、元に戻っていることを確認する
        private static void WaitLoaderFailure()
        {
            if (SceneTransition.IsTransitioning) return;

            var loader = Object.FindAnyObjectByType<LoadingTest.C.SceneLoader>();
            var group = (CanvasGroup)typeof(LoadingTest.C.SceneLoader)
                .GetField("_canvasGroup", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(loader);
            if (!_testKeyboard.enabled || !_lateKeyboard.enabled)
            {
                Finish(false, "ローダーの失敗後に入力が戻っていない");
                return;
            }
            if (group.alpha > 0f || group.blocksRaycasts)
            {
                Finish(false, "ローダーの失敗後にロード画面が残っている");
                return;
            }
            Debug.Log(Tag + "ローダーの失敗後に入力とロード画面が戻った OK");

            InputSystem.RemoveDevice(_testKeyboard);
            InputSystem.RemoveDevice(_lateKeyboard);
            EditorApplication.isPlaying = false;
            SessionState.SetInt(KeyStage, StageStop);
        }

        private static void SetLoaderField(LoadingTest.C.SceneLoader loader, string fieldName, object value)
        {
            typeof(LoadingTest.C.SceneLoader)
                .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(loader, value);
        }

        // 全遷移の後の確認。問題があればその内容を返す(なければ null)
        private static string CheckFinalResults()
        {
            // 二重呼び出しは無視されるため、通知は遷移回数ちょうどになる
            if (_beforeCount != TargetScenes.Length || _afterCount != TargetScenes.Length)
                return $"通知回数が不正 before={_beforeCount} after={_afterCount}";
            // 遷移中は入力が止まり、完了後は戻っていること
            if (!_hasBlockedDuringTransition) return "遷移中にキーボード入力が止まっていない";
            if (!_isLateKeyboardBlocked) return "遷移中に接続されたデバイスが止まっていない";
            if (!_lateKeyboard.enabled) return "遷移完了後にキーボード入力が戻っていない";
            // 遷移中に抜かれたデバイスは、完了後に再接続された時点で有効に戻ること
            if (_testKeyboard.added) return "遷移中に抜いたデバイスが残っている";
            InputSystem.AddDevice(_testKeyboard);
            if (!_testKeyboard.enabled) return "遷移中に抜かれたデバイスが、再接続後も無効のまま";
            return null;
        }

        // Play 終了を待って完了
        private static void WaitPlayStopped()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
            Finish(true, "ALL OK");
        }

        // 現在のシーンから目的のシーンへ遷移する要素を探して実行する。
        // SceneTransitionTrigger(イベント用)があればそれを優先し、無ければボタンを実際に押す
        private static void RequestClick(string targetScene, bool isDoubleClick)
        {
            ResetDeadline();
            // カウントダウン(UnityEvent -> Trigger.Go の接続)があるシーンでは、押さずにイベントでの遷移を待つ
            if (Object.FindAnyObjectByType<CountdownEvent>() != null)
            {
                Debug.Log(Tag + $"{targetScene} へ CountdownEvent の発火で遷移するのを待つ");
                return;
            }
            foreach (var t in Object.FindObjectsByType<SceneTransitionTrigger>())
            {
                if (t.Target.SceneName != targetScene) continue;
                Debug.Log(Tag + $"{targetScene} へ Trigger で遷移");
                t.Go();
                if (isDoubleClick) t.Go();
                return;
            }
            foreach (var b in Object.FindObjectsByType<SceneTransitionButton>())
            {
                if (b.Target.SceneName != targetScene) continue;
                var button = b.GetComponent<UnityEngine.UI.Button>();
                button.onClick.Invoke();
                if (isDoubleClick) button.onClick.Invoke(); // 二重遷移が無視されること(通知が増えない)を確認する
                return;
            }
            Finish(false, $"{targetScene} への遷移要素が見つからない");
        }

        private static void ResetDeadline()
        {
            SessionState.SetFloat(KeyDeadline, (float)(EditorApplication.timeSinceStartup + TimeoutSeconds));
        }

        private static void Finish(bool isSuccess, string message)
        {
            SessionState.SetBool(KeyRun, false);
            EditorApplication.update -= Tick;
            if (isSuccess) Debug.Log(Tag + "RESULT " + message);
            else Debug.LogError(Tag + "RESULT FAILED " + message);
            // 対話モードで実行した場合に Editor を閉じて未保存の作業を失わせないよう、バッチ時のみ終了する
            if (Application.isBatchMode) EditorApplication.Exit(isSuccess ? 0 : 1);
        }
    }
}
