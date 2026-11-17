using UnityEngine;
using UnityEngine.Rendering;

namespace Aura.Water
{
    /// <summary>
    /// 水面シェーダー(ToonWater)用に、細かく分割した平らな板を作って MeshFilter に入れる。
    /// Unity 標準の Plane は縦横 10 マスしかなく、頂点を動かす波がカクカクになるため、分割数を指定できる板を自分で作る。
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter))]
    public sealed class WaterGridMesh : MonoBehaviour
    {
        private const int MinResolution = 1;
        private const int MaxResolution = 500;
        private const int MaxVertexCountFor16BitIndex = 65535;
        // 波で頂点が動いても板が画面外と誤判定されて消えないよう、描画範囲の高さに足す余裕(m)
        private const float BoundsHeightMargin = 5f;

        [SerializeField] private float _size = 40f;
        [SerializeField, Range(MinResolution, MaxResolution)] private int _resolution = 200;

        private Mesh _mesh;
        private MeshFilter _meshFilter;

        private void OnEnable()
        {
            _meshFilter = GetComponent<MeshFilter>();
            BuildMesh();
        }

        private void OnValidate()
        {
            // OnValidate 中は MeshFilter の書き換えが警告になるので、次の更新で作り直す
    #if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall += HandleDelayedRebuild;
    #endif
        }

        private void OnDisable()
        {
            DestroyMesh();
        }

    #if UNITY_EDITOR
        private void HandleDelayedRebuild()
        {
            if (this == null || !isActiveAndEnabled)
            {
                return;
            }
            BuildMesh();
        }
    #endif

        private void BuildMesh()
        {
            if (_meshFilter == null)
            {
                _meshFilter = GetComponent<MeshFilter>();
            }

            int resolution = Mathf.Clamp(_resolution, MinResolution, MaxResolution);
            int lineCount = resolution + 1;
            int vertexCount = lineCount * lineCount;
            var vertices = new Vector3[vertexCount];
            var uvs = new Vector2[vertexCount];
            var normals = new Vector3[vertexCount];
            var indices = new int[resolution * resolution * 6];

            float half = _size * 0.5f;
            for (int z = 0; z < lineCount; z++)
            {
                for (int x = 0; x < lineCount; x++)
                {
                    int i = z * lineCount + x;
                    float u = (float)x / resolution;
                    float v = (float)z / resolution;
                    vertices[i] = new Vector3(u * _size - half, 0f, v * _size - half);
                    uvs[i] = new Vector2(u, v);
                    normals[i] = Vector3.up;
                }
            }

            int t = 0;
            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int i = z * lineCount + x;
                    // 上から見て時計回りにして、表面が上を向くようにする
                    indices[t++] = i;
                    indices[t++] = i + lineCount;
                    indices[t++] = i + 1;
                    indices[t++] = i + 1;
                    indices[t++] = i + lineCount;
                    indices[t++] = i + lineCount + 1;
                }
            }

            DestroyMesh();
            _mesh = new Mesh
            {
                name = "WaterGrid",
                // シーンやプレハブに書き出さない(毎回ここで作り直すため)
                hideFlags = HideFlags.DontSave,
                indexFormat = vertexCount > MaxVertexCountFor16BitIndex ? IndexFormat.UInt32 : IndexFormat.UInt16,
            };
            _mesh.vertices = vertices;
            _mesh.uv = uvs;
            _mesh.normals = normals;
            _mesh.triangles = indices;
            _mesh.bounds = new Bounds(Vector3.zero, new Vector3(_size, BoundsHeightMargin * 2f, _size));
            _meshFilter.sharedMesh = _mesh;
        }

        private void DestroyMesh()
        {
            if (_mesh == null)
            {
                return;
            }
            if (_meshFilter != null && _meshFilter.sharedMesh == _mesh)
            {
                _meshFilter.sharedMesh = null;
            }
            if (Application.isPlaying)
            {
                Destroy(_mesh);
            }
            else
            {
                DestroyImmediate(_mesh);
            }
            _mesh = null;
        }
    }
}
