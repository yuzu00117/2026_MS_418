using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
namespace Echo.Echolocation.Editor
{
    /// <summary>頂点属性を補間し、最大辺長以下の三角形へ分割する。実行時のMesh生成は不要。</summary>
    public static class EchoSurfaceBaker
    {
        public const string OutputFolder = "Assets/_Sandbox/Omata_N/Settings/EchoSurfaces";
        public const int MaxPatches = 1024;
        private const float MinimumEdgeLength = .05f;
        private const int OpaqueQueueUpperBound = 2500;
        private const int MaxSubdivisionDepth = 24;
        private const float MinimumPatchArea = .0000001f;
        public static EchoSurfaceBinding[] Bake(EchoTarget target, float maxEdge)
        {
            if (!target || maxEdge < MinimumEdgeLength) throw new ArgumentException("最大辺長は0.05m以上です。");
            if (Application.isPlaying) throw new InvalidOperationException("ベイクは編集モードで実行してください。");
            var bindings = new List<EchoSurfaceBinding>();
            var generated = new List<UnityEngine.Object>();
            int total = 0;
            try
            {
                foreach (var renderer in target.GetComponentsInChildren<Renderer>(true))
                {
                    if (!target.Owns(renderer) || renderer.GetComponent<EchoRevealProxy>()) continue;
                    if (!(renderer is MeshRenderer source) || !source.TryGetComponent<MeshFilter>(out var filter) || !filter.sharedMesh ||
                        source.GetComponentInParent<Rigidbody>() || source.GetComponentInParent<LODGroup>())
                        throw new InvalidOperationException("部分表示は静的MeshRenderer専用です（LOD/Rigidbodyは非対応）。");
                    foreach (var material in source.sharedMaterials)
                        if (!material || material.renderQueue > OpaqueQueueUpperBound || material.IsKeywordEnabled("_ALPHATEST_ON"))
                            throw new InvalidOperationException("部分表示は不透明・非AlphaClipマテリアル専用です。");
                    var mesh = filter.sharedMesh;
                    var vertices = mesh.vertices; var normals = mesh.normals; var tangents = mesh.tangents;
                    var uv = mesh.uv; var uv2 = mesh.uv2; var colors = mesh.colors;
                    var patches = new List<EchoSurfacePatch>();
                    for (int sub = 0; sub < mesh.subMeshCount; sub++)
                    {
                        if (mesh.GetTopology(sub) != MeshTopology.Triangles) throw new InvalidOperationException("三角形Meshを指定してください。");
                        var triangles = mesh.GetTriangles(sub);
                        for (int triangle = 0; triangle < triangles.Length; triangle += 3)
                        {
                            int ia = triangles[triangle], ib = triangles[triangle + 1], ic = triangles[triangle + 2];
                            Vector3 GetPoint(Vector3 weight) => vertices[ia] * weight.x + vertices[ib] * weight.y + vertices[ic] * weight.z;
                            void Split(Vector3 first, Vector3 second, Vector3 third, int depth)
                            {
                                var pa = GetPoint(first); var pb = GetPoint(second); var pc = GetPoint(third);
                                float ab = source.transform.TransformVector(pb - pa).sqrMagnitude;
                                float bc = source.transform.TransformVector(pc - pb).sqrMagnitude;
                                float ca = source.transform.TransformVector(pa - pc).sqrMagnitude;
                                if (Mathf.Max(ab, Mathf.Max(bc, ca)) > maxEdge * maxEdge)
                                {
                                    if (depth > MaxSubdivisionDepth) throw new InvalidOperationException("分割が細かすぎます。最大辺長を増やしてください。");
                                    if (ab >= bc && ab >= ca) { var midpoint = (first + second) * .5f; Split(first, midpoint, third, depth + 1); Split(midpoint, second, third, depth + 1); }
                                    else if (bc >= ca) { var midpoint = (second + third) * .5f; Split(first, second, midpoint, depth + 1); Split(first, midpoint, third, depth + 1); }
                                    else { var midpoint = (third + first) * .5f; Split(first, second, midpoint, depth + 1); Split(midpoint, second, third, depth + 1); }
                                    return;
                                }
                                float area = Vector3.Cross(source.transform.TransformVector(pb - pa), source.transform.TransformVector(pc - pa)).magnitude * .5f;
                                if (area <= MinimumPatchArea) return;
                                if (++total > MaxPatches) throw new InvalidOperationException("1対象の上限1024分割を超えます。最大辺長を増やすか、対象を分けてください。");
                                var patch = new Mesh { name = "EchoPatch_" + patches.Count };
                                generated.Add(patch);
                                var weights = new[] { first, second, third };
                                patch.vertices = new[] { pa, pb, pc };
                                patch.triangles = new[] { 0, 1, 2 };
                                var ns = new Vector3[3]; var ts = new Vector4[3];
                                var us = new Vector2[3]; var lightUvs = new Vector2[3]; var cs = new Color[3];
                                for (int i = 0; i < 3; i++)
                                {
                                    var weight = weights[i];
                                    if (normals.Length == vertices.Length) ns[i] = (normals[ia] * weight.x + normals[ib] * weight.y + normals[ic] * weight.z).normalized;
                                    if (tangents.Length == vertices.Length)
                                    {
                                        var tangent = tangents[ia] * weight.x + tangents[ib] * weight.y + tangents[ic] * weight.z;
                                        var direction = new Vector3(tangent.x, tangent.y, tangent.z).normalized;
                                        ts[i] = new Vector4(direction.x, direction.y, direction.z, tangent.w >= 0 ? 1 : -1);
                                    }
                                    if (uv.Length == vertices.Length) us[i] = uv[ia] * weight.x + uv[ib] * weight.y + uv[ic] * weight.z;
                                    if (uv2.Length == vertices.Length) lightUvs[i] = uv2[ia] * weight.x + uv2[ib] * weight.y + uv2[ic] * weight.z;
                                    if (colors.Length == vertices.Length) cs[i] = colors[ia] * weight.x + colors[ib] * weight.y + colors[ic] * weight.z;
                                }
                                if (normals.Length == vertices.Length) patch.normals = ns; else patch.RecalculateNormals();
                                if (tangents.Length == vertices.Length) patch.tangents = ts;
                                patch.uv = us; patch.uv2 = lightUvs;
                                if (colors.Length == vertices.Length) patch.colors = cs;
                                patch.uv3 = new[] { Vector2.zero, Vector2.right, Vector2.up };
                                patch.RecalculateBounds();
                                patches.Add(new EchoSurfacePatch(patch, sub, area));
                            }
                            Split(Vector3.right, Vector3.up, Vector3.forward, 0);
                        }
                    }
                    var data = ScriptableObject.CreateInstance<EchoSurfaceData>();
                    generated.Add(data);
                    data.Initialize(mesh, source.transform.lossyScale, AssetDatabase.GetAssetDependencyHash(AssetDatabase.GetAssetPath(mesh)).ToString(), patches);
                    bindings.Add(new EchoSurfaceBinding(source, data));
                }
                if (bindings.Count == 0) throw new InvalidOperationException("対象Rendererがありません。");
                EnsureFolder(OutputFolder);
                foreach (var binding in bindings)
                {
                    var path = AssetDatabase.GenerateUniqueAssetPath(OutputFolder + "/SO_EchoSurface.asset");
                    AssetDatabase.CreateAsset(binding.Data, path);
                    foreach (var patch in binding.Data.Patches) AssetDatabase.AddObjectToAsset(patch.Mesh, binding.Data);
                    EditorUtility.SetDirty(binding.Data);
                }
                foreach (var binding in bindings) AssetDatabase.SaveAssetIfDirty(binding.Data);
                return bindings.ToArray();
            }
            catch
            {
                foreach (var asset in generated) if (asset && !EditorUtility.IsPersistent(asset)) UnityEngine.Object.DestroyImmediate(asset);
                throw;
            }
        }
        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            int slash = path.LastIndexOf('/');
            EnsureFolder(path.Substring(0, slash));
            AssetDatabase.CreateFolder(path.Substring(0, slash), path.Substring(slash + 1));
        }
    }
}

