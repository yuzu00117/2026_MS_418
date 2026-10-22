using System;
using UnityEngine;
namespace Echo.Echolocation
{
    [Serializable]
    public sealed class EchoSurfaceBinding
    {
        [SerializeField] private MeshRenderer _renderer;
        [SerializeField] private EchoSurfaceData _data;
        public MeshRenderer Renderer => _renderer;
        public EchoSurfaceData Data => _data;
        public EchoSurfaceBinding(MeshRenderer renderer, EchoSurfaceData data) { _renderer = renderer; _data = data; }
    }
}

