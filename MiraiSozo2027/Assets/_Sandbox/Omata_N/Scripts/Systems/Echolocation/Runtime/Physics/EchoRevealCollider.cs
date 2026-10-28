using System.Collections.Generic;
using UnityEngine;
namespace Echo.Echolocation
{
    /// <summary>旧シーンの参照互換用。Colliderの生成・有効状態の変更は行わない。</summary>
    [DisallowMultipleComponent, AddComponentMenu("")]
    public sealed class EchoRevealCollider : MonoBehaviour
    {
        public EchoRevealCollisionMode Mode => EchoRevealCollisionMode.Unchanged;
        public bool HasActiveCollision => false;
        public PhysicsMaterial PatchMaterial => null;
        public void Configure(EchoRevealCollisionMode mode, Collider[] colliders, LayerMask layers) { }
        public void Capture() { }
        public bool ShouldRetain(MeshCollider patch) => false;
        public void Commit(bool isActive, IReadOnlyList<MeshCollider> patches, IReadOnlyList<bool> active) { }
        public void Restore() { }
    }
}
