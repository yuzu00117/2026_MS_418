using System;
using UnityEngine;

namespace SceneFlow
{
    /// <summary>
    /// シーン遷移の窓口。SceneTransition.Go(sceneReference) で遷移する。
    /// ロード画面は持たず、登録された ISceneLoader(ロード画面側)に読み込みを任せる。
    /// 担当は「遷移先の検証」「二重遷移の防止」「前後の通知」「遷移中の入力停止」。
    /// </summary>
    public static class SceneTransition
    {
        private static ISceneLoader _loader;

        /// <summary>遷移を始める直前に通知する。</summary>
        public static event Action<SceneReference> OnBeforeTransition;

        /// <summary>新シーンの初期化が一巡し、ロード画面が消え始める直前に通知する。</summary>
        public static event Action<SceneReference> OnAfterTransition;

        /// <summary>遷移中にゲーム入力(キーボード・マウス・パッド)を止めるか。既定は true。</summary>
        public static bool IsInputBlockEnabled { get; set; } = true;

        public static bool HasLoader => _loader != null;

        public static bool IsTransitioning => _loader != null && _loader.IsLoading;

        /// <summary>ロード画面側が起動時に自分を登録する。</summary>
        public static void RegisterLoader(ISceneLoader loader)
        {
            _loader = loader;
        }

        /// <summary>登録中のローダーと同一の場合だけ解除する(別のローダーに差し替わっていたら何もしない)。</summary>
        public static void UnregisterLoader(ISceneLoader loader)
        {
            if (_loader != loader) return;
            _loader = null;
            TransitionInputBlocker.Release(); // 遷移の途中でローダーが破棄されても、入力を止めたままにしない
        }

        /// <summary>指定シーンへ遷移する。ローダー未登録・遷移中・未指定・未登録シーンの場合は無視する。</summary>
        public static void Go(SceneReference scene)
        {
            if (_loader == null)
            {
                Debug.LogWarning("ISceneLoader が登録されていません。ロード画面側の初期化を確認してください。");
                return;
            }
            if (_loader.IsLoading) return;

            if (scene == null || !scene.IsAssigned)
            {
                Debug.LogWarning("遷移先のシーンが指定されていません。");
                return;
            }
            if (!Application.CanStreamedLevelBeLoaded(scene.ScenePath))
            {
                Debug.LogWarning($"シーン {scene.SceneName} が Build Settings に登録されていません。");
                return;
            }

            Notify(OnBeforeTransition, scene);
            if (IsInputBlockEnabled) TransitionInputBlocker.Block();
            try
            {
                _loader.Load(
                    scene.ScenePath,
                    () => Notify(OnAfterTransition, scene),
                    TransitionInputBlocker.Release); // ロード画面が消えて遷移が完了したら入力を戻す
            }
            catch (Exception e)
            {
                // コルーチンを使わない実装が Load で直接例外を投げた場合、onCompleted は呼ばれない。
                // 入力を止めたままにしないため、ここで戻す(コルーチン内の例外はローダー側の finally が受け持つ)
                Debug.LogException(e);
                TransitionInputBlocker.Release();
            }
        }

        // 購読側の例外で遷移が止まらないよう、購読者ごとに例外を捕まえてログに出す。
        // OnAfterTransition はローダーのコルーチン内で呼ばれるため、例外が漏れると
        // 「遷移中」のまま戻らず、ロード画面が消えなくなる。
        // コスト: GetInvocationList で購読者数ぶんの配列を確保する(遷移1回につき O(購読者数)、遷移時のみ)
        private static void Notify(Action<SceneReference> handlers, SceneReference scene)
        {
            if (handlers == null) return;
            foreach (Action<SceneReference> handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(scene);
                }
                catch (Exception e)
                {
                    Debug.LogException(e);
                }
            }
        }
    }
}
