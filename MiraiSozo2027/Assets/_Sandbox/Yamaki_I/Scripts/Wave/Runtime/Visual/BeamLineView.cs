using System.Collections.Generic;
using Aura.Inspector;
using UnityEngine;

namespace Aura.Wave
{
    /// @brief ビーム（点と線）の今の見た目。実体の線に沿って、揺れる線（InfoBeamLine）を流す
    /// WaveEmitter と同じ GameObject に付ける。付いていなければ、WaveEmitter が再生を始めるときに足す
    /// 色・波長・振幅・流れる速さ・太さは、波の種類（WaveType）から読む
    /// 「見せ方」を「見える範囲の中だけ」にすると、見せる範囲（WaveRevealSource）の中にある部分だけを描く（エコーロケーション中だけ波が見える、など）
    /// 「範囲に触れたら全体を見せる」にすると、線のどこかが見せる範囲に触れたときに線の全体を描き、触れなくなってからも決めた秒数だけ描き続ける
    /// 見せ方は見た目だけを変える。当たり判定（実体の点と線）と、受け手に波を渡すことは変わらない
    [DisallowMultipleComponent]
    public sealed class BeamLineView : MonoBehaviour, IBeamWaveView
    {
        [InspectorLabel("線のマテリアル")]
        [Tooltip("ビームの線のマテリアル。空なら既定のマテリアルを使い、WaveType の「波の色」で色を付ける\n" +
                 "再生中に変えたときは、次に再生したときから効く")]
        [SerializeField] private Material _lineMaterial;  ///< ビームの線のマテリアル。空なら既定のもの

        [InspectorLabel("見せ方")]
        [Tooltip("ビームの線をいつ見せるか。実行中に変えてもよい\n" +
                 "いつも見せる: 出している間ずっと見せる\n" +
                 "見える範囲の中だけ: シーンにある有効な WaveRevealSource の範囲（球の一部）に入っている部分だけを見せる。範囲が 1 つも無ければ見せない\n" +
                 "範囲に触れたら全体を見せる: 線のどこかが WaveRevealSource の範囲に触れたら、線の全体を見せる。触れなくなってからも「触れた後も見せる秒数」の間は見せる\n" +
                 "　エコーロケーションで波が見えるようにするには、エコーを出す物（Test_Player の EchoController）に EchoWaveRevealSource（Scripts/Wave/Echo）を付け、ここを「見える範囲の中だけ」か「範囲に触れたら全体を見せる」にする\n" +
                 "当たり判定と、受け手に波を渡すことは、見せ方に関係なく行う")]
        [SerializeField] private BeamLineVisibility _visibility = BeamLineVisibility.Always;  ///< 見せ方

        [InspectorLabel("触れた後も見せる秒数")]
        [Tooltip("「範囲に触れたら全体を見せる」のとき、線が範囲に触れなくなってから、全体を見せ続ける秒数。0 なら触れている間だけ見せる")]
        [SerializeField, Min(0.0f)] private float _revealHoldSeconds = DefaultRevealHoldSeconds;  ///< 範囲に触れなくなってから全体を見せ続ける秒数

        private const float DefaultRevealHoldSeconds = 2.0f;  ///< 「触れた後も見せる秒数」の初期値（エコーの専用タグの残存時間と同じくらい）
        private const float TouchSampleSpacing = 0.25f;       ///< 範囲に触れたかを調べるとき、線の上で調べる点の間隔（m）

        private readonly List<WaveRevealVolume> _revealVolumes = new List<WaveRevealVolume>();  ///< このフレームの見せる範囲（「見える範囲の中だけ」「範囲に触れたら全体を見せる」のとき）
        private float _revealedUntil = float.NegativeInfinity;  ///< 「範囲に触れたら全体を見せる」で、全体を見せる最後の時刻
        private InfoBeamLine _line;

        /// @brief 見せ方。実行中に変えてもよい
        public BeamLineVisibility Visibility
        {
            get => _visibility;
            set => _visibility = value;
        }

        /// @brief 「範囲に触れたら全体を見せる」のとき、範囲に触れなくなってから全体を見せ続ける秒数
        public float RevealHoldSeconds
        {
            get => _revealHoldSeconds;
            set => _revealHoldSeconds = Mathf.Max(0.0f, value);
        }

        public void DrawBeam(in BeamWaveFrame frame)
        {
            if (frame.Type == null)
            {
                HideBeam();
                return;
            }
            if (_line == null)
            {
                _line = InfoBeamLine.Create("InfoBeam", transform, _lineMaterial);
            }
            float phase = Time.time * frame.Type.ScrollSpeed;
            if (_visibility == BeamLineVisibility.InRevealVolumes)
            {
                // 見せる範囲の中にある部分だけを描く（範囲が無ければ、空の一覧を渡して消す）
                WaveRevealSource.CollectAll(_revealVolumes);
                _line.Draw(frame.Corners, frame.FadingCorners, frame.Up, frame.Type, phase, _revealVolumes);
                return;
            }
            if (_visibility == BeamLineVisibility.WholeWhenTouched)
            {
                // 線のどこかが見せる範囲に触れたら、そこから「触れた後も見せる秒数」の間は全体を描く
                WaveRevealSource.CollectAll(_revealVolumes);
                if (TouchesAny(frame.Corners, _revealVolumes))
                {
                    _revealedUntil = Time.time + _revealHoldSeconds;
                }
                if (Time.time > _revealedUntil)
                {
                    _line.Hide();
                    return;
                }
            }
            _line.Draw(frame.Corners, frame.FadingCorners, frame.Up, frame.Type, phase);
        }

        public void HideBeam()
        {
            if (_line != null)
            {
                _line.Hide();
            }
            // ビームが止まったら「触れた後も見せる秒数」も終わりにする（すぐ撃ち直したとき、範囲に触れていないのに全体が見えないように）
            _revealedUntil = float.NegativeInfinity;
        }

        private void OnDisable()
        {
            HideBeam();
        }

        /// @brief 折れ線のどこかが、範囲のどれかに入っているか
        /// 点だけでなく、点の間の直線も TouchSampleSpacing ごとに調べる（エコーの帯が点の間を通り過ぎても見落とさない）
        /// @param corners 折れ線の点
        /// @param volumes 範囲
        private static bool TouchesAny(IReadOnlyList<Vector3> corners, List<WaveRevealVolume> volumes)
        {
            if (corners == null || volumes.Count == 0)
            {
                return false;
            }
            for (int i = 0; i < corners.Count; ++i)
            {
                if (ContainsAny(volumes, corners[i]))
                {
                    return true;
                }
                if (i + 1 >= corners.Count)
                {
                    continue;
                }
                Vector3 start = corners[i];
                Vector3 end = corners[i + 1];
                int steps = Mathf.CeilToInt(Vector3.Distance(start, end) / TouchSampleSpacing);
                for (int step = 1; step < steps; ++step)
                {
                    if (ContainsAny(volumes, Vector3.Lerp(start, end, (float)step / steps)))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// @brief その位置が、範囲のどれかに入っているか
        private static bool ContainsAny(List<WaveRevealVolume> volumes, Vector3 point)
        {
            for (int i = 0; i < volumes.Count; ++i)
            {
                if (volumes[i].Contains(point))
                {
                    return true;
                }
            }
            return false;
        }

        private void OnDestroy()
        {
            if (_line != null)
            {
                Destroy(_line.gameObject);
            }
        }
    }
}
