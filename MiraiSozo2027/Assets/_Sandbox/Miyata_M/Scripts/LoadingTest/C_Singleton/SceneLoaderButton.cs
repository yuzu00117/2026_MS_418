using UnityEngine;
using UnityEngine.UI;

namespace LoadingTest.C
{
    /// <summary>方式C用: ボタン押下で SceneLoader.Load を呼ぶ。</summary>
    [RequireComponent(typeof(Button))]
    public class SceneLoaderButton : MonoBehaviour
    {
        [SerializeField] private string _targetScene;

        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(() => SceneLoader.Load(_targetScene));
        }
    }
}
