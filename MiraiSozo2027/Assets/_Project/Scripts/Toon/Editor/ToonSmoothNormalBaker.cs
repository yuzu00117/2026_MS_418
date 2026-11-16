using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MS.Toon.Editor
{
    /// <summary>
    /// 輪郭線(裏面押し出し)用のスムース法線を UV3(TEXCOORD3)へ焼き込む。
    /// 角が硬いメッシュでは頂点が面ごとに分かれ法線がバラバラになり、押し出すと角で輪郭線が割れるため、
    /// 同一位置の頂点の法線を平均した値を別チャンネルに持たせる。
    /// モデルのインポート時に自動で焼き込む。焼かれていないメッシュ(Unity 標準の Cube 等)は、
    /// シェーダー側でモデル中心からの向きに押し出して代用する(ToonOutline.hlsl の GetOutlineDirectionWS)。
    /// </summary>
    public class ToonSmoothNormalBaker : AssetPostprocessor
    {
        // シェーダー側 TEXCOORD3 に対応する UV チャンネル
        private const int SmoothNormalChannel = 3;
        // UV3 の w に書く「焼き込み済み」の目印。ToonOutline.hlsl の TOON_OUTLINE_MARKER_* と一致させること。
        // UV3 を持たないメッシュでは、シェーダーが 0 ではなく別チャンネル(UV0 等)の値を受け取ることがあるため、
        // 「0 かどうか」ではなく目印で焼き込み済みかを判定する。UV0 等の w は 0 か 1 になるので 2 以上を使う。
        // タンジェント空間: スキンメッシュでも骨の回転に追従する(法線・タンジェントは GPU で回るが UV は回らないため)
        private const float TangentSpaceMarker = 2f;
        // オブジェクト空間: タンジェントを用意できないメッシュ(UV が無い等)の代替
        private const float ObjectSpaceMarker = 3f;
        // 同一位置とみなすための量子化精度(1 / この値 単位)
        private const float PositionQuantize = 10000f;
        // これ未満の二乗長は退化(長さ 0)として扱う
        private const float DegenerateSqrEpsilon = 1e-12f;

        // モデルのインポート時に、含まれる全メッシュへ自動で焼き込む
        private void OnPostprocessModel(GameObject root)
        {
            foreach (MeshFilter meshFilter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                BakeToMesh(meshFilter.sharedMesh);
            }
            foreach (SkinnedMeshRenderer skinned in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                BakeToMesh(skinned.sharedMesh);
            }
        }

        /// <summary>
        /// スムース法線を UV3 の xyz へ、焼き込みの種類を表す目印を w へ書き込む。
        /// 可能なら各頂点のタンジェント空間(tangent, bitangent, normal 基準)に変換して保存する。
        /// メッシュが無い、または法線が無いときは何もしない。
        /// </summary>
        private static void BakeToMesh(Mesh mesh)
        {
            if (mesh == null)
            {
                return;
            }
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            if (normals == null || normals.Length != vertices.Length || vertices.Length == 0)
            {
                return;
            }

            Vector3[] smoothNormals = CalculateSmoothNormals(mesh, vertices, normals);
            Vector4[] tangents = GetOrCalculateTangents(mesh);
            bool canUseTangentSpace = tangents != null;

            var packed = new List<Vector4>(smoothNormals.Length);
            for (int i = 0; i < smoothNormals.Length; i++)
            {
                packed.Add(canUseTangentSpace
                    ? ToTangentSpace(smoothNormals[i], normals[i], tangents[i])
                    : new Vector4(smoothNormals[i].x, smoothNormals[i].y, smoothNormals[i].z, ObjectSpaceMarker));
            }
            mesh.SetUVs(SmoothNormalChannel, packed);
        }

        /// <summary>
        /// 頂点位置ごとに法線を平均したスムース法線を返す。
        /// 平均は面の頂点角で重み付けする(分割数に依存しないため)。
        /// </summary>
        private static Vector3[] CalculateSmoothNormals(Mesh mesh, Vector3[] vertices, Vector3[] normals)
        {
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

        // タンジェントが無ければ UV0 から計算する。UV0 も無く計算できない場合は null。
        private static Vector4[] GetOrCalculateTangents(Mesh mesh)
        {
            int vertexCount = mesh.vertexCount;
            Vector4[] tangents = mesh.tangents;
            if (tangents != null && tangents.Length == vertexCount)
            {
                return tangents;
            }
            if (!mesh.HasVertexAttribute(UnityEngine.Rendering.VertexAttribute.TexCoord0))
            {
                return null;
            }
            mesh.RecalculateTangents();
            tangents = mesh.tangents;
            return tangents != null && tangents.Length == vertexCount ? tangents : null;
        }

        // スムース法線を、その頂点の (tangent, bitangent, normal) を軸とする座標へ変換する。
        // bitangent の向きは URP の GetVertexNormalInputs と同じ cross(normal, tangent) * tangent.w に揃える。
        private static Vector4 ToTangentSpace(Vector3 smoothNormal, Vector3 normal, Vector4 tangent)
        {
            Vector3 n = normal.normalized;
            Vector3 t = new Vector3(tangent.x, tangent.y, tangent.z);
            // 法線と直交するよう補正する(シェーダー側の行列とずれないようにするため)
            t = (t - n * Vector3.Dot(n, t)).normalized;
            Vector3 b = Vector3.Cross(n, t) * (tangent.w < 0f ? -1f : 1f);
            return new Vector4(Vector3.Dot(smoothNormal, t), Vector3.Dot(smoothNormal, b), Vector3.Dot(smoothNormal, n),
                               TangentSpaceMarker);
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
    }
}
