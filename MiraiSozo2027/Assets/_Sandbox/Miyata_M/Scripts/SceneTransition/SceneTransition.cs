using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SceneFlow
{
    /// <summary>
    /// シーン遷移の窓口。SceneTransition.Go(SceneId.Game) で、ロード画面を挟んで遷移する。
    /// DontDestroyOnLoad で常駐し、Resources/SceneTransition.prefab を起動時に自動生成する。
    /// prefab が無い環境(他メンバー)では何もしない。
    /// </summary>
    public class SceneTransition : MonoBehaviour
    {
        private const string PrefabPath = "SceneTransition"; // Resources/SceneTransition.prefab
        private const float ProgressLoadedThreshold = 0.9f;  // allowSceneActivation = false の間は 0.9 で止まる
        private const float MinFillSeconds = 0.01f;          // 0除算防止

        private static SceneTransition _instance;

        [SerializeField] private CanvasGroup _canvasGroup; // ロード画面のルート(Sort Order 大)
        [SerializeField] private Slider _progressBar;
        [SerializeField] private float _fadeTime = 0.3f;
        [SerializeField] private float _minDisplayTime = 1.0f; // バーが 0→1 になるまでの最低秒数

        private bool _isTransitioning;

        /// <summary>遷移を始める直前に通知する。</summary>
        public static event Action<SceneId> OnBeforeTransition;

        /// <summary>新シーンの初期化が一巡し、ロード画面が消え始める直前に通知する。</summary>
        public static event Action<SceneId> OnAfterTransition;

        public static bool IsTransitioning => _instance != null && _instance._isTransitioning;

        private void Awake()
        {
            // _canvasGroup のオブジェクトは SetActive(false) しない
            // (同じオブジェクトにこのスクリプトがあるとコルーチンが止まる)
            _canvasGroup.alpha = 0f;
            _canvasGroup.blocksRaycasts = false;
        }

        // 起動時に自動生成する(どのシーンから Play しても存在する)
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            var prefab = Resources.Load<SceneTransition>(PrefabPath);
            if (prefab == null) return;

            _instance = Instantiate(prefab);
            DontDestroyOnLoad(_instance.gameObject);
        }

        /// <summary>指定シーンへ遷移する。遷移中・未登録シーンの場合は無視する。</summary>
        public static void Go(SceneId id)
        {
            if (_instance == null)
            {
                Debug.LogWarning("SceneTransition が存在しません。Resources/SceneTransition.prefab を確認してください。");
                return;
            }
            if (_instance._isTransitioning) return;

            string sceneName = id.ToSceneName();
            if (!Application.CanStreamedLevelBeLoaded(sceneName))
            {
                Debug.LogWarning($"シーン {sceneName} が Build Settings に登録されていません。");
                return;
            }
            _instance.StartCoroutine(_instance.TransitionRoutine(id, sceneName));
        }

        private IEnumerator TransitionRoutine(SceneId id, string sceneName)
        {
            _isTransitioning = true;
            OnBeforeTransition?.Invoke(id);

            yield return Fade(0f, 1f);
            _progressBar.value = 0f;

            AsyncOperation op = SceneManager.LoadSceneAsync(sceneName);
            op.allowSceneActivation = false;

            // 実読み込みが一瞬で終わってもバーが見えるよう、表示値を一定速度で追従させる。
            // timeScale = 0 でも進むよう unscaledDeltaTime を使う
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
            OnAfterTransition?.Invoke(id);

            yield return Fade(1f, 0f);
            _isTransitioning = false;
        }

        private IEnumerator Fade(float from, float to)
        {
            _canvasGroup.blocksRaycasts = true; // 遷移中はクリックを遮断
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
