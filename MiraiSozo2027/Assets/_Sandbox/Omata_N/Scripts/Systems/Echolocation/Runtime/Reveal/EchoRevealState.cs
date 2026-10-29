using System;
using System.Collections.Generic;
using UnityEngine;
namespace Echo.Echolocation
{
    /// <summary>旧シーンの参照を維持する互換コンポーネント。表示判定はGPUに移行済み。</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(EchoTarget)), AddComponentMenu("")]
    public sealed class EchoRevealState : MonoBehaviour
    {
        [SerializeField] private EchoRevealWorld _world;
        [SerializeField] private EchoSurfaceBinding[] _surfaces = Array.Empty<EchoSurfaceBinding>();
        public EchoRevealWorld World => _world;
        public IReadOnlyList<EchoSurfaceBinding> Surfaces => _surfaces;
        public EchoTag Tag => GetComponent<EchoTag>();
        // GPUのピクセル可視性をCPUの物理状態として公開しない。旧APIは非稼働を返す。
        public bool IsActive => false;
        public bool HasActiveCollision => false;
        public bool IsOperational => false;
        public float RevealedFraction => 0;
        public uint Revision => 0;
        public int PendingPatchCount => 0;
        public IReadOnlyList<EchoRenderItem> OriginalItems => Array.Empty<EchoRenderItem>();
        public EchoRevealSnapshot Snapshot => default;
        public event Action<EchoRevealSnapshot> Changed { add { } remove { } }
        public void Configure(EchoRevealWorld world, EchoSurfaceBinding[] surfaces)
        { _world = world; _surfaces = surfaces ?? Array.Empty<EchoSurfaceBinding>(); }
        public void Sample(EchoTagColorRule rule) { }
        public void Commit() { }
        public void Refresh() { }
        public void Release() { }
    }
}
