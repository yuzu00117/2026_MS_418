using UnityEngine;
using UnityEngine.UI;

namespace SceneFlow
{
    /// <summary>ボタン押下で指定シーンへ遷移する。Inspector で遷移先のシーンをドラッグして指定する。</summary>
    [RequireComponent(typeof(Button))]
    public class SceneTransitionButton : MonoBehaviour
    {
        [SerializeField] private SceneReference _target;

        public SceneReference Target => _target;

        private void Awake()
        {
            GetComponent<Button>().onClick.AddListener(() => SceneTransition.Go(_target));
        }
    }
}
