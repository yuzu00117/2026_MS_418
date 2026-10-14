using UnityEngine;

namespace Aura.Prototype
{
    /// @brief 起動で決めた位置まで動き、止めると元の位置に戻るギミック（扉、リフトなど）
    /// WaveReceiver の On Activated に Activate、On Deactivated に Deactivate をつなぐ
    public sealed class DemoGimmickMover : MonoBehaviour
    {
        [Tooltip("起動したときに動く量（m）。ゲーム開始時の位置から、この分だけずらした位置まで動く\n" +
                 "向きは親の GameObject から見た向き（親がなければワールドの向き）。例: (0, 3, 0) なら 3 m 上がる")]
        [SerializeField] private Vector3 _activeOffset = new Vector3(0.0f, 3.0f, 0.0f);  ///< 起動したときに動く量（ローカル座標、m）

        [Tooltip("動く速さ（m/s）。起動したときも、元の位置に戻るときも、この速さで動く。0.01 以上")]
        [SerializeField, Min(0.01f)] private float _speed = 2.0f;                        ///< 動く速さ（m/s）

        private Vector3 _inactivePosition;  ///< 止まっているときの位置（ローカル座標）
        private bool _isActive;             ///< 起動しているか

        /// @brief 起動した位置へ動かす
        public void Activate()
        {
            _isActive = true;
        }

        /// @brief 元の位置へ戻す
        public void Deactivate()
        {
            _isActive = false;
        }

        /// @brief 起動と停止を入れ替える
        public void Toggle()
        {
            _isActive = !_isActive;
        }

        private void Awake()
        {
            _inactivePosition = transform.localPosition;
        }

        private void Update()
        {
            Vector3 target = _isActive ? _inactivePosition + _activeOffset : _inactivePosition;
            transform.localPosition = Vector3.MoveTowards(transform.localPosition, target, _speed * Time.deltaTime);
        }
    }
}
