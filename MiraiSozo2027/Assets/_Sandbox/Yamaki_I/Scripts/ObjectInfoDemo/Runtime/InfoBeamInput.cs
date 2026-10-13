using UnityEngine;
using UnityEngine.InputSystem;

namespace Aura.ObjectInfoDemo
{
    /// @brief Input System の入力を InfoBeamEmitter につなぐ部品
    /// アクションをこの部品の中に持つので、プロジェクトの .inputactions に依存しない。キーは Inspector で変えられる
    public sealed class InfoBeamInput : MonoBehaviour
    {
        [Tooltip("操作する InfoBeamEmitter。空なら、同じ GameObject の InfoBeamEmitter を使う")]
        [SerializeField] private InfoBeamEmitter _emitter;  ///< 操作する InfoBeamEmitter。空なら同じ GameObject から探す

        [Tooltip("ビームを出すボタン。押している間ずっと出す\n" +
                 "既定: マウスの左ボタン、ゲームパッドの右トリガー")]
        [SerializeField] private InputAction _fireAction = CreateButton("Fire", "<Mouse>/leftButton", "<Gamepad>/rightTrigger");           ///< 出す

        [Tooltip("次のビーム（InfoBeamEmitter の Sources の 1 つ下）に切り替えるボタン\n" +
                 "既定: E キー、ゲームパッドの RB")]
        [SerializeField] private InputAction _nextTypeAction = CreateButton("NextType", "<Keyboard>/e", "<Gamepad>/rightShoulder");        ///< 次のビーム

        [Tooltip("前のビーム（InfoBeamEmitter の Sources の 1 つ上）に切り替えるボタン\n" +
                 "既定: Q キー、ゲームパッドの LB")]
        [SerializeField] private InputAction _previousTypeAction = CreateButton("PreviousType", "<Keyboard>/q", "<Gamepad>/leftShoulder"); ///< 前のビーム

        private void Awake()
        {
            if (_emitter == null)
            {
                _emitter = GetComponent<InfoBeamEmitter>();
            }
        }

        private void OnEnable()
        {
            _fireAction.Enable();
            _nextTypeAction.Enable();
            _previousTypeAction.Enable();
        }

        private void OnDisable()
        {
            _fireAction.Disable();
            _nextTypeAction.Disable();
            _previousTypeAction.Disable();
            if (_emitter != null)
            {
                _emitter.SetTriggerHeld(false);
            }
        }

        private void Update()
        {
            if (_emitter == null)
            {
                return;
            }

            if (_nextTypeAction.WasPressedThisFrame())
            {
                _emitter.SelectNextSource();
            }
            if (_previousTypeAction.WasPressedThisFrame())
            {
                _emitter.SelectPreviousSource();
            }
            _emitter.SetTriggerHeld(_fireAction.IsPressed());
        }

        private static InputAction CreateButton(string name, string primaryBinding, string secondaryBinding)
        {
            var action = new InputAction(name, InputActionType.Button, primaryBinding);
            action.AddBinding(secondaryBinding);
            return action;
        }
    }
}
