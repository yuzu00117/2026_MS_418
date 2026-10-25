using System;
using System.Collections;
using SceneFlow;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace LoadingTest.C
{
    /// <summary>
    /// 方式C: DontDestroyOnLoad で常駐するシングルトン。SceneLoader.Load("Game") で呼べる。
    /// Resources/PF_SceneLoader.prefab にアタッチする。prefab が無い場合は何もしない。
    /// </summary>
    public class SceneLoader : MonoBehaviour, ISceneLoader
    {
        private const string PrefabPath = "PF_SceneLoader"; // Resources/PF_SceneLoader.prefab
        private const float ProgressLoadedThreshold = 0.9f;
        private const float MinFillSeconds = 0.01f; // 0除算防止

        private static SceneLoader _instance;

        [SerializeField] private CanvasGroup _canvasGroup; // 全画面UI(Sort Order 大)
        [SerializeField] private Slider _progressBar;
        [SerializeField] private float _fadeTime = 0.3f;
        [SerializeField] private float _minDisplayTime = 1.0f;

        private bool _isLoading;

        private void Awake()
        {
            // _canvasGroup のオブジェクトは SetActive(false) しない
            // (同じオブジェクトにこのスクリプトがあるとコルーチンが止まる)
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
        }

        // 起動時に自動生成(どのシーンから Play しても存在する)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            // リポジトリ共有のため、prefab 未作成の環境(他メンバー)では黙ってスキップする
            var prefab = Resources.Load<SceneLoader>(PrefabPath);
            if (prefab == null) return;

            _instance = Instantiate(prefab);
            DontDestroyOnLoad(_instance.gameObject);
            SceneTransition.RegisterLoader(_instance); // 遷移の窓口(SceneTransition)から使えるようにする
        }

        private void OnDestroy()
        {
            SceneTransition.UnregisterLoader(this);
        }

        /// <summary>どこからでも呼べる。ロード中の多重呼び出しは無視する。</summary>
        public static void Load(string sceneName, Action onLoaded = null)
        {
            if (_instance == null || _instance._isLoading) return;
            _instance.StartCoroutine(_instance.LoadRoutine(sceneName, onLoaded));
        }

        // ISceneLoader: static の Load と同名のため明示実装にする
        bool ISceneLoader.IsLoading => _isLoading;

        void ISceneLoader.Load(string scenePath, Action onLoaded, Action onCompleted)
        {
            if (_isLoading) return;
            StartCoroutine(LoadRoutine(scenePath, onLoaded, onCompleted));
        }

        private IEnumerator LoadRoutine(string sceneName, Action onLoaded, Action onCompleted = null)
        {
            _isLoading = true;
            _progressBar.value = 0f; // フェードイン中に前回の満タンが見えないよう、表示前に戻す
            yield return Fade(0f, 1f);

            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
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

            yield return null; // 新シーンの初期化が一巡するのを1フレーム待つ
            onLoaded?.Invoke();

            yield return Fade(1f, 0f);
            _isLoading = false;
            onCompleted?.Invoke();
        }

        private IEnumerator Fade(float from, float to)
        {
            _canvasGroup.blocksRaycasts = true; // ロード中はクリックを遮断
            float t = 0f;
            while (t < _fadeTime)
            {
                t += Time.unscaledDeltaTime;
                _canvasGroup.alpha = Mathf.Lerp(from, to, t / _fadeTime);
                yield return null;
            }
            _canvasGroup.alpha = to;
            _canvasGroup.blocksRaycasts = to > 0f; // 非表示時は遮断を解除
        }
    }
}
