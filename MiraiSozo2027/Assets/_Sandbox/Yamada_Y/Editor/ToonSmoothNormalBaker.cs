using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MS.Toon.Editor
{
    /// <summary>
    /// 輪郭線(裏面押し出し)用のスムース法線を UV3(TEXCOORD3)へ焼き込む。
    /// 角が硬いメッシュでは頂点が面ごとに分かれ法線がバラバラになり、押し出すと角で輪郭線が割れるため、
    /// 同一位置の頂点の法線を平均した値を別チャンネルに持たせる。
    /// モデルのインポート時に自動で焼き込み、Mesh アセットは Tools/MS/Toon メニューからコピーして焼く。
    /// </summary>
    public class ToonSmoothNormalBaker : AssetPostprocessor
    {
        // シェーダー側 TEXCOORD3 に対応する UV チャンネル
        private const int SmoothNormalChannel = 3;
        // 同一位置とみなすための量子化精度(1 / この値 単位)
        private const float PositionQuantize = 10000f;
        // これ未満の二乗長は退化(長さ 0)として扱う
        private const float DegenerateSqrEpsilon = 1e-12f;
        private const string MeshParentFolder = "Assets/_Sandbox/Yamada_Y/Art";
        private const string MeshFolderName = "Meshes";
        private const string MeshFolder = MeshParentFolder + "/" + MeshFolderName;
        private const string MenuCopySelected = "Tools/MS/Toon/Bake Smooth Normals (Copy Selected Mesh)";
        private const string MenuCreateCube = "Tools/MS/Toon/Create Smooth Cube";
        private const string BuiltinCubeName = "Cube.fbx";

        /// <summary>
        /// 頂点位置ごとに法線を平均したスムース法線を返す。法線が無いメッシュでは null。
        /// 平均は面の頂点角で重み付けする(分割数に依存しないため)。
        /// </summary>
        public static Vector3[] CalculateSmoothNormals(Mesh mesh)
        {
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            if (normals == null || normals.Length != vertices.Length || vertices.Length == 0)
            {
                return null;
            }

            int[] groupOfVertex = GroupVerticesByPosition(vertices, out int groupCount);
            Vector3[] sums = AccumulateAngleWeightedNormals(mesh, vertices, normals, groupOfVertex, groupCount);

            var result = new Vector3[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 sum = sums[groupOfVertex[i]];
                // どの三角形にも使われていない頂点は元の法線を使う
                result[i] = sum.sqrMagnitude > DegenerateSqrEpsilon ? sum.normalized : normals[i];
            }
            return result;
        }

        /// <summary>スムース法線を UV3 へ書き込む。他の UV チャンネルには触れない。</summary>
        public static bool BakeToMesh(Mesh mesh)
        {
            Vector3[] smoothNormals = CalculateSmoothNormals(mesh);
            if (smoothNormals == null)
            {
                return false;
            }
            mesh.SetUVs(SmoothNormalChannel, new List<Vector3>(smoothNormals));
            return true;
        }

        // 同一位置の頂点を同じグループ番号にまとめる(面ごとに分かれた頂点を一つとして扱うため)
        private static int[] GroupVerticesByPosition(Vector3[] vertices, out int groupCount)
        {
            var groupIndices = new Dictionary<Vector3Int, int>();
            var groupOfVertex = new int[vertices.Length];
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3Int key = Vector3Int.RoundToInt(vertices[i] * PositionQuantize);
                if (!groupIndices.TryGetValue(key, out int group))
                {
                    group = groupIndices.Count;
                    groupIndices.Add(key, group);
                }
                groupOfVertex[i] = group;
            }
            groupCount = groupIndices.Count;
            return groupOfVertex;
        }

        // 三角形ごとに、頂点角で重み付けした法線をグループへ加算する(分割数に依存させないため)
        private static Vector3[] AccumulateAngleWeightedNormals(Mesh mesh, Vector3[] vertices, Vector3[] normals,
                                                                int[] groupOfVertex, int groupCount)
        {
            var sums = new Vector3[groupCount];
            var triangles = new List<int>();
            for (int sub = 0; sub < mesh.subMeshCount; sub++)
            {
                if (mesh.GetTopology(sub) != MeshTopology.Triangles)
                {
                    continue;
                }
                triangles.Clear();
                mesh.GetTriangles(triangles, sub);
                for (int t = 0; t + 2 < triangles.Count; t += 3)
                {
                    AccumulateTriangle(vertices, normals, groupOfVertex, sums, triangles[t], triangles[t + 1], triangles[t + 2]);
                }
            }
            return sums;
        }

        private static void AccumulateTriangle(Vector3[] vertices, Vector3[] normals, int[] groupOfVertex,
                                               Vector3[] sums, int i0, int i1, int i2)
        {
            AddCorner(vertices, normals, groupOfVertex, sums, i0, i1, i2);
            AddCorner(vertices, normals, groupOfVertex, sums, i1, i2, i0);
            AddCorner(vertices, normals, groupOfVertex, sums, i2, i0, i1);
        }

        private static void AddCorner(Vector3[] vertices, Vector3[] normals, int[] groupOfVertex,
                                      Vector3[] sums, int corner, int next, int prev)
        {
            Vector3 edgeA = vertices[next] - vertices[corner];
            Vector3 edgeB = vertices[prev] - vertices[corner];
            if (edgeA.sqrMagnitude < DegenerateSqrEpsilon || edgeB.sqrMagnitude < DegenerateSqrEpsilon)
            {
                return;
            }
            float angle = Vector3.Angle(edgeA, edgeB) * Mathf.Deg2Rad;
            sums[groupOfVertex[corner]] += normals[corner] * angle;
        }

        // モデルのインポート時に、含まれる全メッシュへ自動で焼き込む
        private void OnPostprocessModel(GameObject root)
        {
            foreach (MeshFilter meshFilter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                BakeIfPresent(meshFilter.sharedMesh);
            }
            foreach (SkinnedMeshRenderer skinned in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                BakeIfPresent(skinned.sharedMesh);
            }
        }

        private static void BakeIfPresent(Mesh mesh)
        {
            if (mesh != null)
            {
                BakeToMesh(mesh);
            }
        }

        [MenuItem(MenuCopySelected)]
        private static void CopySelectedMeshes()
        {
            Mesh[] meshes = Selection.GetFiltered<Mesh>(SelectionMode.Assets);
            if (meshes.Length == 0)
            {
                Debug.LogWarning("Project で Mesh アセットを選択してください。組み込みの Cube は 'Tools/MS/Toon/Create Smooth Cube' を使います。");
                return;
            }
            foreach (Mesh mesh in meshes)
            {
                CreateBakedCopy(mesh, mesh.name);
            }
            AssetDatabase.SaveAssets();
        }

        [MenuItem(MenuCreateCube)]
        private static void CreateSmoothCube()
        {
            Mesh cube = Resources.GetBuiltinResource<Mesh>(BuiltinCubeName);
            Mesh created = CreateBakedCopy(cube, "Cube");
            AssetDatabase.SaveAssets();
            if (created != null)
            {
                Selection.activeObject = created;
            }
        }

        private static Mesh CreateBakedCopy(Mesh source, string baseName)
        {
            // 組み込みメッシュは読み取り不可の場合があるため、複製してから処理する
            Mesh copy = Object.Instantiate(source);
            copy.name = baseName + "_SmoothNormals";
            if (!BakeToMesh(copy))
            {
                Debug.LogWarning($"{source.name} は法線が無いためスキップしました。");
                Object.DestroyImmediate(copy);
                return null;
            }

            EnsureMeshFolder();
            string path = AssetDatabase.GenerateUniqueAssetPath($"{MeshFolder}/{copy.name}.asset");
            AssetDatabase.CreateAsset(copy, path);
            return copy;
        }

        private static void EnsureMeshFolder()
        {
            if (!AssetDatabase.IsValidFolder(MeshFolder))
            {
                AssetDatabase.CreateFolder(MeshParentFolder, MeshFolderName);
            }
        }
    }
}
