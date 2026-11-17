using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Aura.Water
{
    /// <summary>
    /// 水面シェーダー(ToonWater)用に、マス目に分割した平らな板を作って MeshFilter に入れる。
    /// Unity 標準の Plane は縦横 10 マスしかなく、頂点を動かす波がカクカクになるため、板を自分で作る。
    /// カメラはステージの上しか動かないので、板の中心(ステージ)付近だけマス目を細かくし、外側ほど粗くする。
    /// 全体を同じ細かさにすると、見分けられない遠くの細かさに処理を使ってしまうため。
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(MeshFilter))]
    public sealed class WaterGridMesh : MonoBehaviour
    {
        private const float MinDetailCellSize = 0.1f;
        // 細かい範囲の外で、1マス進むごとにマス目を広げる倍率。
        // 大きくするほど軽くなるが、細かさの変わり目で波の形が急に変わって見えやすくなる
        private const float OuterCellGrowth = 1.1f;
        // 1辺の線の数の上限。これを超えると頂点が 100 万個を超え、作り直しのたびにエディターが固まるため
        private const int MaxLineCountPerAxis = 1001;
        private const int MaxVertexCountFor16BitIndex = 65535;
        // 波で頂点が動いても板が画面外と誤判定されて消えないよう、描画範囲の高さに足す余裕(m)
        private const float BoundsHeightMargin = 5f;

        [SerializeField, Min(1f), Tooltip("板全体の一辺の長さ(m)")]
        private float _size = 1000f;
        [SerializeField, Min(0f), Tooltip("マス目を細かくする範囲の一辺の長さ(m)。ステージが収まる広さにする")]
        private float _detailAreaSize = 100f;
        [SerializeField, Min(MinDetailCellSize), Tooltip("細かくする範囲のマス目の幅(m)。小さいほど細かい波まで出るが重くなる")]
        private float _detailCellSize = 0.5f;

        private Mesh _mesh;
        private MeshFilter _meshFilter;

        private void OnEnable()
        {
            _meshFilter = GetComponent<MeshFilter>();
            BuildMesh();
        }

        private void OnValidate()
        {
            // OnValidate 中は MeshFilter の書き換えが警告になるので、次の更新で作り直す。
            // 値を続けて変えると予約が重なり何度も作り直すため、先に外してから 1 つだけ登録する
#if UNITY_EDITOR
            UnityEditor.EditorApplication.delayCall -= HandleDelayedRebuild;
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

            // 縦と横で同じ並びの線を使う。こうするとマス目は必ず四角の格子になり、
            // 細かさの違う所の境目に隙間(頂点のずれで開く穴)ができない
            List<float> lines = BuildLinePositions();
            int lineCount = lines.Count;
            if (lineCount > MaxLineCountPerAxis)
            {
                Debug.LogWarning(
                    $"{nameof(WaterGridMesh)}: マス目が多すぎるため水面を作りませんでした(1辺 {lineCount} 本、上限 {MaxLineCountPerAxis} 本)。" +
                    "細かくする範囲を狭くするか、マス目の幅を大きくしてください。", this);
                DestroyMesh();
                return;
            }

            int cellCount = lineCount - 1;
            int vertexCount = lineCount * lineCount;
            var vertices = new Vector3[vertexCount];
            var normals = new Vector3[vertexCount];
            var indices = new int[cellCount * cellCount * 6];

            // UV は作らない。シェーダーは模様をワールド座標で決めるため、使われないので
            for (int z = 0; z < lineCount; z++)
            {
                for (int x = 0; x < lineCount; x++)
                {
                    int i = z * lineCount + x;
                    vertices[i] = new Vector3(lines[x], 0f, lines[z]);
                    normals[i] = Vector3.up;
                }
            }

            int t = 0;
            for (int z = 0; z < cellCount; z++)
            {
                for (int x = 0; x < cellCount; x++)
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
            _mesh.normals = normals;
            _mesh.triangles = indices;
            _mesh.bounds = new Bounds(Vector3.zero, new Vector3(_size, BoundsHeightMargin * 2f, _size));
            _meshFilter.sharedMesh = _mesh;
        }

        /// <summary>
        /// 板の中心から端までの線の位置を決め、左右対称に並べて返す(小さい順)。
        /// 中心から細かくする範囲の端までは同じ幅、その先は 1 マスごとに幅を広げる。
        /// </summary>
        private List<float> BuildLinePositions()
        {
            float half = _size * 0.5f;
            float detailHalf = Mathf.Min(_detailAreaSize * 0.5f, half);
            float cellSize = Mathf.Max(_detailCellSize, MinDetailCellSize);

            var positive = new List<float>();
            float position = 0f;
            float step = cellSize;
            // 上限を超えた時点で打ち切る。呼び出し側で上限超えとして扱うので、正確な本数は要らない
            while (position < half && positive.Count < MaxLineCountPerAxis)
            {
                if (position >= detailHalf)
                {
                    step *= OuterCellGrowth;
                }
                position += step;
                positive.Add(Mathf.Min(position, half));
            }

            // 端の手前に極端に細いマスが残ると、そこだけ三角形がつぶれるので、1つ手前の線とまとめる
            int count = positive.Count;
            if (count >= 2 && half - positive[count - 2] < step * 0.5f)
            {
                positive.RemoveAt(count - 2);
            }

            var lines = new List<float>(positive.Count * 2 + 1);
            for (int i = positive.Count - 1; i >= 0; i--)
            {
                lines.Add(-positive[i]);
            }
            lines.Add(0f);
            lines.AddRange(positive);
            return lines;
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
