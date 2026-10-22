using Aura.ObjectInfoDemo;
using Aura.Wave;
using Echo.Echolocation;
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

    private WaveEmitter _waveEmitter;
    private EchoController _echoController;
    private Phononkinesis _phononkinesis;

    private InputSystem_Actions _actions => GameManager.Actions;

    // 移動用のキャッシュ変数
    private Vector3 _moveDirection = Vector3.zero;
    private bool _jumpRequested = false;

    public static PlayerController Instance { get; private set;}

    //プレイヤーがスポーンしたことを通知するイベント
    public static event System.Action<PlayerController> OnPlayerSpawned;

    private void Awake()
    {
        if(Instance != null && Instance != this)
        {
            Debug.LogWarning("プレイヤーが重複生成されたため破棄しました：" + gameObject.name);
            Destroy(gameObject);
            return;
        }

        Instance = this;

        //コンポーネントの取得
        _rb = GetComponent<Rigidbody>();
        _animator = GetComponent<Animator>();
        _waveEmitter = GetComponent<WaveEmitter>();
        _echoController = GetComponent<EchoController>();
        _phononkinesis = GetComponent<Phononkinesis>();

    }

    private void OnEnable()
    {

    }

    private void OnDisable()
    {

    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {


        Camera mainCam = Camera.main;
        if(mainCam != null)
        {
            _cameraTransform = mainCam.transform;

            InjectCameraReference(mainCam);
        }

        // プレイヤーがスポーンしたことを通知
        OnPlayerSpawned?.Invoke(this);

        if(_rb != null)
        {
            // Rigidbodyの回転を禁止
            _rb.constraints = RigidbodyConstraints.FreezeRotation;
            // 物理移動を滑らかにするための補間を有効化
            _rb.interpolation = RigidbodyInterpolation.Interpolate;
        }

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
            _moveDirection = (camForward * input.y + camRight * input.x).normalized;

            // スティックを最大まで倒したときに速度が１を超えないようにクランプ
            if(_moveDirection.sqrMagnitude > 1.0f)
            {
                _moveDirection.Normalize();
            }
        }
        // 3. 入力がある場合の「回転」と「移動」
        if (_moveDirection.sqrMagnitude > 0.001f)
        {
            // 目標の向き（進行方向）を計算
            Quaternion targetRotation = Quaternion.LookRotation(_moveDirection, Vector3.up);
            // 現在の向きから目標の向きへ、1秒あたり _rotateSpeed 度の速度で滑らかに旋回
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                _rotateSpeed * Time.deltaTime
            );
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

        // ４．ジャンプの検知
        if(_actions.Player.Jump.triggered && _groundChecker != null && _groundChecker.IsGrounded)
        {
            _jumpRequested = true;
            _animator.SetBool("isJumping", true);
        }
        else if(_groundChecker != null && !_groundChecker.IsGrounded)
        {
            _animator.SetBool("isJumping", false);
        }


        // Layout Mode Toggle
        if(_phononkinesis != null)
        {
            if(_actions.Player.LayoutMode.triggered)
            {
                _phononkinesis.TogglePhononkinesis();
            }
        }


        // WaveEmmiter
        if(_waveEmitter != null)
        {
            _waveEmitter.SetTriggerHeld(_actions.Player.Fire.IsPressed());

            if(_actions.Player.SwitchWave.triggered)
             {
                _waveEmitter.SelectNextSource();
             }
            //if(_actions.Player.ToggleFireMode.triggered)
            //{
            //    _waveEmitter.FireMode = _waveEmitter.FireMode == WaveFireMode.Pulse ? WaveFireMode.Beam : WaveFireMode.Pulse;
            //}

        }


        // EchoLocation
        if(_echoController != null)
        {
            if(_actions.Player.EchoScan.triggered)
            {
                _echoController.ToggleScan();
            }
        }
    }

    /// <summary>
    /// 物理演算の更新（毎フレーム安定した力・速度を適用）
    /// </summary>
    void FixedUpdate()
    {
        // 1. 水平方向の物理移動（linearVelocity）
        Vector3 targetVelocity = _moveDirection * _moveSpeed;
        // Y軸（重力落下速度や既存のジャンプ速度）は維持したまま、XとZのみ適用
        _rb.linearVelocity = new Vector3(targetVelocity.x, _rb.linearVelocity.y, targetVelocity.z);
        // 2. ジャンプの物理適用
        if (_jumpRequested)
        {
            // 上方向の速度をリセットして安定して跳べるようにする
            _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
            _rb.AddForce(Vector3.up * _jumpForce, ForceMode.Impulse);
            _jumpRequested = false;
        }
    }

    private void InjectCameraReference(Camera cam)
    {
        // WaveEmitter へ照準カメラを渡す
        if (_waveEmitter != null)
        {
            // ※WaveEmitter の Inspector フィールド _aimOrigin にカメラを注入
            _waveEmitter.AimOrigin = cam.transform;
        }
        // EchoController へ描画カメラを渡す
        if (_echoController != null)
        {
            _echoController.TargetCamera = cam;
            _echoController.DirectionTransform = cam.transform; // 正面基準もカメラに
        }
        // Phononkinesis へカメラを渡す
        if (_phononkinesis != null)
        {
            _phononkinesis.CameraTransform = cam.transform;
        }
    }
}
