using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LoadingTest.A
{
    /// <summary>
    /// 方式A: 各シーンにロードUIを置く最小実装。
    /// シーンごとに LoadingPanel(Canvas + Slider) を配置してアタッチする。
    /// </summary>
    public class LoadingScreen : MonoBehaviour
    {
        // 非同期読み込みは allowSceneActivation = false の間 0.9 で止まる
        private const float ProgressLoadedThreshold = 0.9f;
        private const float MinFillSeconds = 0.01f; // 0除算防止

        [SerializeField] private GameObject _loadingPanel;     // ロード画面のルート
        [SerializeField] private Slider _progressBar;          // 進捗バー
        [SerializeField] private float _minDisplayTime = 1.0f; // 最低表示時間(チラつき防止)

        private bool _isLoading;

        /// <summary>ボタンの onClick などから呼ぶ。ロード中の再呼び出しは無視する。</summary>
        public void LoadScene(string sceneName)
        {
            if (_isLoading) return; // 二重ロード防止
            StartCoroutine(LoadRoutine(sceneName));
        }

        private IEnumerator LoadRoutine(string sceneName)
        {
            _isLoading = true;
            _loadingPanel.SetActive(true);
            _progressBar.value = 0f;

            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
            op.allowSceneActivation = false; // 読み込み完了しても自動で切り替えない

            // 実読み込みが一瞬で終わってもバーが見えるよう、表示値を一定速度で追従させる。
            // 0→1 に最低 _minDisplayTime 秒かかる(timeScale = 0 でも進むよう unscaledDeltaTime を使う)
            float shown = 0f;
            float fillSpeed = 1f / Mathf.Max(_minDisplayTime, MinFillSeconds);
            while (op.progress < ProgressLoadedThreshold || shown < 1f)
            {
                float target = Mathf.Clamp01(op.progress / ProgressLoadedThreshold);
                shown = Mathf.MoveTowards(shown, target, fillSpeed * Time.unscaledDeltaTime);
                _progressBar.value = shown;
                yield return null;
            }

            _progressBar.value = 1f;
            op.allowSceneActivation = true; // ここでシーン切り替え
        }
    }
}
