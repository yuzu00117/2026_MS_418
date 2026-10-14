using UnityEngine;
using UnityEngine.InputSystem;

namespace Aura.Prototype
{
    /// @brief 波を試すための最小限のプレイヤーの移動と視点の操作（本実装には持ち込まない想定）
    [RequireComponent(typeof(CharacterController))]
    public sealed class DemoPlayerController : MonoBehaviour
    {
        private const float MaxPitch = 85.0f;
        private const float GroundedVerticalSpeed = -2.0f;  ///< 着地中に下へ押し付ける速さ（m/s）

        [Header("視点")]
        [Tooltip("上下に向きを変える部分（カメラと手の親）。上下の操作ではこれだけが傾き、左右の操作ではこの GameObject（Player）ごと回る\n" +
                 "上下は ±85 度まで。空だと上下を向けない")]
        [SerializeField] private Transform _pitchPivot;                      ///< 上下に向ける部分（カメラの親）

        [Tooltip("マウスを 1 ピクセル動かしたときに回る角度（度）。大きいほど速く振り向く")]
        [SerializeField, Min(0.0f)] private float _mouseSensitivity = 0.1f;  ///< マウスの 1 ドットあたりの回転（度）

        [Tooltip("ゲームパッドの右スティックを倒しきったときに回る速さ（度/秒）。倒した量に比例する")]
        [SerializeField, Min(0.0f)] private float _stickSensitivity = 180.0f;  ///< スティックを倒しきったときの回転（度/秒）

        [Header("移動")]
        [Tooltip("歩く速さ（m/s）。スティックは倒した量に比例する")]
        [SerializeField, Min(0.0f)] private float _moveSpeed = 5.0f;         ///< 歩く速さ（m/s）

        [Tooltip("Sprint Action を押している間の速さ（m/s）")]
        [SerializeField, Min(0.0f)] private float _sprintSpeed = 8.0f;       ///< 走る速さ（m/s）

        [Tooltip("ジャンプで上がる高さ（m）。地面に着いているときだけ跳べる。Gravity を変えても、上がる高さはほぼこの値のまま（上がり下りの速さが変わる）。0 なら跳ばない")]
        [SerializeField, Min(0.0f)] private float _jumpHeight = 1.2f;        ///< 跳ぶ高さ（m）

        [Tooltip("重力の加速度（m/s²）。必ず負の値にする（0 以上だとジャンプと落下が正しく動かない）\n" +
                 "現実の重力は約 -9.8。絶対値を大きくするほど、速く落ち、ジャンプが短く鋭くなる")]
        [SerializeField] private float _gravity = -20.0f;                    ///< 重力（m/s^2）

        [Header("入力")]
        [Tooltip("移動。既定: W / A / S / D キー、ゲームパッドの左スティック")]
        [SerializeField] private InputAction _moveAction = CreateMoveAction();  ///< 移動

        [Tooltip("マウスで視点を回す。既定: マウスの移動量。回る量は Mouse Sensitivity で決める\n" +
                 "画面をクリックしてカーソルを固定している間だけ効く（Esc で解除）")]
        [SerializeField] private InputAction _mouseLookAction = new InputAction("MouseLook", InputActionType.Value, "<Mouse>/delta");  ///< マウスで視点を回す

        [Tooltip("スティックで視点を回す。既定: ゲームパッドの右スティック。回る速さは Stick Sensitivity で決める\n" +
                 "画面をクリックしてカーソルを固定している間だけ効く（Esc で解除）")]
        [SerializeField] private InputAction _stickLookAction = new InputAction("StickLook", InputActionType.Value, "<Gamepad>/rightStick");  ///< スティックで視点を回す

        [Tooltip("ジャンプ。既定: Space キー、ゲームパッドの下のボタン（Xbox の A）")]
        [SerializeField] private InputAction _jumpAction = CreateButton("Jump", "<Keyboard>/space", "<Gamepad>/buttonSouth");  ///< ジャンプ

        [Tooltip("押している間、Sprint Speed で走る。既定: 左 Shift キー、ゲームパッドの左スティックの押し込み")]
        [SerializeField] private InputAction _sprintAction = CreateButton("Sprint", "<Keyboard>/leftShift", "<Gamepad>/leftStickPress");  ///< 走る

        private CharacterController _characterController;
        private float _pitch;          ///< 今の上下の角度（度）
        private float _verticalSpeed;  ///< 今の上下の速さ（m/s）

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
        }

        private void OnEnable()
        {
            _moveAction.Enable();
            _mouseLookAction.Enable();
            _stickLookAction.Enable();
            _jumpAction.Enable();
            _sprintAction.Enable();
        }

        private void OnDisable()
        {
            _moveAction.Disable();
            _mouseLookAction.Disable();
            _stickLookAction.Disable();
            _jumpAction.Disable();
            _sprintAction.Disable();
        }

        private void Update()
        {
            UpdateCursorLock();
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                UpdateLook();
            }
            UpdateMove();
        }

        private void UpdateCursorLock()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }

        private void UpdateLook()
        {
            Vector2 look = _mouseLookAction.ReadValue<Vector2>() * _mouseSensitivity
                + _stickLookAction.ReadValue<Vector2>() * (_stickSensitivity * Time.deltaTime);

            transform.Rotate(0.0f, look.x, 0.0f, Space.Self);
            _pitch = Mathf.Clamp(_pitch - look.y, -MaxPitch, MaxPitch);
            if (_pitchPivot != null)
            {
                _pitchPivot.localRotation = Quaternion.Euler(_pitch, 0.0f, 0.0f);
            }
        }

        private void UpdateMove()
        {
            Vector2 input = Vector2.ClampMagnitude(_moveAction.ReadValue<Vector2>(), 1.0f);
            float speed = _sprintAction.IsPressed() ? _sprintSpeed : _moveSpeed;
            Vector3 horizontal = (transform.right * input.x + transform.forward * input.y) * speed;

            if (_characterController.isGrounded)
            {
                _verticalSpeed = GroundedVerticalSpeed;
                if (_jumpAction.WasPressedThisFrame())
                {
                    _verticalSpeed = Mathf.Sqrt(2.0f * _jumpHeight * -_gravity);
                }
            }
            _verticalSpeed += _gravity * Time.deltaTime;

            Vector3 velocity = horizontal + Vector3.up * _verticalSpeed;
            _characterController.Move(velocity * Time.deltaTime);
        }

        private static InputAction CreateMoveAction()
        {
            var action = new InputAction("Move", InputActionType.Value);
            action.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w")
                .With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a")
                .With("Right", "<Keyboard>/d");
            action.AddBinding("<Gamepad>/leftStick");
            return action;
        }

        private static InputAction CreateButton(string name, string primaryBinding, string secondaryBinding)
        {
            var action = new InputAction(name, InputActionType.Button, primaryBinding);
            action.AddBinding(secondaryBinding);
            return action;
        }
    }
}
