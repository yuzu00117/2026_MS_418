using Aura.Inspector;
using UnityEngine;

namespace Aura.Gimmick
{
    /// @brief 起動で決めた角度まで回り、止めると元の向きに戻るギミック（回る鏡、回る扉、首を振るタレットなど）
    /// GimmickMover の回る版。受け手（ReactionTrigger）の「起動したときの処理」に Activate、「止まったときの処理」に Deactivate をつなぐ
    /// 回る中心はこの GameObject の位置。扉の蝶番のように端で回したいときは、空の親に付けて、扉の見た目を子としてずらして置く
    /// 起動・停止・往復の切り替えは親の GimmickMotion にある
    public sealed class GimmickRotator : GimmickMotion
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

        private Quaternion _inactiveRotation;  ///< 止まっているときの向き（ローカル座標）
        private float _angle;                  ///< 今、元の向きから回っている角度（度）

        protected override void RememberInactivePose()
        {
            _inactiveRotation = transform.localRotation;
        }

        protected override bool MoveStep(bool isHeadingToActive)
        {
            if (_axis.sqrMagnitude < MinAxisSqrLength)
            {
                return false;
            }
            float target = isHeadingToActive ? _activeAngle : 0.0f;
            _angle = Mathf.MoveTowards(_angle, target, _speed * Time.deltaTime);
            // 軸は親から見た向きなので、元の向きの「前」から掛ける
            transform.localRotation = Quaternion.AngleAxis(_angle, _axis) * _inactiveRotation;
            return Mathf.Approximately(_angle, target);
        }
    }
}
