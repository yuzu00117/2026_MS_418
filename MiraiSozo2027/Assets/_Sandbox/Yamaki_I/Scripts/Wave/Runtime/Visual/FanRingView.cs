using System.Collections.Generic;
using Aura.Inspector;
using UnityEngine;

namespace Aura.Wave
{
    /// @brief 扇（近距離）の 1 発の今の見た目。撃った位置から輪を順に出し、広げながら進め、届く距離を越えたら消す（FanWaveLine）
    /// 先頭の輪は波面と同じ位置を進むので、輪の先頭が届いた物に波が渡って見える
    /// WaveEmitter と同じ GameObject に付ける。付いていなければ、WaveEmitter が再生を始めるときに足す
    /// 色・太さは、波の種類（WaveType）から読む。前の 1 発の輪が残っている間に次を撃つと、両方の輪が出る
    [DisallowMultipleComponent]
    public sealed class FanRingView : MonoBehaviour, IFanWaveView
    {
        [InspectorLabel("線のマテリアル")]
        [Tooltip("扇の輪のマテリアル。空なら既定のマテリアルを使い、WaveType の「波の色」で色を付ける\n" +
                 "再生中に変えたときは、次に再生したときから効く")]
        [SerializeField] private Material _lineMaterial;  ///< 扇の輪のマテリアル。空なら既定のもの

        [InspectorLabel("輪の数")]
        [Tooltip("1 発で出す輪の数。輪の間隔は、届く距離（WaveEmitter の「扇の届く距離（m）」）をこの数で割ったもの\n" +
                 "多いほど詰まって見え、最後の輪が消えるまでが長くなる。実行中に変えてもよい")]
        [SerializeField, Range(1, 32)] private int _ringCount = FanWaveLine.DefaultRingCount;  ///< 1 発で出す輪の数

        [InspectorLabel("輪が薄れ始める位置（割合）")]
        [Tooltip("輪が薄れ始める位置。届く距離に対する割合（0〜1）。ここから先で薄れ、届く距離で消える\n" +
                 "例: 0.6 なら届く距離の 6 割から薄れる。1 なら薄れずに、届く距離で消える。0 なら出てすぐ薄れ始める。実行中に変えてもよい")]
        [SerializeField, Range(0.0f, 1.0f)] private float _farFadeStart = FanWaveLine.DefaultFarFadeStart;  ///< 輪が薄れ始める位置（届く距離に対する割合）

        [InspectorLabel("遠くの輪の太さ（倍）")]
        [Tooltip("届く距離まで進んだ輪の太さが、波の種類（WaveType）の「線の太さ（m）」の何倍か。進むほど、手元の 1 倍からこの倍率まで太くなる\n" +
                 "遠くの輪は小さく見えて細りやすいので、1 より大きくすると見やすい。1 なら太さは変わらない。実行中に変えてもよい")]
        [SerializeField, Range(0.0f, 5.0f)] private float _farWidthScale = FanWaveLine.DefaultFarWidthScale;  ///< 届く距離まで進んだ輪の太さ（線の太さの何倍か）

        private readonly List<RingShot> _shots = new List<RingShot>();  ///< 1 発ごとの輪（進んでいるものと、使い回すために取っておくもの）

        public void ShowFanShot(in FanShotInfo shot)
        {
            RingShot ringShot = GetIdleShot();
            ringShot.Info = shot;
            ringShot.IsActive = true;
        }

        public void ClearFanShots()
        {
            foreach (RingShot ringShot in _shots)
            {
                ringShot.IsActive = false;
                ringShot.Line.Hide();
            }
        }

        private void LateUpdate()
        {
            foreach (RingShot ringShot in _shots)
            {
                if (!ringShot.IsActive)
                {
                    continue;
                }
                FanShotInfo info = ringShot.Info;
                FanWaveLine line = ringShot.Line;
                line.RingCount = _ringCount;
                line.FarFadeStart = _farFadeStart;
                line.FarWidthScale = _farWidthScale;
                bool hasRingsLeft = line.DrawShot(info.Origin, info.Direction, info.Up, info.HalfAngle, info.Range, info.Type,
                    info.GetFrontDistance(Time.time));
                if (!hasRingsLeft)
                {
                    ringShot.IsActive = false;
                    line.Hide();
                }
            }
        }

        private void OnDisable()
        {
            ClearFanShots();
        }

        private void OnDestroy()
        {
            foreach (RingShot ringShot in _shots)
            {
                if (ringShot.Line != null)
                {
                    Destroy(ringShot.Line.gameObject);
                }
            }
        }

        /// @brief 輪が進んでいない 1 発を返す。すべて進んでいれば、新しく作る（輪の GameObject も 1 つ作る）
        private RingShot GetIdleShot()
        {
            foreach (RingShot ringShot in _shots)
            {
                if (!ringShot.IsActive)
                {
                    return ringShot;
                }
            }
            var created = new RingShot(FanWaveLine.Create("FanWave " + _shots.Count, transform, _lineMaterial));
            _shots.Add(created);
            return created;
        }

        /// @brief 1 発分の輪
        private sealed class RingShot
        {
            public RingShot(FanWaveLine line)
            {
                Line = line;
            }

            public FanWaveLine Line { get; }   ///< この 1 発の輪
            public FanShotInfo Info { get; set; }  ///< 撃った 1 発の情報
            public bool IsActive { get; set; }     ///< 輪が進んでいるか
        }
    }
}
