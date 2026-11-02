using System.Collections.Generic;
using Aura.Inspector;
using Echo.Echolocation;
using UnityEngine;

namespace Aura.Wave.Echo
{
    /// @brief エコーロケーション（Omata_N の EchoController）の今の範囲を、波の見た目を見せる範囲（WaveRevealSource）に毎フレーム写す
    /// EchoController と同じ GameObject（チームの Test_Player）に付け、見せたいビームの BeamLineView の「見せ方」を「見える範囲の中だけ」にすると、
    /// エコーの波が届いている所だけビームが見える（ふだんは見えない）。当たり判定と、受け手に波を渡すことは変わらない
    /// アセンブリ Aura.Wave.Echo だけが Echo.Runtime（Omata_N）を参照する。Scripts/Wave の本体（Aura.Wave）はエコーを知らないので、
    /// エコーの無いプロジェクトへ移すときは、Scripts/Wave/Echo を外す
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WaveRevealSource))]
    [DefaultExecutionOrder(1001)]  // EchoController（1000）が LateUpdate で範囲を更新した後に写す
    public sealed class EchoWaveRevealSource : MonoBehaviour
    {
        private const float WholeBeamRadius = 1.0e6f;  ///< 「範囲で切らずに全部見せる」のときの、ビーム全体を覆う半径（m）

        [InspectorLabel("エコーロケーション")]
        [Tooltip("範囲を読む EchoController。空なら、この GameObject か親に付いたものを使う")]
        [SerializeField] private EchoController _controller;  ///< 範囲を読む EchoController。空なら自分か親から探す

        [InspectorLabel("範囲で切らずに全部見せる")]
        [Tooltip("オンにすると、エコーを出している間（消えていく間も）、範囲で切らずにビームを全部見せる\n" +
                 "オフなら、エコーの波が届いている範囲（球の一部）に入っている部分だけを見せる。ソナーでは、波の輪ごとに見える")]
        [SerializeField] private bool _isWholeBeamShown;  ///< エコー中は、範囲で切らずに全部見せるか

        private readonly List<WaveRevealVolume> _volumes = new List<WaveRevealVolume>();  ///< このフレームのエコーの範囲
        private WaveRevealSource _source;

        /// @brief 範囲を読む EchoController
        public EchoController Controller
        {
            get => _controller;
            set => _controller = value;
        }

        /// @brief エコー中は、範囲で切らずに全部見せるか
        public bool IsWholeBeamShown
        {
            get => _isWholeBeamShown;
            set => _isWholeBeamShown = value;
        }

        private void Awake()
        {
            _source = GetComponent<WaveRevealSource>();
            if (_controller == null)
            {
                _controller = GetComponentInParent<EchoController>();
            }
        }

        private void OnDisable()
        {
            if (_source != null)
            {
                _source.ClearVolumes();
            }
        }

        private void LateUpdate()
        {
            _volumes.Clear();
            if (_controller != null && _controller.isActiveAndEnabled)
            {
                IReadOnlyList<EchoFrameData> frames = _controller.Frames;
                for (int i = 0; i < frames.Count; ++i)
                {
                    EchoFrameData frame = frames[i];
                    if (frame.State == EchoState.Hidden)
                    {
                        continue;
                    }
                    if (_isWholeBeamShown)
                    {
                        _volumes.Add(WaveRevealVolume.Sphere(frame.Origin, WholeBeamRadius));
                        break;
                    }
                    // 消えていく間は、内側の消えた分（EraseRadius）を見せない（エコーの表示と同じ）
                    float innerRadius = frame.State == EchoState.Hiding ? frame.EraseRadius : 0.0f;
                    _volumes.Add(new WaveRevealVolume(frame.Origin, frame.Forward, frame.Angle, frame.OuterRadius, innerRadius));
                }
            }
            _source.SetVolumes(_volumes);
        }
    }
}
