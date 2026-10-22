using UnityEngine;

namespace Echo.Echolocation
{
    /// <summary>表示分類とは独立した発生地点遮蔽。Renderer自身に付ける。</summary>
    [DisallowMultipleComponent]
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
