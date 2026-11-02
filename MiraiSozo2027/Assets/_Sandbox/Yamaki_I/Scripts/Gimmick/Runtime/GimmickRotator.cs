using Aura.Inspector;
using UnityEngine;

namespace Aura.Gimmick
{
    /// @brief 起動で決めた角度まで回り、止めると元の向きに戻るギミック（回る鏡、回る扉、首を振るタレットなど）
    /// GimmickMover の回る版。受け手（ReactionTrigger）の「起動したときの処理」に Activate、「止まったときの処理」に Deactivate をつなぐ
    /// 回る中心はこの GameObject の位置。扉の蝶番のように端で回したいときは、空の親に付けて、扉の見た目を子としてずらして置く
    public sealed class GimmickRotator : MonoBehaviour
    {
        private const float MinAxisSqrLength = 0.0001f;  ///< これより短い「回る軸」は、向きが決まらないので回さない

        [InspectorLabel("回る軸")]
        [Tooltip("回る軸の向き。親の GameObject から見た向き（親がなければワールドの向き）。例: (0, 1, 0) なら上下の軸のまわりに水平に回る\n" +
                 "長さは関係ない。長さ 0 なら回らない")]
        [SerializeField] private Vector3 _axis = Vector3.up;                              ///< 回る軸（ローカル座標）

        [InspectorLabel("起動で回る角度（度）")]
        [Tooltip("起動したときに回る角度（度）。ゲーム開始時の向きから、この分だけ回った向きまで回る\n" +
                 "マイナスなら逆向きに回る。180 度より大きくてもよい（近い向きへ回り込まず、決めた向きに回る）")]
        [SerializeField] private float _activeAngle = 90.0f;                              ///< 起動したときに回る角度（度）

        [InspectorLabel("回る速さ（度/s）")]
        [Tooltip("回る速さ（度/s）。起動したときも、元の向きに戻るときも、この速さで回る。0.01 以上")]
        [SerializeField, Min(0.01f)] private float _speed = 90.0f;                        ///< 回る速さ（度/s）

        [InspectorLabel("動き方")]
        [Tooltip("起動している間の動き方\n" +
                 "・起動で動く: 起動した向きまで回って止まる\n" +
                 "・起動中は往復する: 起動している間、元の向きと起動した向きをずっと行き来する（首振り）。止めると元の向きに戻る")]
        [SerializeField] private GimmickMotionMode _motionMode = GimmickMotionMode.Once;  ///< 起動している間の動き方

        [InspectorLabel("端で止まる時間（秒）")]
        [Tooltip("「起動中は往復する」のとき、端に着いてから折り返すまでに止まる時間（秒）。0 ならすぐ折り返す")]
        [SerializeField, Min(0.0f)] private float _endWaitSeconds = 1.0f;                ///< 往復で、端に着いてから折り返すまでの時間（秒）

        [InspectorLabel("始めから起動しておく")]
        [Tooltip("ON なら、ゲーム開始時から起動している（Activate を呼ばなくても回る）。首を振り続けるタレットなどに使う")]
        [SerializeField] private bool _isActiveOnStart;                                  ///< ゲーム開始時から起動しているか

        private Quaternion _inactiveRotation;  ///< 止まっているときの向き（ローカル座標）
        private float _angle;                  ///< 今、元の向きから回っている角度（度）
        private bool _isActive;                ///< 起動しているか
        private bool _isReturning;             ///< 往復で、元の向きへ戻っている途中か
        private float _waitSeconds;            ///< 往復で、端に着いてから止まっている時間（秒）

        /// @brief 起動した向きへ回す
        [ShowInEventMenu]
        public void Activate()
        {
            _isActive = true;
        }

        /// @brief 元の向きへ戻す
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
            _inactiveRotation = transform.localRotation;
            if (_isActiveOnStart)
            {
                Activate();
            }
        }

        private void Update()
        {
            if (_axis.sqrMagnitude < MinAxisSqrLength)
            {
                return;
            }
            float target = _isActive && !_isReturning ? _activeAngle : 0.0f;
            _angle = Mathf.MoveTowards(_angle, target, _speed * Time.deltaTime);
            // 軸は親から見た向きなので、元の向きの「前」から掛ける
            transform.localRotation = Quaternion.AngleAxis(_angle, _axis) * _inactiveRotation;
            if (_isActive && _motionMode == GimmickMotionMode.PingPong && Mathf.Approximately(_angle, target))
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
