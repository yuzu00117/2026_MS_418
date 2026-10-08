using System;
using UnityEngine;
using UnityEngine.Events;

namespace Aura.Wave
{
    /// @brief 決めた種類の波を受けるとエネルギーを溜め、溜まると起動する受け手
    /// マイク（音波）やアンテナ（電波）の GameObject に付け、On Activated にギミックの処理をつなぐ
    public sealed class WaveReceiver : MonoBehaviour, IWaveReceiver
    {
        private const float ReceivingGraceTime = 0.1f;  ///< 最後に受けてからこの時間（秒）は、受けている最中とみなす

        [Header("反応する波")]
        [Tooltip("反応する波の種類（WaveType のアセット）。ここにない種類の波が当たっても反応しない（WaveReceiverFeedback を付けていれば赤く光る）")]
        [SerializeField] private WaveType[] _acceptedTypes;                ///< 反応する波の種類

        [Tooltip("オンにすると、Accepted Types に関係なく、どの種類の波にも反応する")]
        [SerializeField] private bool _canAcceptAnyType;                   ///< true なら種類に関係なく反応する

        [Header("起動")]
        [Tooltip("エネルギーが Required Energy まで溜まったときの動き\n" +
                 "Latch: 起動したら、ずっと起動したまま（扉を開けるなど）\n" +
                 "Toggle: 起動するたびに ON と OFF が入れ替わる。ビームは一度途切れるまで次を数えない（スイッチなど）\n" +
                 "Sustain: 起動した後、エネルギーが 0 まで減ると止まる。当てている間だけ動くギミックに使う（リフトなど）")]
        [SerializeField] private WaveActivationMode _mode = WaveActivationMode.Latch;  ///< 起動のしかた

        [Tooltip("起動に要るエネルギー。単発は 1 発ごとに WaveEmitter の Pulse Energy（既定 1）、ビームは当てている間 1 秒ごとに Beam Energy Per Second（既定 1）溜まる\n" +
                 "例: 3 にすると、ビームなら 3 秒で起動する。単発なら Decay Per Second が 0 のとき 3 発。既定の 0.5 では撃つ間にも減るので、最速で撃っても 4 発要る")]
        [SerializeField, Min(0.001f)] private float _requiredEnergy = 1.0f;  ///< 起動に要るエネルギー。単発 1 発は既定で 1

        [Tooltip("波を受けていない間に、1 秒あたりに減るエネルギー。0 なら減らず、溜めた分が残る\n" +
                 "単発を何発か当てて溜めるときは、撃つ間にも減る（間を空けすぎると溜まらない）\n" +
                 "Sustain では、当てるのをやめてから止まるまでの時間がこれで決まる（Required Energy ÷ これ。例: 1.5 ÷ 0.5 で 3 秒）\n" +
                 "Latch で起動した後は減らない")]
        [SerializeField, Min(0.0f)] private float _decayPerSecond = 0.5f;  ///< 波を受けていない間に 1 秒あたりに減るエネルギー

        [Header("イベント")]
        [Tooltip("起動したときに呼ぶ処理。扉を開ける、リフトを上げるなど、ギミックの処理をつなぐ")]
        [SerializeField] private UnityEvent _onActivated = new UnityEvent();    ///< 起動した後に呼ぶ

        [Tooltip("止まったときに呼ぶ処理。Toggle で OFF になったときと、Sustain でエネルギーが 0 になったときに呼ばれる\n" +
                 "Latch では、コードから戻したとき（ResetState、SetActive(false)）だけ呼ばれる")]
        [SerializeField] private UnityEvent _onDeactivated = new UnityEvent();  ///< 止まった後に呼ぶ

        private float _energy;          ///< 溜まっているエネルギー
        private float _lastReceiveTime = float.NegativeInfinity;  ///< 最後に波を受け付けた時刻
        private bool _isWaitingRelease; ///< Toggle で切り替えた後、波が途切れるのを待っているか

        /// @brief 反応する種類の波を受け付けた後に起きる
        public event Action<WaveHit> WaveAccepted;

        /// @brief 反応しない種類の波が当たった後に起きる
        public event Action<WaveHit> WaveRejected;

        /// @brief 起動しているか
        public bool IsActive { get; private set; }

        /// @brief 波を受けている最中か
        public bool IsReceiving => Time.time - _lastReceiveTime <= ReceivingGraceTime;

        /// @brief 溜まっているエネルギー
        public float Energy => _energy;

        /// @brief 起動に要るエネルギーに対する、溜まっている割合（0〜1）
        public float EnergyRatio => Mathf.Clamp01(_energy / _requiredEnergy);

        /// @brief 起動のしかた
        public WaveActivationMode Mode => _mode;

        /// @brief 起動した後に呼ぶ UnityEvent。コードから AddListener してもよい
        public UnityEvent ActivatedEvent => _onActivated;

        /// @brief 止まった後に呼ぶ UnityEvent。コードから AddListener してもよい
        public UnityEvent DeactivatedEvent => _onDeactivated;

        //============================================================
        // 公開の操作
        //============================================================

        /// @brief その種類の波に反応するか
        /// @param type 波の種類
        /// @return 反応するなら true
        public bool CanAccept(WaveType type)
        {
            if (_canAcceptAnyType)
            {
                return true;
            }
            return type != null && _acceptedTypes != null && Array.IndexOf(_acceptedTypes, type) >= 0;
        }

        /// @brief 波を受ける。反応する種類ならエネルギーを溜め、溜まったら起動する
        /// @param hit 当たりの情報
        /// @return 反応する種類なら true
        public bool ReceiveWave(in WaveHit hit)
        {
            if (!isActiveAndEnabled)
            {
                return false;
            }
            if (!CanAccept(hit.Type))
            {
                WaveRejected?.Invoke(hit);
                return false;
            }

            _lastReceiveTime = Time.time;
            WaveAccepted?.Invoke(hit);

            // Latch で起動済み、または Toggle で波が途切れるのを待っている間は溜めない
            bool isLatched = _mode == WaveActivationMode.Latch && IsActive;
            if (isLatched || _isWaitingRelease)
            {
                return true;
            }

            _energy = Mathf.Min(_energy + hit.Energy, _requiredEnergy);
            if (_energy >= _requiredEnergy)
            {
                Trigger();
            }
            return true;
        }

        /// @brief 外から起動の状態を決める（セーブの読み込みや、別のスイッチとの連動に使う）
        /// @param isActive 起動するなら true
        public void SetActive(bool isActive)
        {
            _energy = isActive ? _requiredEnergy : 0.0f;
            SetActiveState(isActive);
        }

        /// @brief エネルギーを 0 にして、止まった状態に戻す
        public void ResetState()
        {
            _isWaitingRelease = false;
            SetActive(false);
        }

        //============================================================
        // Unity のメッセージ
        //============================================================

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

            _energy = Mathf.Max(0.0f, _energy - _decayPerSecond * Time.deltaTime);
            if (_mode == WaveActivationMode.Sustain && IsActive && _energy <= 0.0f)
            {
                SetActiveState(false);
            }
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
                    _energy = 0.0f;
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
