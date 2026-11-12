using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;
namespace Echo.Echolocation.Editor
{
    /// <summary>ビルド対象シーンから参照される古いベイクデータを検出する。</summary>
    public sealed class EchoSurfaceBuildValidator : IProcessSceneWithReport
    {
        public int callbackOrder => 0;
        public static bool IsCurrent(EchoSurfaceBinding binding)
        {
            if (binding == null || !binding.Renderer || !binding.Data ||
                !binding.Renderer.TryGetComponent<MeshFilter>(out var filter) ||
                filter.sharedMesh != binding.Data.SourceMesh) return false;
            if ((binding.Renderer.transform.lossyScale - binding.Data.SourceScale).sqrMagnitude > .000001f) return false;
            return binding.Data.SourceHash == AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(filter.sharedMesh)).ToString();
        }
        // ベイクデータは新しい描画方式から参照しない。旧データの有無でビルドを阻止しない。
        public void OnProcessScene(Scene scene, BuildReport report) { }
    }
}
