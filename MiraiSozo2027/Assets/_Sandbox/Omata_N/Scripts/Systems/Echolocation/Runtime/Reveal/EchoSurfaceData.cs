using System.Collections.Generic;
using UnityEngine;
namespace Echo.Echolocation
{
    /// <summary>Editorで分割した三角形。表示と物理が同じMeshを共有する。</summary>
    public sealed class EchoSurfaceData : ScriptableObject
    {
        [SerializeField] private Mesh _sourceMesh;
        [SerializeField] private Vector3 _sourceScale;
        [SerializeField] private string _sourceHash;
        [SerializeField] private List<EchoSurfacePatch> _patches = new List<EchoSurfacePatch>();
        public Mesh SourceMesh => _sourceMesh;
        public Vector3 SourceScale => _sourceScale;
        public string SourceHash => _sourceHash;
        public IReadOnlyList<EchoSurfacePatch> Patches => _patches;
        public void Initialize(Mesh source, Vector3 scale, string hash, List<EchoSurfacePatch> patches)
        { _sourceMesh = source; _sourceScale = scale; _sourceHash = hash; _patches = patches; }
    }
}

