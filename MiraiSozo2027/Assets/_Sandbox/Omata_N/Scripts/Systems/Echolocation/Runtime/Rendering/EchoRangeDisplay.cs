// スキャン範囲の見た目だけを設定する。タグ判定や検出範囲そのものは変更しない。
using System.Collections.Generic;
using UnityEngine;

namespace Echo.Echolocation
{
    /// <summary>可視面の色と空間の境界線を設定する。描画は対象CameraのURPパスが行う。</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(EchoController))]
    public sealed class EchoRangeDisplay : MonoBehaviour
    {
        [SerializeField, Tooltip("床・壁など、見えている表面のスキャン済み部分を薄く色付けします。")]
        [UnityEngine.Serialization.FormerlySerializedAs("showSurface")]
        private bool _isSurfaceVisible = true;
        [SerializeField, Tooltip("球の扇形の経線・緯線と角度境界を描画します。")]
        [UnityEngine.Serialization.FormerlySerializedAs("showBoundary")]
        private bool _isBoundaryVisible = true;
        [SerializeField, Tooltip("スキャン範囲内の表面色。Aは不透明度です。")]
        [UnityEngine.Serialization.FormerlySerializedAs("fillColor")]
        private Color _fillColor = new Color(.05f, .7f, 1, .09f);
        [SerializeField, Tooltip("外側へ進む波の色。Aは不透明度です。")]
        [UnityEngine.Serialization.FormerlySerializedAs("waveColor")]
        private Color _waveColor = new Color(.05f, .9f, 1, .85f);
        [SerializeField, Tooltip("手前から消える波の色。Aは不透明度です。")]
        [UnityEngine.Serialization.FormerlySerializedAs("eraseColor")]
        private Color _eraseColor = new Color(1, .45f, .08f, .85f);
        [SerializeField, Tooltip("空間の境界線の色。Aは不透明度です。")]
        [UnityEngine.Serialization.FormerlySerializedAs("boundaryColor")]
        private Color _boundaryColor = new Color(.1f, .8f, 1, .32f);
        [SerializeField, Min(.01f), Tooltip("波の帯の幅（ワールド単位）。")]
        [UnityEngine.Serialization.FormerlySerializedAs("waveWidth")]
        private float _waveWidth = .3f;
        private Mesh _mesh;
        private float _builtAngle = -1;
        /// <summary>可視面の範囲表示の有効状態。</summary>
        public bool IsSurfaceVisible { get => _isSurfaceVisible; set => _isSurfaceVisible = value; }
        /// <summary>空間の境界線の有効状態。</summary>
        public bool IsBoundaryVisible { get => _isBoundaryVisible; set => _isBoundaryVisible = value; }
        /// <summary>範囲内の薄い表面色。</summary>
        public Color FillColor => _fillColor;
        /// <summary>拡張波の色。</summary>
        public Color WaveColor => _waveColor;
        /// <summary>消去波の色。</summary>
        public Color EraseColor => _eraseColor;
        /// <summary>空間境界線の色。</summary>
        public Color BoundaryColor => _boundaryColor;
        /// <summary>波の帯の幅（ワールド単位）。</summary>
        public float WaveWidth => _waveWidth;

        void OnValidate()
        {
            if (!float.IsFinite(_waveWidth) || _waveWidth < .01f)
                _waveWidth = .3f;
        }

        /// <summary>角度が変わった時だけ半径1の線メッシュを再構築する。半径変化は描画行列で処理する。</summary>
        public Mesh GetBoundaryMesh(float angle)
        {
            angle = Mathf.Clamp(angle, 0, 360);
            if (_mesh && _builtAngle == angle)
                return _mesh;
            ReleaseMesh();
            _builtAngle = angle;
            _mesh = new Mesh
            {
                name = "Echo range boundary",
                hideFlags = HideFlags.HideAndDontSave
            };
            var points = new List<Vector3>();
            var indices = new List<int>();
            float half = angle * .5f * Mathf.Deg2Rad;
            void Segment(Vector3 a, Vector3 b)
            {
                int i = points.Count;
                points.Add(a);
                points.Add(b);
                indices.Add(i);
                indices.Add(i + 1);
            }

            Vector3 Point(float polar, float azimuth) => new Vector3(Mathf.Sin(polar) * Mathf.Cos(azimuth), Mathf.Sin(polar) * Mathf.Sin(azimuth), Mathf.Cos(polar));
            // 半角は180度まで使えるため、270度や360度も実際の判定と同じ形になる。
            for (int j = 0; j < 8; j++)
            {
                float az = j * Mathf.PI / 4;
                for (int i = 0; i < 48; i++)
                    Segment(Point(half * i / 48, az), Point(half * (i + 1) / 48, az));
                if (angle < 360)
                    Segment(Vector3.zero, Point(half, az));
            }

            for (int j = 1; j <= 4; j++)
                for (int i = 0; i < 96; i++)
                    Segment(Point(half * j / 4, 2 * Mathf.PI * i / 96), Point(half * j / 4, 2 * Mathf.PI * (i + 1) / 96));
            _mesh.SetVertices(points);
            _mesh.SetIndices(indices, MeshTopology.Lines, 0);
            _mesh.RecalculateBounds();
            return _mesh;
        }

        void ReleaseMesh()
        {
            if (!_mesh)
                return;
            if (Application.isPlaying)
                Destroy(_mesh);
            else
                DestroyImmediate(_mesh);
            _mesh = null;
        }

        void OnDisable() => ReleaseMesh();
        void OnDestroy() => ReleaseMesh();
    }
}
