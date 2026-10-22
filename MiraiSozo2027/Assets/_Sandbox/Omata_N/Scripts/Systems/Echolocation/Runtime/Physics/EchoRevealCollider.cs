using System.Collections.Generic;
using UnityEngine;
namespace Echo.Echolocation
{
    /// <summary>指定Colliderだけを一時制御する。解除時は取得時のenabledへ戻す。</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(EchoRevealState))]
    public sealed class EchoRevealCollider : MonoBehaviour
    {
        [SerializeField] private EchoRevealCollisionMode _mode;
        [SerializeField] private Collider[] _originalColliders = System.Array.Empty<Collider>();
        [SerializeField] private LayerMask _occupantLayers;
        [SerializeField] private PhysicsMaterial _patchMaterial;
        [SerializeField, Tooltip("重なっている間は表示と衝突を一緒に保持する。検出バッファが満杯の場合も保持。")]
        private bool _hasPenetrationGuard = true;
        private readonly Dictionary<Collider, bool> _initialStates = new Dictionary<Collider, bool>();
        private IReadOnlyList<MeshCollider> _patchColliders;
        private bool _hasPendingRefresh;
        private EchoRevealState _state;
        private readonly Collider[] _overlaps = new Collider[64];
        public EchoRevealCollisionMode Mode => isActiveAndEnabled ? _mode : EchoRevealCollisionMode.Unchanged;
        public bool HasActiveCollision { get; private set; }
        public PhysicsMaterial PatchMaterial => _patchMaterial;
        private void Awake() => _state = GetComponent<EchoRevealState>();
        private void OnValidate() => _hasPendingRefresh = true;
        private void LateUpdate()
        {
            if (!_hasPendingRefresh) return;
            _hasPendingRefresh = false;
            if (_state) _state.Refresh();
        }
        public void Configure(EchoRevealCollisionMode mode, Collider[] originalColliders, LayerMask occupantLayers)
        {
            Restore(); _mode = mode; _originalColliders = originalColliders ?? System.Array.Empty<Collider>(); _occupantLayers = occupantLayers;
            if (_state) _state.Refresh();
        }
        public void Capture()
        {
            if (Mode == EchoRevealCollisionMode.Unchanged || _initialStates.Count != 0) return;
            foreach (var collider in _originalColliders)
                if (collider && !_initialStates.ContainsKey(collider)) _initialStates.Add(collider, collider.enabled);
        }
        public bool ShouldRetain(MeshCollider patch)
        {
            if (!_hasPenetrationGuard || !patch || !patch.enabled || _occupantLayers.value == 0) return false;
            var bounds = patch.bounds;
            int count = Physics.OverlapBoxNonAlloc(bounds.center, Vector3.Max(bounds.extents, Vector3.one * .001f), _overlaps, Quaternion.identity,
                _occupantLayers, QueryTriggerInteraction.Ignore);
            if (count == _overlaps.Length) return true;
            for (int i = 0; i < count; i++)
            {
                var actor = _overlaps[i];
                if (!actor || actor.transform.IsChildOf(transform)) continue;
                if (!actor.attachedRigidbody && !(actor is CharacterController)) continue;
                if (!(actor is BoxCollider || actor is SphereCollider || actor is CapsuleCollider || actor is MeshCollider mesh && mesh.convex)) return true;
                if (Physics.ComputePenetration(actor, actor.transform.position, actor.transform.rotation,
                    patch, patch.transform.position, patch.transform.rotation, out _, out _)) return true;
            }
            return false;
        }
        public void Commit(bool isActive, IReadOnlyList<MeshCollider> patches, IReadOnlyList<bool> active)
        {
            Capture();
            _patchColliders = patches;
            bool canCollide = _originalColliders.Length == 0;
            foreach (var pair in _initialStates) canCollide |= pair.Value;
            HasActiveCollision = false;
            foreach (var pair in _initialStates)
                if (pair.Key)
                {
                    pair.Key.enabled = pair.Value && Mode == EchoRevealCollisionMode.WholeObject && isActive;
                    HasActiveCollision |= pair.Key.enabled;
                }
            for (int i = 0; i < patches.Count; i++)
                if (patches[i])
                {
                    patches[i].enabled = canCollide && Mode == EchoRevealCollisionMode.RevealedRegion && active[i];
                    HasActiveCollision |= patches[i].enabled;
                }
            if (Mode == EchoRevealCollisionMode.Unchanged) Restore();
        }
        public void Restore()
        {
            foreach (var pair in _initialStates) if (pair.Key) pair.Key.enabled = pair.Value;
            _initialStates.Clear();
            if (_patchColliders != null) foreach (var patch in _patchColliders) if (patch) patch.enabled = false;
            _patchColliders = null;
            HasActiveCollision = false;
        }
        private void OnDisable() => Restore();
    }
}

