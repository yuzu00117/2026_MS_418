using System;
using Aura.Inspector;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Aura.Wave.Controls
{
    /// @brief Input System の入力を WaveEmitter につなぐ部品
    /// アクションをこの部品の中に持つので、プロジェクトの .inputactions に依存しない。キーは Inspector で変えられる
    public sealed class WaveEmitterInput : MonoBehaviour
    {
        private static readonly int ReflectionModeCount = Enum.GetValues(typeof(WaveReflectionMode)).Length;  ///< 反射のしかたの数（切り替えて一周する数）

        [InspectorLabel("波を出す部品")]
        [Tooltip("操作する WaveEmitter。空なら、同じ GameObject の WaveEmitter を使う")]
        [SerializeField] private WaveEmitter _emitter;  ///< 操作する WaveEmitter。空なら同じ GameObject から探す

        [InspectorLabel("撃つ操作")]
        [Tooltip("撃つボタン。「単発」なら押した瞬間に 1 発、「連続（ビーム）」なら押している間ずっと出す\n" +
                 "既定: マウスの左ボタン、ゲームパッドの右トリガー")]
        [SerializeField] private InputAction _fireAction = CreateButton("Fire", "<Mouse>/leftButton", "<Gamepad>/rightTrigger");           ///< 撃つ

        [InspectorLabel("次の種類へ切り替える操作")]
        [Tooltip("次の波の種類（WaveEmitter の「撃てる波の種類」の 1 つ下）に切り替えるボタン\n" +
                 "既定: E キー、ゲームパッドの RB")]
        [SerializeField] private InputAction _nextTypeAction = CreateButton("NextType", "<Keyboard>/e", "<Gamepad>/rightShoulder");        ///< 次の波の種類

        [InspectorLabel("前の種類へ切り替える操作")]
        [Tooltip("前の波の種類（WaveEmitter の「撃てる波の種類」の 1 つ上）に切り替えるボタン\n" +
                 "既定: Q キー、ゲームパッドの LB")]
        [SerializeField] private InputAction _previousTypeAction = CreateButton("PreviousType", "<Keyboard>/q", "<Gamepad>/leftShoulder"); ///< 前の波の種類

        [InspectorLabel("撃ち方を入れ替える操作")]
        [Tooltip("撃ち方の「単発」と「連続（ビーム）」を入れ替えるボタン。撃ち方を比べて試す用\n" +
                 "既定: F キー、ゲームパッドの十字キーの上")]
        [SerializeField] private InputAction _toggleFireModeAction = CreateButton("ToggleFireMode", "<Keyboard>/f", "<Gamepad>/dpad/up");  ///< 単発と連続を入れ替える（確かめる用）

        [InspectorLabel("反射のしかたを切り替える操作")]
        [Tooltip("WaveEmitter の「反射のしかた」を「跳ね返らない」 → 「どの物でも跳ね返る」 → 「反射板でだけ跳ね返る」 → 「跳ね返らない」の順に切り替えるボタン。反射のありとなしを比べて試す用\n" +
                 "既定: R キー、ゲームパッドの十字キーの右")]
        [SerializeField] private InputAction _cycleReflectionModeAction = CreateButton("CycleReflectionMode", "<Keyboard>/r", "<Gamepad>/dpad/right");  ///< 反射のしかたを順に切り替える（確かめる用）

        private void Awake()
        {
            if (_emitter == null)
            {
                _emitter = GetComponent<WaveEmitter>();
            }
        }

        private void OnEnable()
        {
            _fireAction.Enable();
            _nextTypeAction.Enable();
            _previousTypeAction.Enable();
            _toggleFireModeAction.Enable();
            _cycleReflectionModeAction.Enable();
        }

        private void OnDisable()
        {
            _fireAction.Disable();
            _nextTypeAction.Disable();
            _previousTypeAction.Disable();
            _toggleFireModeAction.Disable();
            _cycleReflectionModeAction.Disable();
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
                _emitter.SelectNextType();
            }
            if (_previousTypeAction.WasPressedThisFrame())
            {
                _emitter.SelectPreviousType();
            }
            if (_toggleFireModeAction.WasPressedThisFrame())
            {
                _emitter.FireMode = _emitter.FireMode == WaveFireMode.Pulse ? WaveFireMode.Beam : WaveFireMode.Pulse;
            }
            if (_cycleReflectionModeAction.WasPressedThisFrame())
            {
                _emitter.ReflectionMode = (WaveReflectionMode)(((int)_emitter.ReflectionMode + 1) % ReflectionModeCount);
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
