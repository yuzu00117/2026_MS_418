using UnityEngine;
using UnityEngine.UI;

namespace SceneFlow
{
    /// <summary>ボタン押下で指定シーンへ遷移する。Inspector で遷移先を選ぶ。</summary>
    [RequireComponent(typeof(Button))]
    public class SceneTransitionButton : MonoBehaviour
    {
        [SerializeField] private SceneId _target;

        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(() => SceneTransition.Go(_target));
        }
    }
}
