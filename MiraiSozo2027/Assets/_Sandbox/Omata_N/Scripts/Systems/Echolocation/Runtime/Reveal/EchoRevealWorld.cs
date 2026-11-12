using UnityEngine;
namespace Echo.Echolocation
{
    /// <summary>旧シーンの参照互換用。GPU表示にFixedUpdateや読み戻しは不要。</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(EchoController)), AddComponentMenu("")]
    public sealed class EchoRevealWorld : MonoBehaviour
    {
        public EchoController Controller { get; private set; }
        private void Awake() => Controller = GetComponent<EchoController>();
        public void Refresh() { }
    }
}
