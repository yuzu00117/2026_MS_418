using UnityEngine;

namespace SceneFlow
{
    /// <summary>
    /// 指定シーンへの遷移を、Inspector から呼べるようにするコンポーネント。
    /// クリア判定などのイベント(UnityEvent)に、このコンポーネントの Go を接続して使う。
    /// 遷移先は Inspector でシーンをドラッグして指定する。
    /// </summary>
    public class SceneTransitionTrigger : MonoBehaviour
    {
        [SerializeField] private SceneReference _target;

        public SceneReference Target => _target;

        /// <summary>遷移する。二重呼び出しや遷移中の呼び出しは SceneTransition 側で無視される。</summary>
        public void Go()
        {
            SceneTransition.Go(_target);
        }
    }
}
