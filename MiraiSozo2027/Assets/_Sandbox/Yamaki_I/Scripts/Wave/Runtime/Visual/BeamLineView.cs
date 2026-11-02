using System.Collections.Generic;
using Aura.Inspector;
using UnityEngine;

namespace Aura.Wave
{
    /// @brief ビーム（点と線）の今の見た目。実体の線に沿って、揺れる線（InfoBeamLine）を流す
    /// WaveEmitter と同じ GameObject に付ける。付いていなければ、WaveEmitter が再生を始めるときに足す
    /// 色・波長・振幅・流れる速さ・太さは、波の種類（WaveType）から読む
    /// 「見せ方」を「見える範囲の中だけ」にすると、見せる範囲（WaveRevealSource）の中にある部分だけを描く（エコーロケーション中だけ波が見える、など）
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
                 "　エコーロケーション中だけ波が見えるようにするには、エコーを出す物（Test_Player の EchoController）に EchoWaveRevealSource（Scripts/Wave/Echo）を付け、ここを「見える範囲の中だけ」にする\n" +
                 "当たり判定と、受け手に波を渡すことは、見せ方に関係なく行う")]
        [SerializeField] private BeamLineVisibility _visibility = BeamLineVisibility.Always;  ///< 見せ方

        private readonly List<WaveRevealVolume> _revealVolumes = new List<WaveRevealVolume>();  ///< このフレームの見せる範囲（「見える範囲の中だけ」のとき）
        private InfoBeamLine _line;

        /// @brief 見せ方。実行中に変えてもよい
        public BeamLineVisibility Visibility
        {
            get => _visibility;
            set => _visibility = value;
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
            _line.Draw(frame.Corners, frame.FadingCorners, frame.Up, frame.Type, phase);
        }

        public void HideBeam()
        {
            if (_line != null)
            {
                _line.Hide();
            }
        }

        private void OnDisable()
        {
            HideBeam();
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
