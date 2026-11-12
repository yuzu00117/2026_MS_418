using UnityEngine;

namespace Echo.Echolocation
{
    /// <summary>旧シーンの互換用。遮断はEchoTargetの専用タグで設定する。</summary>
    [DisallowMultipleComponent, AddComponentMenu("")]
    public sealed class EchoOccluder : MonoBehaviour
    {
        [SerializeField] private bool _isBlocking = true;
        private void OnEnable() => Notify();
        private void OnDisable() => Notify();
        private void Notify()
        {
            foreach (var controller in FindObjectsByType<EchoController>()) controller.RefreshTargets();
        }
        public bool IsBlocking { get => _isBlocking; set => _isBlocking = value; }
    }
}
