using UnityEngine;

namespace Echo.Echolocation
{
    /// <summary>旧シーンの互換用。専用タグはEchoTargetで指定する。統合ツールで削除できる。</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(EchoTarget)), AddComponentMenu("")]
    public sealed class EchoTag : MonoBehaviour
    {
        [SerializeField] private EchoTagDefinition _definition;
        public EchoTagDefinition Definition { get => _definition; set { _definition = value; Refresh(); } }
        private EchoTarget _target;
        private bool _hasPendingRefresh;
        internal void SetDefinitionWithoutRefresh(EchoTagDefinition value) => _definition = value;
        private void Awake() => _target = GetComponent<EchoTarget>();
        private void OnEnable() => Refresh();
        private void OnDisable() => Refresh();
        private void OnDestroy()
        {
            if (_target) _target.ForgetLegacyTag();
            Refresh();
        }
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
            if (!_target) return;
            _target.ForgetLegacyTag();
            _target.RefreshRenderers();
        }
    }
}
