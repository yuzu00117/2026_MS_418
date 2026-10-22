using System;
using UnityEngine;
namespace Echo.Echolocation
{
    [Serializable]
    public sealed class EchoSurfacePatch
    {
        [SerializeField] private Mesh _mesh;
        [SerializeField] private int _materialIndex;
        [SerializeField] private float _area;
        public Mesh Mesh => _mesh;
        public int MaterialIndex => _materialIndex;
        public float Area => _area;
        public EchoSurfacePatch(Mesh mesh, int materialIndex, float area)
        { _mesh = mesh; _materialIndex = materialIndex; _area = area; }
    }
}

