using UnityEngine;
using UnityEngine.UI;

namespace LoadingTest.B
{
    /// <summary>
    /// 方式B用: ゲームシーン側のボタンから Boot 常駐の AdditiveSceneLoader へ遷移を依頼する。
    /// ローダーは別シーンにあり参照を持てないため、押下時に検索する(Update では呼ばない)。
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class AdditiveLoadButton : MonoBehaviour
    {
        [SerializeField] private string _targetScene;

        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(OnClick);
        }

        private void OnClick()
        {
            var loader = FindFirstObjectByType<AdditiveSceneLoader>();
            if (loader == null)
            {
                Debug.LogWarning("AdditiveSceneLoader が見つかりません。Boot シーンから再生してください。");
                return;
            }
            loader.Load(_targetScene);
        }
    }
}
