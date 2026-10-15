using Aura.Inspector;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Aura.Prototype
{
    /// @brief 一人称と三人称を切り替える。Inspector の View Mode か V キーで切り替える
    /// 視点ごとの GameObject を出し入れする。ビームの出る手や狙いは、ビームの側（InfoBeamEmitter）が、出ている方を選ぶ
    public sealed class ViewModeSwitcher : MonoBehaviour
    {
        [InspectorLabel("今の視点")]
        [Tooltip("今の視点。実行中に変えてもよい（「視点を入れ替える操作」のキーでも切り替わる）\n" +
                 "一人称: 一人称。「一人称」の欄の物を出し、「三人称」の欄の物を隠す\n" +
                 "三人称: 三人称。「三人称」の欄の物を出し、「一人称」の欄の物を隠す")]
        [SerializeField] private ViewMode _viewMode = ViewMode.FirstPerson;  ///< 今の視点。実行中に変えてもよい

        [Header("一人称")]
        [InspectorLabel("一人称で出す物")]
        [Tooltip("一人称のときだけ出す GameObject（一人称のカメラ、手など）。三人称のときは非アクティブになる")]
        [SerializeField] private GameObject[] _firstPersonObjects;  ///< 一人称のときだけ出す物（カメラ、手）

        [Header("三人称")]
        [InspectorLabel("三人称で出す物")]
        [Tooltip("三人称のときだけ出す GameObject（三人称のカメラ、体、手など）。一人称のときは非アクティブになる")]
        [SerializeField] private GameObject[] _thirdPersonObjects;  ///< 三人称のときだけ出す物（カメラ、体、手）

        [Header("入力")]
        [InspectorLabel("視点を入れ替える操作")]
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
