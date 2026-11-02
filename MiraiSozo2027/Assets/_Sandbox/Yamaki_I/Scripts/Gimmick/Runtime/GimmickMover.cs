using Aura.Inspector;
using UnityEngine;

namespace Aura.Gimmick
{
    /// @brief 起動で決めた位置まで動き、止めると元の位置に戻るギミック（扉、リフトなど）
    /// 受け手（ReactionTrigger、古い WaveReceiver）の On Activated に Activate、On Deactivated に Deactivate をつなぐ
    /// 「動き方」を「起動中は往復する」にすると、起動している間ずっと往復する足場になる
    public sealed class GimmickMover : MonoBehaviour
    {
        [InspectorLabel("起動で動く量（m）")]
        [Tooltip("起動したときに動く量（m）。ゲーム開始時の位置から、この分だけずらした位置まで動く\n" +
                 "向きは親の GameObject から見た向き（親がなければワールドの向き）。例: (0, 3, 0) なら 3 m 上がる")]
        [SerializeField] private Vector3 _activeOffset = new Vector3(0.0f, 3.0f, 0.0f);  ///< 起動したときに動く量（ローカル座標、m）

        [InspectorLabel("動く速さ（m/s）")]
        [Tooltip("動く速さ（m/s）。起動したときも、元の位置に戻るときも、この速さで動く。0.01 以上")]
        [SerializeField, Min(0.01f)] private float _speed = 2.0f;                        ///< 動く速さ（m/s）

        [InspectorLabel("動き方")]
        [Tooltip("起動している間の動き方\n" +
                 "・起動で動く: 起動した位置まで動いて止まる\n" +
                 "・起動中は往復する: 起動している間、元の位置と起動した位置をずっと往復する。止めると元の位置に戻る")]
        [SerializeField] private GimmickMotionMode _motionMode = GimmickMotionMode.Once;  ///< 起動している間の動き方

        [InspectorLabel("端で止まる時間（秒）")]
        [Tooltip("「起動中は往復する」のとき、端に着いてから折り返すまでに止まる時間（秒）。0 ならすぐ折り返す")]
        [SerializeField, Min(0.0f)] private float _endWaitSeconds = 1.0f;                ///< 往復で、端に着いてから折り返すまでの時間（秒）

        [InspectorLabel("始めから起動しておく")]
        [Tooltip("ON なら、ゲーム開始時から起動している（Activate を呼ばなくても動く）")]
        [SerializeField] private bool _isActiveOnStart;                                  ///< ゲーム開始時から起動しているか

        private Vector3 _inactivePosition;  ///< 止まっているときの位置（ローカル座標）
        private bool _isActive;             ///< 起動しているか
        private bool _isReturning;          ///< 往復で、元の位置へ戻っている途中か
        private float _waitSeconds;         ///< 往復で、端に着いてから止まっている時間（秒）

        /// @brief 起動した位置へ動かす
        [ShowInEventMenu]
        public void Activate()
        {
            _isActive = true;
        }

        /// @brief 元の位置へ戻す
        [ShowInEventMenu]
        public void Deactivate()
        {
            _isActive = false;
            _isReturning = false;
            _waitSeconds = 0.0f;
        }

        /// @brief 起動と停止を入れ替える
        [ShowInEventMenu]
        public void Toggle()
        {
            if (_isActive)
            {
                Deactivate();
            }
            else
            {
                Activate();
            }
        }

        private void Awake()
        {
            _inactivePosition = transform.localPosition;
            if (_isActiveOnStart)
            {
                Activate();
            }
        }

        private void Update()
        {
            Vector3 target = _isActive && !_isReturning ? _inactivePosition + _activeOffset : _inactivePosition;
            transform.localPosition = Vector3.MoveTowards(transform.localPosition, target, _speed * Time.deltaTime);
            if (_isActive && _motionMode == GimmickMotionMode.PingPong && transform.localPosition == target)
            {
                WaitAndTurn();
            }
        }

        /// @brief 往復で端に着いているとき、「端で止まる時間（秒）」だけ待ってから折り返す
        private void WaitAndTurn()
        {
            _waitSeconds += Time.deltaTime;
            if (_waitSeconds >= _endWaitSeconds)
            {
                _isReturning = !_isReturning;
                _waitSeconds = 0.0f;
            }
        }
    }
}
