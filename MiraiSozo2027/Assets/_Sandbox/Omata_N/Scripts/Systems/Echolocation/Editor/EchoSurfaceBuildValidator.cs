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
        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (report == null) return;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var state in root.GetComponentsInChildren<EchoRevealState>(true))
                {
                    if (!state.World || !state.TryGetComponent<EchoTag>(out var tag)) continue;
                    var controller = state.World.GetComponent<EchoController>();
                    EchoTagColorRule matched = null;
                    foreach (var rule in controller.Settings.TagColorRules)
                        if (rule != null && rule.IsEnabled && rule.Definition && rule.Definition == tag.Definition) { matched = rule; break; }
                    if (matched == null || matched.RevealMode != EchoOriginalRevealMode.ContactRegion || !matched.OriginalEnabled) continue;
                    if (state.Surfaces.Count == 0) throw new BuildFailedException(state.name + ": 部分表示の分割データをベイクしてください。");
                    foreach (var binding in state.Surfaces)
                        if (!IsCurrent(binding)) throw new BuildFailedException(state.name + ": Meshまたはスケールが変わっています。再ベイクしてください。");
                }
        }
    }
}
