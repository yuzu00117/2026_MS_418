using Aura.Inspector;
using UnityEngine;

namespace Aura.Gimmick
{
    /// @brief 起動で決めた位置まで動き、止めると元の位置に戻るギミック（扉、リフトなど）
    /// 受け手（ReactionTrigger、古い WaveReceiver）の On Activated に Activate、On Deactivated に Deactivate をつなぐ
    /// 「動き方」を「起動中は往復する」にすると、起動している間ずっと往復する足場になる
    /// 起動・停止・往復の切り替えは親の GimmickMotion にある
    public sealed class GimmickMover : GimmickMotion
    {
        [InspectorLabel("起動で動く量（m）")]
        [Tooltip("起動したときに動く量（m）。ゲーム開始時の位置から、この分だけずらした位置まで動く\n" +
                 "向きは親の GameObject から見た向き（親がなければワールドの向き）。例: (0, 3, 0) なら 3 m 上がる")]
        [SerializeField] private Vector3 _activeOffset = new Vector3(0.0f, 3.0f, 0.0f);  ///< 起動したときに動く量（ローカル座標、m）

        [InspectorLabel("動く速さ（m/s）")]
        [Tooltip("動く速さ（m/s）。起動したときも、元の位置に戻るときも、この速さで動く。0.01 以上")]
        [SerializeField, Min(0.01f)] private float _speed = 2.0f;                        ///< 動く速さ（m/s）

        private Vector3 _inactivePosition;  ///< 止まっているときの位置（ローカル座標）

        protected override void RememberInactivePose()
        {
            _inactivePosition = transform.localPosition;
        }

        protected override bool MoveStep(bool isHeadingToActive)
        {
            Vector3 target = isHeadingToActive ? _inactivePosition + _activeOffset : _inactivePosition;
            transform.localPosition = Vector3.MoveTowards(transform.localPosition, target, _speed * Time.deltaTime);
            return transform.localPosition == target;
        }
    }
}
