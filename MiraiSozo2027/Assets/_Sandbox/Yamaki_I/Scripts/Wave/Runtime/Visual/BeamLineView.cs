using Aura.Inspector;
using UnityEngine;

namespace Aura.Wave
{
    /// @brief ビーム（点と線）の今の見た目。実体の線に沿って、揺れる線（InfoBeamLine）を流す
    /// WaveEmitter と同じ GameObject に付ける。付いていなければ、WaveEmitter が再生を始めるときに足す
    /// 色・波長・振幅・流れる速さ・太さは、波の種類（WaveType）から読む
    [DisallowMultipleComponent]
    public sealed class BeamLineView : MonoBehaviour, IBeamWaveView
    {
        [InspectorLabel("線のマテリアル")]
        [Tooltip("ビームの線のマテリアル。空なら既定のマテリアルを使い、WaveType の「波の色」で色を付ける\n" +
                 "再生中に変えたときは、次に再生したときから効く")]
        [SerializeField] private Material _lineMaterial;  ///< ビームの線のマテリアル。空なら既定のもの

        private InfoBeamLine _line;

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
