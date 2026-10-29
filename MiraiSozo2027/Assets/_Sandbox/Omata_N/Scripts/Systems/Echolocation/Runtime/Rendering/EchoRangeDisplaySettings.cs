// スキャン範囲の見た目だけを設定する。タグ判定や検出範囲そのものは変更しない。
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Echo.Echolocation
{
    /// <summary>可視面の色と空間の境界線。EchoControllerが保持し、描画は対象CameraのURPパスが行う。</summary>
    [Serializable]
    public sealed class EchoRangeDisplaySettings
    {
        private const float DefaultWaveWidth = .3f;
        private const float MinimumWaveWidth = .01f;
        private const float FullCircleDegrees = 360;
        // 境界線メッシュの分割数。経線は扇形の稜線、緯線は等間隔の輪。
        private const int MeridianCount = 8;
        private const int MeridianSegments = 48;
        private const int LatitudeCount = 4;
        private const int LatitudeSegments = 96;
        // 統合前のControllerは範囲表示を持たないため、既存シーンでは無効のまま読み込む。
        [SerializeField, Tooltip("スキャン範囲の色と境界線を表示します。")]
        private bool _isEnabled;
        [SerializeField, Tooltip("床・壁など、見えている表面のスキャン済み部分を薄く色付けします。")]
        private bool _isSurfaceVisible = true;
        [SerializeField, Tooltip("球の扇形の経線・緯線と角度境界を描画します。")]
        private bool _isBoundaryVisible = true;
        [SerializeField, Tooltip("スキャン範囲内の表面色。Aは不透明度です。")]
        private Color _fillColor = new Color(.05f, .7f, 1, .09f);
        [SerializeField, Tooltip("外側へ進む波の色。Aは不透明度です。")]
        private Color _waveColor = new Color(.05f, .9f, 1, .85f);
        [SerializeField, Tooltip("手前から消える波の色。Aは不透明度です。")]
        private Color _eraseColor = new Color(1, .45f, .08f, .85f);
        [SerializeField, Tooltip("空間の境界線の色。Aは不透明度です。")]
        private Color _boundaryColor = new Color(.1f, .8f, 1, .32f);
        [SerializeField, Min(MinimumWaveWidth), Tooltip("波の帯の幅（ワールド単位）。")]
        private float _waveWidth = DefaultWaveWidth;
        [NonSerialized] private Mesh _mesh;
        [NonSerialized] private float _builtAngle = -1;

        /// <summary>範囲表示全体の有効状態。</summary>
        public bool IsEnabled { get => _isEnabled; set => _isEnabled = value; }
        /// <summary>可視面の範囲表示の有効状態。</summary>
        public bool IsSurfaceVisible { get => _isSurfaceVisible; set => _isSurfaceVisible = value; }
        /// <summary>空間の境界線の有効状態。</summary>
        public bool IsBoundaryVisible { get => _isBoundaryVisible; set => _isBoundaryVisible = value; }
        /// <summary>範囲内の薄い表面色。</summary>
        public Color FillColor { get => _fillColor; set => _fillColor = value; }
        /// <summary>拡張波の色。</summary>
        public Color WaveColor { get => _waveColor; set => _waveColor = value; }
        /// <summary>消去波の色。</summary>
        public Color EraseColor { get => _eraseColor; set => _eraseColor = value; }
        /// <summary>空間境界線の色。</summary>
        public Color BoundaryColor { get => _boundaryColor; set => _boundaryColor = value; }
        /// <summary>波の帯の幅（ワールド単位）。</summary>
        public float WaveWidth { get => _waveWidth; set => _waveWidth = value; }

        public void Validate()
        {
            if (!float.IsFinite(_waveWidth) || _waveWidth < MinimumWaveWidth)
                _waveWidth = DefaultWaveWidth;
        }

        /// <summary>角度が変わった時だけ半径1の線メッシュを再構築する。半径変化は描画行列で処理する。</summary>
        public Mesh GetBoundaryMesh(float angle)
        {
            angle = Mathf.Clamp(angle, 0, FullCircleDegrees);
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
            void AddSegment(Vector3 start, Vector3 end)
            {
                int i = points.Count;
                points.Add(start);
                points.Add(end);
                indices.Add(i);
                indices.Add(i + 1);
            }

            Vector3 GetPoint(float polar, float azimuth) => new Vector3(Mathf.Sin(polar) * Mathf.Cos(azimuth), Mathf.Sin(polar) * Mathf.Sin(azimuth), Mathf.Cos(polar));
            // 半角は180度まで使えるため、270度や360度も実際の判定と同じ形になる。
            for (int meridian = 0; meridian < MeridianCount; meridian++)
            {
                float azimuth = meridian * 2 * Mathf.PI / MeridianCount;
                for (int i = 0; i < MeridianSegments; i++)
                    AddSegment(GetPoint(half * i / MeridianSegments, azimuth), GetPoint(half * (i + 1) / MeridianSegments, azimuth));
                if (angle < FullCircleDegrees)
                    AddSegment(Vector3.zero, GetPoint(half, azimuth));
            }

            for (int latitude = 1; latitude <= LatitudeCount; latitude++)
                for (int i = 0; i < LatitudeSegments; i++)
                    AddSegment(GetPoint(half * latitude / LatitudeCount, 2 * Mathf.PI * i / LatitudeSegments),
                        GetPoint(half * latitude / LatitudeCount, 2 * Mathf.PI * (i + 1) / LatitudeSegments));
            _mesh.SetVertices(points);
            _mesh.SetIndices(indices, MeshTopology.Lines, 0);
            _mesh.RecalculateBounds();
            return _mesh;
        }

        public void ReleaseMesh()
        {
            if (!_mesh)
                return;
            if (Application.isPlaying)
                UnityEngine.Object.Destroy(_mesh);
            else
                UnityEngine.Object.DestroyImmediate(_mesh);
            _mesh = null;
        }
    }
}
