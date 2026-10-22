using UnityEngine;

namespace Echo.Echolocation
{
    [DisallowMultipleComponent, RequireComponent(typeof(EchoTarget))]
    public sealed class EchoTag : MonoBehaviour
    {
        [SerializeField] private EchoTagDefinition _definition;
        public EchoTagDefinition Definition { get => _definition; set { _definition = value; Refresh(); } }
        private EchoTarget _target;
        private bool _hasPendingRefresh;
        private void Awake() => _target = GetComponent<EchoTarget>();
        private void OnEnable() => Refresh();
        private void OnDisable() => Refresh();
        private void OnValidate() => _hasPendingRefresh = true;
        private void LateUpdate()
        {
            if (!_hasPendingRefresh) return;
            _hasPendingRefresh = false;
            Refresh();
        }
        private void Refresh()
        {
            if (!_target) _target = GetComponent<EchoTarget>();
            if (_target) _target.RefreshRenderers();
        }
    }
}
