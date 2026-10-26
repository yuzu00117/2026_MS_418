using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LoadingTest.B
{
    /// <summary>
    /// 方式B: 常駐 Boot シーンに置き、ゲームシーンを Additive で出し入れする。
    /// Boot シーンはアンロードしない。Build Settings の index 0 に Boot を置くこと。
    /// </summary>
    public class AdditiveSceneLoader : MonoBehaviour
    {
        private const float ProgressLoadedThreshold = 0.9f;
        private const float MinFillSeconds = 0.01f; // 0除算防止

        [SerializeField] private CanvasGroup _loadingGroup; // Screen Space - Overlay の Canvas
        [SerializeField] private Slider _progressBar;
        [SerializeField] private string _firstScene = "Title";
        [SerializeField] private float _fadeTime = 0.3f;
        [SerializeField] private float _minDisplayTime = 0.8f;

        private Scene _currentScene; // 現在ロード中のゲームシーン
        private bool _isLoading;

        private void Start()
        {
            // _loadingGroup のオブジェクトは SetActive(false) しない。
            // このスクリプトが同じオブジェクトにあると、コルーチンが開始できない/止まるため。
            _loadingGroup.alpha = 0f;
            _loadingGroup.blocksRaycasts = false;
            StartCoroutine(LoadRoutine(_firstScene));
        }

        /// <summary>Boot 以外のシーンへ遷移する。ロード中の再呼び出しは無視する。</summary>
        public void Load(string sceneName)
        {
            if (_isLoading) return;
            StartCoroutine(LoadRoutine(sceneName));
        }

        private IEnumerator LoadRoutine(string sceneName)
        {
            _isLoading = true;

            // 1. ロードUIをフェードイン(フェードイン中に前回の満タンが見えないよう、表示前にバーを戻す)
            _progressBar.value = 0f;
            yield return Fade(0f, 1f);

            // 2. 旧シーンを先にアンロードしてメモリのピークを抑える
            if (_currentScene.IsValid() && _currentScene.isLoaded)
            {
                yield return SceneManager.UnloadSceneAsync(_currentScene);
                yield return Resources.UnloadUnusedAssets();
            }

            // 3. 新シーンを Additive でロード
            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);
            op.allowSceneActivation = false;

            // 実読み込みが一瞬で終わってもバーが見えるよう、表示値を一定速度で追従させる。
            // 0->1 に最低 _minDisplayTime 秒かかる(timeScale = 0 でも進むよう unscaledDeltaTime を使う)
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
            op.allowSceneActivation = true;
            while (!op.isDone) yield return null;

            // 4. アクティブシーンを切り替える(ライティング設定の反映に必須)
            _currentScene = SceneManager.GetSceneByName(sceneName);
            SceneManager.SetActiveScene(_currentScene);

            // 5. ロードUIをフェードアウト
            yield return Fade(1f, 0f);
            _isLoading = false;
        }

        private IEnumerator Fade(float from, float to)
        {
            _loadingGroup.blocksRaycasts = true; // ロード中はクリックを遮断
            float t = 0f;
            while (t < _fadeTime)
            {
                t += Time.unscaledDeltaTime;
                _loadingGroup.alpha = Mathf.Lerp(from, to, t / _fadeTime);
                yield return null;
            }
            _loadingGroup.alpha = to;
            _loadingGroup.blocksRaycasts = to > 0f; // 非表示時は遮断を解除
        }
    }
}
