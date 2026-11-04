using Aura.Inspector;
using UnityEngine;

namespace Aura.Gimmick
{
    /// @brief 起動で決めた位置や向きまで動き、止めると元に戻るギミックの共通部分（GimmickMover、GimmickRotator の親）
    /// 起動・停止・往復の切り替えと、「始めから起動しておく」をここで持つ
    /// 「どこまで、どう動かすか」は継承先が RememberInactivePose と MoveStep で決める
    public abstract class GimmickMotion : MonoBehaviour
    {
        [InspectorLabel("動き方")]
        [Tooltip("起動している間の動き方\n" +
                 "・起動で動く: 起動した位置（向き）まで動いて止まる\n" +
                 "・起動中は往復する: 起動している間、元の位置（向き）と起動した位置（向き）をずっと往復する。止めると元に戻る")]
        [SerializeField] private GimmickMotionMode _motionMode = GimmickMotionMode.Once;  ///< 起動している間の動き方

        [InspectorLabel("端で止まる時間（秒）")]
        [Tooltip("「起動中は往復する」のとき、端に着いてから折り返すまでに止まる時間（秒）。0 ならすぐ折り返す")]
        [SerializeField, Min(0.0f)] private float _endWaitSeconds = 1.0f;                ///< 往復で、端に着いてから折り返すまでの時間（秒）

        [InspectorLabel("始めから起動しておく")]
        [Tooltip("ON なら、ゲーム開始時から起動している（Activate を呼ばなくても動く）。首を振り続けるタレットなどに使う")]
        [SerializeField] private bool _isActiveOnStart;                                  ///< ゲーム開始時から起動しているか

        private bool _isActive;      ///< 起動しているか
        private bool _isReturning;   ///< 往復で、元の位置（向き）へ戻っている途中か
        private float _waitSeconds;  ///< 往復で、端に着いてから止まっている時間（秒）

        /// @brief 起動した位置（向き）へ動かす
        [ShowInEventMenu]
        public void Activate()
        {
            _isActive = true;
        }

        /// @brief 元の位置（向き）へ戻す
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

        /// @brief 止まっているときの位置や向きを覚える。ゲーム開始時に 1 回呼ぶ
        protected abstract void RememberInactivePose();

        /// @brief 1 フレーム分動かす
        /// @param isHeadingToActive true なら起動した位置（向き）へ、false なら元の位置（向き）へ向かう
        /// @return 向かっている先に着いていれば true
        protected abstract bool MoveStep(bool isHeadingToActive);

        private void Awake()
        {
            RememberInactivePose();
            if (_isActiveOnStart)
            {
                Activate();
            }
        }

        private void Update()
        {
            bool hasArrived = MoveStep(_isActive && !_isReturning);
            if (_isActive && _motionMode == GimmickMotionMode.PingPong && hasArrived)
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
