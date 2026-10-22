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
        public static EchoSurfaceBinding[] Bake(EchoTarget target, float maxEdge)
        {
            if (!target || maxEdge < .05f) throw new ArgumentException("最大辺長は0.05m以上です。");
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
                        if (!material || material.renderQueue > 2500 || material.IsKeywordEnabled("_ALPHATEST_ON"))
                            throw new InvalidOperationException("部分表示は不透明・非AlphaClipマテリアル専用です。");
                    var mesh = filter.sharedMesh;
                    var vertices = mesh.vertices; var normals = mesh.normals; var tangents = mesh.tangents;
                    var uv = mesh.uv; var uv2 = mesh.uv2; var colors = mesh.colors;
                    var patches = new List<EchoSurfacePatch>();
                    for (int sub = 0; sub < mesh.subMeshCount; sub++)
                    {
                        if (mesh.GetTopology(sub) != MeshTopology.Triangles) throw new InvalidOperationException("三角形Meshを指定してください。");
                        var triangles = mesh.GetTriangles(sub);
                        for (int t = 0; t < triangles.Length; t += 3)
                        {
                            int ia = triangles[t], ib = triangles[t + 1], ic = triangles[t + 2];
                            Vector3 Point(Vector3 w) => vertices[ia] * w.x + vertices[ib] * w.y + vertices[ic] * w.z;
                            void Split(Vector3 a, Vector3 b, Vector3 c, int depth)
                            {
                                var pa = Point(a); var pb = Point(b); var pc = Point(c);
                                float ab = source.transform.TransformVector(pb - pa).sqrMagnitude;
                                float bc = source.transform.TransformVector(pc - pb).sqrMagnitude;
                                float ca = source.transform.TransformVector(pa - pc).sqrMagnitude;
                                if (Mathf.Max(ab, Mathf.Max(bc, ca)) > maxEdge * maxEdge)
                                {
                                    if (depth > 24) throw new InvalidOperationException("分割が細かすぎます。最大辺長を増やしてください。");
                                    if (ab >= bc && ab >= ca) { var m = (a + b) * .5f; Split(a, m, c, depth + 1); Split(m, b, c, depth + 1); }
                                    else if (bc >= ca) { var m = (b + c) * .5f; Split(a, b, m, depth + 1); Split(a, m, c, depth + 1); }
                                    else { var m = (c + a) * .5f; Split(a, b, m, depth + 1); Split(m, b, c, depth + 1); }
                                    return;
                                }
                                float area = Vector3.Cross(source.transform.TransformVector(pb - pa), source.transform.TransformVector(pc - pa)).magnitude * .5f;
                                if (area <= .0000001f) return;
                                if (++total > MaxPatches) throw new InvalidOperationException("1対象の上限1024分割を超えます。最大辺長を増やすか、対象を分けてください。");
                                var patch = new Mesh { name = "EchoPatch_" + patches.Count };
                                generated.Add(patch);
                                var weights = new[] { a, b, c };
                                patch.vertices = new[] { pa, pb, pc };
                                patch.triangles = new[] { 0, 1, 2 };
                                var ns = new Vector3[3]; var ts = new Vector4[3];
                                var us = new Vector2[3]; var lightUvs = new Vector2[3]; var cs = new Color[3];
                                for (int i = 0; i < 3; i++)
                                {
                                    var w = weights[i];
                                    if (normals.Length == vertices.Length) ns[i] = (normals[ia] * w.x + normals[ib] * w.y + normals[ic] * w.z).normalized;
                                    if (tangents.Length == vertices.Length)
                                    {
                                        var tangent = tangents[ia] * w.x + tangents[ib] * w.y + tangents[ic] * w.z;
                                        var direction = new Vector3(tangent.x, tangent.y, tangent.z).normalized;
                                        ts[i] = new Vector4(direction.x, direction.y, direction.z, tangent.w >= 0 ? 1 : -1);
                                    }
                                    if (uv.Length == vertices.Length) us[i] = uv[ia] * w.x + uv[ib] * w.y + uv[ic] * w.z;
                                    if (uv2.Length == vertices.Length) lightUvs[i] = uv2[ia] * w.x + uv2[ib] * w.y + uv2[ic] * w.z;
                                    if (colors.Length == vertices.Length) cs[i] = colors[ia] * w.x + colors[ib] * w.y + colors[ic] * w.z;
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

