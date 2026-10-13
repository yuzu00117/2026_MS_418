using System;
using System.Collections.Generic;
using Aura.ObjectInfo;
using Aura.Wave;
using UnityEngine;
using UnityEngine.Events;

namespace Aura.ObjectInfoDemo
{
    /// @brief WaveReaction（反応する波の種類、動くまでに当てる時間）の情報を読んで、ビームを当て続けると起動する受け手
    /// 溜まる量は「ビームの強さ（BeamSourceInfo の Energy Per Second）× 当てた秒数 × 弱点の倍率」。
    /// これが WaveReaction の Required Seconds に届くと起動する（強さ 1 なら、ちょうど Required Seconds 秒で起動する）
    /// 起動したときの動き（扉を開ける、電気をつけるなど）は On Activated につなぐ
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WaveReaction))]
    public sealed class ReactionTrigger : MonoBehaviour
    {
        private const float ReceivingGraceTime = 0.1f;  ///< 最後に受けてからこの時間（秒）は、受けている最中とみなす

        // WaveReaction から受け手を引くための表。ビームの側が毎フレーム GetComponent を呼ばずに済むようにする
        private static readonly Dictionary<WaveReaction, ReactionTrigger> TriggersByReaction =
            new Dictionary<WaveReaction, ReactionTrigger>();

        [Tooltip("溜まりきったときの動き\n" +
                 "Latch: 起動したら、ずっと起動したまま（扉を開けるなど）\n" +
                 "Toggle: 起動するたびに ON と OFF が入れ替わる。ビームが一度途切れるまで次を数えない（スイッチ、電気など）\n" +
                 "Sustain: 起動した後、溜まった分が 0 まで減ると止まる。当てている間だけ動くギミックに使う（リフトなど）")]
        [SerializeField] private WaveActivationMode _mode = WaveActivationMode.Latch;  ///< 起動のしかた

        [Tooltip("ビームを受けていない間に、1 秒あたりに減る量（溜まった秒数で数える）。0 なら減らず、溜めた分が残る\n" +
                 "Sustain では、当てるのをやめてから止まるまでの時間がこれで決まる（Required Seconds ÷ これ）\n" +
                 "Latch で起動した後は減らない")]
        [SerializeField, Min(0.0f)] private float _decayPerSecond = 0.5f;  ///< ビームを受けていない間に 1 秒あたりに減る量

        [Tooltip("起動したときに呼ぶ処理。扉を開ける、電気をつける、リフトを上げるなど")]
        [SerializeField] private UnityEvent _onActivated = new UnityEvent();    ///< 起動した後に呼ぶ

        [Tooltip("止まったときに呼ぶ処理。Toggle で OFF になったときと、Sustain で溜まった分が 0 になったときに呼ばれる")]
        [SerializeField] private UnityEvent _onDeactivated = new UnityEvent();  ///< 止まった後に呼ぶ

        private WaveReaction _reaction;
        private float _charge;           ///< 溜まった量（秒で数える）
        private float _lastReceiveTime = float.NegativeInfinity;  ///< 最後に反応する種類のビームを受けた時刻
        private bool _isWaitingRelease;  ///< Toggle で切り替えた後、ビームが途切れるのを待っているか

        /// @brief 反応する種類のビームを受けた後に起きる
        public event Action<InfoBeamHit> BeamAccepted;

        /// @brief 反応しない種類のビームが当たった後に起きる
        public event Action<InfoBeamHit> BeamRejected;

        /// @brief 起動しているか
        public bool IsActive { get; private set; }

        /// @brief 反応する種類のビームを受けている最中か
        public bool IsReceiving => Time.time - _lastReceiveTime <= ReceivingGraceTime;

        /// @brief 起動のしかた
        public WaveActivationMode Mode => _mode;

        /// @brief 起動に要る量（WaveReaction の Required Seconds）に対する、溜まっている割合（0〜1）
        public float ChargeRatio
        {
            get
            {
                float required = Reaction.RequiredSeconds;
                return required > 0.0f ? Mathf.Clamp01(_charge / required) : (IsActive ? 1.0f : 0.0f);
            }
        }

        /// @brief 読んでいる WaveReaction
        public WaveReaction Reaction => _reaction != null ? _reaction : _reaction = GetComponent<WaveReaction>();

        //============================================================
        // 公開の操作
        //============================================================

        /// @brief WaveReaction に付いた受け手を探す（ビームの側が、当たった物の WaveReaction から引く）
        /// @param reaction 当たった物の WaveReaction
        /// @param outTrigger 見つかった受け手。無ければ null
        /// @return 有効な受け手があれば true
        public static bool TryGetTrigger(WaveReaction reaction, out ReactionTrigger outTrigger)
        {
            outTrigger = null;
            return reaction != null && TriggersByReaction.TryGetValue(reaction, out outTrigger);
        }

        /// @brief ビームを受ける。反応する種類なら溜め、溜まったら起動する
        /// @param hit 当たりの情報
        /// @return 反応する種類なら true
        public bool ReceiveBeam(in InfoBeamHit hit)
        {
            if (!isActiveAndEnabled)
            {
                return false;
            }
            if (!Reaction.CanReactTo(hit.Type))
            {
                BeamRejected?.Invoke(hit);
                return false;
            }

            _lastReceiveTime = Time.time;
            BeamAccepted?.Invoke(hit);

            // Latch で起動済み、または Toggle でビームが途切れるのを待っている間は溜めない
            bool isLatched = _mode == WaveActivationMode.Latch && IsActive;
            if (isLatched || _isWaitingRelease)
            {
                return true;
            }

            float required = Reaction.RequiredSeconds;
            _charge = Mathf.Min(_charge + hit.Energy, required);
            if (_charge >= required)
            {
                Trigger();
            }
            return true;
        }

        //============================================================
        // Unity のメッセージ
        //============================================================

        private void OnEnable()
        {
            TriggersByReaction[Reaction] = this;
        }

        private void OnDisable()
        {
            if (TriggersByReaction.TryGetValue(Reaction, out ReactionTrigger registered) && registered == this)
            {
                TriggersByReaction.Remove(Reaction);
            }
        }

        private void Update()
        {
            if (IsReceiving)
            {
                return;
            }

            _isWaitingRelease = false;
            if (_mode == WaveActivationMode.Latch && IsActive)
            {
                return;
            }

            _charge = Mathf.Max(0.0f, _charge - _decayPerSecond * Time.deltaTime);
            if (_mode == WaveActivationMode.Sustain && IsActive && _charge <= 0.0f)
            {
                SetActiveState(false);
            }
        }

        // domain reload を切っていると、前の再生の表が残るので、再生を始めるたびに空にする
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            TriggersByReaction.Clear();
        }

        //============================================================
        // 内部の処理
        //============================================================

        private void Trigger()
        {
            switch (_mode)
            {
                case WaveActivationMode.Latch:
                case WaveActivationMode.Sustain:
                    SetActiveState(true);
                    break;
                case WaveActivationMode.Toggle:
                    _charge = 0.0f;
                    _isWaitingRelease = true;
                    SetActiveState(!IsActive);
                    break;
            }
        }

        private void SetActiveState(bool isActive)
        {
            if (IsActive == isActive)
            {
                return;
            }
            IsActive = isActive;
            if (isActive)
            {
                _onActivated.Invoke();
            }
            else
            {
                _onDeactivated.Invoke();
            }
        }
    }
}
