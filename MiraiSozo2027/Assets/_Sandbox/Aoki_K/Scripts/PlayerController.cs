using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField]private float _moveSpeed = 5.0f;
    [SerializeField]private float _jumpForce = 5.0f;
    [SerializeField]private float _rotateSpeed = 720f; // 1秒当たりの最大回転角度（度/秒）

    [Header("References")]
    [SerializeField]private Transform _cameraTransform;
    [SerializeField]private GroundChecker _groundChecker;

    private Rigidbody _rb;
    private Animator _animator;

    private InputSystem_Actions _actions;

    private void Awake()
    {
        _actions = new InputSystem_Actions();
    }

    private void OnEnable()
    {
        _actions.Enable();
    }

    private void OnDisable()
    {
        _actions.Disable();
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _rb = GetComponent<Rigidbody>();
        _animator = GetComponent<Animator>();

        if(_cameraTransform == null)
        {// カメラが未アサインの場合は自動取得
            _cameraTransform = Camera.main.transform;
        }

        // Rigidbodyの回転を禁止
        _rb.constraints = RigidbodyConstraints.FreezeRotation;
    }

    // Update is called once per frame
    void Update()
    {
        // 1. 入力の取得 (W/A/S/D)
        Vector2 input = _actions.Player.Move.ReadValue<Vector2>();

        // 2. カメラ基準の移動方向ベクトルを計算（XZ平面）
        Vector3 moveDirection = Vector3.zero;
        if (_cameraTransform != null)
        {
            Vector3 camForward = _cameraTransform.forward;
            Vector3 camRight = _cameraTransform.right;
            // 水平移動のみ行わせるためY軸（高さ）をカット
            camForward.y = 0f;
            camRight.y = 0f;
            camForward.Normalize();
            camRight.Normalize();
            moveDirection = (camForward * input.y + camRight * input.x).normalized;

            // スティックを最大まで倒したときに速度が１を超えないようにクランプ
            if(moveDirection.sqrMagnitude > 1.0f)
            {
                moveDirection.Normalize();
            }
        }
        // 3. 入力がある場合の「回転」と「移動」
        if (moveDirection.sqrMagnitude > 0.001f)
        {
            // 目標の向き（進行方向）を計算
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection, Vector3.up);
            // 現在の向きから目標の向きへ、1秒あたり _rotateSpeed 度の速度で滑らかに旋回
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                _rotateSpeed * Time.deltaTime
            );
            // 進行方向へ移動
            transform.position += moveDirection * (_moveSpeed * Time.deltaTime);
            // 歩行アニメーションON
            if (_animator != null)
            {
                _animator.SetBool("isWalking", true);
            }
        }
        else
        {
            // 入力がない時は歩行アニメーションOFF
            if (_animator != null)
            {
                _animator.SetBool("isWalking", false);
            }
        }

        if(_actions.Player.Jump.triggered && _groundChecker != null && _groundChecker.IsGrounded)
        {
            _animator.SetBool("isJumping", true);
            _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
            _rb.AddForce(Vector3.up * _jumpForce, ForceMode.Impulse);
        }
        else if(_groundChecker != null && !_groundChecker.IsGrounded)
        {
            _animator.SetBool("isJumping", false);
        }


        // Layout Mode Toggle
        if(_actions.Player.LayoutMode.triggered)
        {
            gameObject.GetComponent<ObjectPlacer>().TogglePlaceMode();
        }
    }
}
