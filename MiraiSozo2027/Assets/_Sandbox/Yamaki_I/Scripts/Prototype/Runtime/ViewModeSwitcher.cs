using Aura.Wave;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Aura.Prototype
{
    /// @brief 一人称と三人称を切り替える。Inspector の View Mode か V キーで切り替える
    /// 視点ごとの GameObject を出し入れし、WaveEmitter の手（Muzzle）と視点（AimOrigin）を差し替える
    public sealed class ViewModeSwitcher : MonoBehaviour
    {
        [Tooltip("今の視点。実行中に変えてもよい（Toggle Action のキーでも切り替わる）\n" +
                 "First Person: 一人称。「一人称」の欄の物を出し、「三人称」の欄の物を隠す\n" +
                 "Third Person: 三人称。「三人称」の欄の物を出し、「一人称」の欄の物を隠す")]
        [SerializeField] private ViewMode _viewMode = ViewMode.FirstPerson;  ///< 今の視点。実行中に変えてもよい

        [Tooltip("視点に合わせて、手（Muzzle）と狙いの視点（Aim Origin）を差し替える WaveEmitter。空なら差し替えない")]
        [SerializeField] private WaveEmitter _emitter;                       ///< 手と視点を差し替える WaveEmitter

        [Header("一人称")]
        [Tooltip("一人称のときだけ出す GameObject（一人称のカメラ、手など）。三人称のときは非アクティブになる")]
        [SerializeField] private GameObject[] _firstPersonObjects;  ///< 一人称のときだけ出す物（カメラ、手）

        [Tooltip("一人称のカメラ。一人称のとき、WaveEmitter の Aim Origin になる（空なら Muzzle の正面に撃つ）\n" +
                 "ここに入れるだけでは表示は切り替わらない。カメラの GameObject は First Person Objects にも入れる")]
        [SerializeField] private Transform _firstPersonCamera;      ///< 一人称のカメラ

        [Tooltip("一人称の手の先。一人称のとき、WaveEmitter の Muzzle になる（空なら WaveEmitter の GameObject の位置から出る）")]
        [SerializeField] private Transform _firstPersonMuzzle;      ///< 一人称の手の先

        [Header("三人称")]
        [Tooltip("三人称のときだけ出す GameObject（三人称のカメラ、体、手など）。一人称のときは非アクティブになる")]
        [SerializeField] private GameObject[] _thirdPersonObjects;  ///< 三人称のときだけ出す物（カメラ、体、手）

        [Tooltip("三人称のカメラ。三人称のとき、WaveEmitter の Aim Origin になる（空なら Muzzle の正面に撃つ）\n" +
                 "ここに入れるだけでは表示は切り替わらない。カメラの GameObject は Third Person Objects にも入れる")]
        [SerializeField] private Transform _thirdPersonCamera;      ///< 三人称のカメラ

        [Tooltip("三人称の手の先。三人称のとき、WaveEmitter の Muzzle になる（空なら WaveEmitter の GameObject の位置から出る）")]
        [SerializeField] private Transform _thirdPersonMuzzle;      ///< 三人称の手の先

        [Header("入力")]
        [Tooltip("一人称と三人称を入れ替えるボタン。既定: V キー")]
        [SerializeField] private InputAction _toggleAction = new InputAction("ToggleView", InputActionType.Button, "<Keyboard>/v");  ///< 視点を入れ替える

        private ViewMode _appliedViewMode;  ///< 最後に反映した視点
        private bool _hasApplied;           ///< 一度でも反映したか

        /// @brief 今の視点。変えるとすぐに反映する
        public ViewMode Mode
        {
            get => _viewMode;
            set
            {
                _viewMode = value;
                Apply();
            }
        }

        private void Awake()
        {
            Apply();
        }

        private void OnEnable()
        {
            _toggleAction.Enable();
        }

        private void OnDisable()
        {
            _toggleAction.Disable();
        }

        private void Update()
        {
            if (_toggleAction.WasPressedThisFrame())
            {
                Mode = _viewMode == ViewMode.FirstPerson ? ViewMode.ThirdPerson : ViewMode.FirstPerson;
            }
            // Inspector で実行中に変えた値を反映する
            if (_viewMode != _appliedViewMode)
            {
                Apply();
            }
        }

        private void Apply()
        {
            if (_hasApplied && _viewMode == _appliedViewMode)
            {
                return;
            }
            _hasApplied = true;
            _appliedViewMode = _viewMode;

            bool isFirstPerson = _viewMode == ViewMode.FirstPerson;
            SetObjectsActive(_firstPersonObjects, isFirstPerson);
            SetObjectsActive(_thirdPersonObjects, !isFirstPerson);

            if (_emitter != null)
            {
                _emitter.Muzzle = isFirstPerson ? _firstPersonMuzzle : _thirdPersonMuzzle;
                _emitter.AimOrigin = isFirstPerson ? _firstPersonCamera : _thirdPersonCamera;
            }
        }

        private static void SetObjectsActive(GameObject[] targets, bool isActive)
        {
            if (targets == null)
            {
                return;
            }
            foreach (GameObject target in targets)
            {
                if (target != null)
                {
                    target.SetActive(isActive);
                }
            }
        }
    }
}
