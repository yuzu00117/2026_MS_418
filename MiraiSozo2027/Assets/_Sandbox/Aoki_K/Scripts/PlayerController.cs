using Aura.Wave;
using Aura.Echolocation;
using UnityEngine;
using UnityEngine.InputSystem;
using Aura.Cameras;
using Aura.Core;
using Aura.Kinesis;

namespace Aura.PlayerControl
{
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [Tooltip("通常移動（ダッシュ）の最大速度（デフォルト移動）")]
        [SerializeField] private float _runSpeed = 8.0f;

        [Tooltip("歩行時の最大速度（サブ移動 / スティック浅倒し時）")]
        [SerializeField] private float _walkSpeed = 5.5f;

        [Tooltip("キーボード操作時、歩行修飾キー（Sprintボタン/Shift等）を押している間に歩行（減速）するかどうか\n※ON: Shift長押しで歩き / OFF: Shift長押しでダッシュ")]
        [SerializeField] private bool _walkOnModifierKey = true;

        [SerializeField] private float _jumpForce = 5.0f;
        [SerializeField] private float _rotateSpeed = 720f; // 1秒当たりの最大回転角度（度/秒）

        [Tooltip("AnimatorのSpeedパラメータ変化の滑らかさ（Blend Treeのスムージング）")]
        [SerializeField] private float _animDampTime = 0.1f;

        [Header("References")]
        [SerializeField] private Transform _cameraTransform;
        [SerializeField] private GroundChecker _groundChecker;
        [Tooltip("ADS中にカメラを寄せる位置と向き（プレイヤーの子）。未設定ならカメラ側の距離・横オフセットで寄せる")]
        [SerializeField] private Transform _adsPosition;
        [Tooltip("ADS中に上下へ傾ける子（見た目・銃口・エコーの発生位置・ADS位置の親）。当たり判定は入れない。未設定ならカメラだけが上下を向く")]
        [SerializeField] private Transform _aimPivot;

        private Rigidbody _rb;
        private Animator _animator;

        private WaveEmitter _waveEmitter;
        private EchoController _echoController;
        private Phononkinesis _phononkinesis;
        private CameraBehavior _cameraBehavior;

        private InputSystem_Actions _actions => GameManager.Actions;

        // 移動制御用キャッシュ変数
        private Vector3 _moveDirection = Vector3.zero;
        // Rigidbodyの補間と食い違わないよう、Updateでは目標の向きだけを決め、回転はFixedUpdateで行う
        private Quaternion _targetRotation = Quaternion.identity;
        private bool _isAiming; // ADS中はカメラの向きへすぐに合わせるため、回転の速さの制限を使わない
        private float _currentMoveSpeed = 0f;
        private bool _jumpRequested = false;
        private bool _isWaitingFireRelease = false;

        // Animator パラメータハッシュ
        // 入力がほぼ0の時に向きがぶれないよう、これより小さい移動量は止まっている扱いにする
        private const float MinMoveSqrMagnitude = 0.001f;

        private static readonly int AnimParamSpeed = Animator.StringToHash("Speed");
        private static readonly int AnimParamIsJumping = Animator.StringToHash("isJumping");

        private void Awake()
        {
            // コンポーネントの取得
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
            if(_cameraBehavior != null)
            {
                _cameraBehavior.SetAds(false);   
            }
        }

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            Camera mainCam = Camera.main;
            if (mainCam != null)
            {
                _cameraTransform = mainCam.transform;
                InjectCameraReference(mainCam);
            }

            if (_rb != null)
            {
                // Rigidbodyの回転を禁止
                _rb.constraints = RigidbodyConstraints.FreezeRotation;
                // 物理移動を滑らかにするための補間を有効化
                _rb.interpolation = RigidbodyInterpolation.Interpolate;
            }

            // 開始直後に意図しない向きへ回らないよう、今の向きから始める
            _targetRotation = transform.rotation;
        }

        // Update is called once per frame
        void Update()
        {
            // 入力の取得 (W/A/S/D または スティック)
            Vector2 input = _actions.Player.Move.ReadValue<Vector2>();
            float inputMagnitude = Mathf.Clamp01(input.magnitude);

            // カメラ基準の移動方向ベクトルを計算（XZ平面）
            if (_cameraTransform != null)
            {
                Vector3 camForward = _cameraTransform.forward;
                Vector3 camRight = _cameraTransform.right;
                camForward.y = 0f;
                camRight.y = 0f;
                camForward.Normalize();
                camRight.Normalize();

                Vector3 moveVector = camForward * input.y + camRight * input.x;
                if (moveVector.sqrMagnitude > MinMoveSqrMagnitude)
                {
                    _moveDirection = moveVector.normalized;
                }
                else
                {
                    _moveDirection = Vector3.zero;
                }
            }

            // 速度計算（ダッシュデフォルト ＆ スティック倒し量 ＆ キーボード歩行切り替え）
            bool isModifierPressed = _actions.Player.Sprint.IsPressed();
            bool isWalkingMode = _walkOnModifierKey ? isModifierPressed : !isModifierPressed;

            float maxSpeedForCurrentMode = isWalkingMode ? _walkSpeed : _runSpeed;
            _currentMoveSpeed = maxSpeedForCurrentMode * inputMagnitude;

            // 0.0(停止) 〜 1.0(最高速ダッシュ) の正規化速度
            float normalizedSpeed = _runSpeed > 0f ? (_currentMoveSpeed / _runSpeed) : 0f;

            // キャラクターの目標の向き（実際の回転はFixedUpdateで行う）
            // ADS中はカメラの左右の向きに合わせ、それ以外は移動方向へ向ける
            _isAiming = _cameraBehavior != null && _actions.Player.FocusAim.IsPressed();
            if (_isAiming)
            {
                _targetRotation = Quaternion.Euler(0f, _cameraBehavior.Yaw, 0f);
            }
            else if (_moveDirection.sqrMagnitude > MinMoveSqrMagnitude)
            {
                _targetRotation = Quaternion.LookRotation(_moveDirection, Vector3.up);
            }

            // アニメーション更新
            UpdateAnimator(normalizedSpeed);

            // ジャンプの検知
            bool isGrounded = _groundChecker != null && _groundChecker.IsGrounded;
            if (_actions.Player.Jump.triggered && isGrounded)
            {
                _jumpRequested = true;
                if (_animator != null)
                {
                    _animator.SetBool(AnimParamIsJumping, true);
                }
            }
            else if (isGrounded)
            {
                // 接地中はジャンプ状態を解除
                if (_animator != null)
                {
                    _animator.SetBool(AnimParamIsJumping, false);
                }
            }

            // ADS
            if(_cameraBehavior != null)
            {
                _cameraBehavior.SetAds(_isAiming);
            }

            // フォノンキネシスが起動中かどうか（Toggle前後どちらでも検知できるよう記録）
            bool wasKinesisActive = _phononkinesis != null && _phononkinesis.IsActive;

            // フォノンキネシス
            if (_phononkinesis != null)
            {
                if (_actions.Player.LayoutMode.triggered)
                {
                    _phononkinesis.TogglePhononkinesis();
                }
            }

            bool isKinesisActive = wasKinesisActive || (_phononkinesis != null && _phononkinesis.IsActive);

            // 波照射
            if (_waveEmitter != null)
            {
                if (isKinesisActive)
                {
                    // サーチ中・ホールド中は発射・切り替えを無効化し、発射ボタン離し待ちにする
                    _isWaitingFireRelease = true;
                    _waveEmitter.SetTriggerHeld(false);
                }
                else
                {
                    // 配置確定時などに押された発射ボタン（左クリック等）が一度離されるまで待機
                    if (_isWaitingFireRelease && !_actions.Player.Fire.IsPressed())
                    {
                        _isWaitingFireRelease = false;
                    }

                    bool canUseWave = !_isWaitingFireRelease;
                    _waveEmitter.SetTriggerHeld(canUseWave && _actions.Player.Fire.IsPressed());

                    if (canUseWave)
                    {
                        if (_actions.Player.SwitchWave.triggered)
                        {
                            _waveEmitter.SelectNextSource();
                        }
                        if (_actions.Player.ToggleFireMode.triggered)
                        {
                            _waveEmitter.ToggleFireShape();
                        }
                    }
                }
            }

            // EchoLocation
            if (_echoController != null)
            {
                if (_actions.Player.EchoScan.triggered)
                {
                    _echoController.ToggleScan();
                }
            }
        }

        /// <summary>
        /// アニメーションパラメータの更新
        /// Blend Tree（Speed Float）と既存のBoolステートの両方に対応するハイブリッド仕様
        /// </summary>
        private void UpdateAnimator(float normalizedSpeed)
        {
            if (_animator == null) return;

            // Blend Tree 用: 0.0 (Idle) ~ 0.4 (Walk) ~ 1.0 (Run)
            _animator.SetFloat(AnimParamSpeed, normalizedSpeed, _animDampTime, Time.deltaTime);

        }

        /// <summary>
        /// 物理演算の更新（毎フレーム安定した力・速度を適用）
        /// </summary>
        void FixedUpdate()
        {
            // 1. 水平方向の物理移動（linearVelocity）
            Vector3 targetVelocity = _moveDirection * _currentMoveSpeed;
            _rb.linearVelocity = new Vector3(targetVelocity.x, _rb.linearVelocity.y, targetVelocity.z);

            // 2. 回転（Transformを直接書き換えると補間に上書きされるため、Rigidbody経由で回す）
            _rb.MoveRotation(_isAiming
                ? _targetRotation
                : Quaternion.RotateTowards(_rb.rotation, _targetRotation, _rotateSpeed * Time.fixedDeltaTime));

            // 3. ジャンプの物理適用
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
            // CameraBehavior コンポーネントを取得
            _cameraBehavior = cam.GetComponent<CameraBehavior>();
            if (_cameraBehavior != null)
            {
                _cameraBehavior.SetAdsTransforms(_adsPosition, _aimPivot);
            }

            // WaveEmitter へ照準カメラを渡す
            if (_waveEmitter != null)
            {
                _waveEmitter.AimOrigin = cam.transform;
            }
            // EchoController へ描画カメラを渡す
            if (_echoController != null)
            {
                _echoController.TargetCamera = cam;
                if(_echoController.DirectionTransform == null)
                {
                    _echoController.DirectionTransform = cam.transform; // 正面基準もカメラに
                }
            }
            // Phononkinesis へカメラを渡す
            if (_phononkinesis != null)
            {
                _phononkinesis.CameraTransform = cam.transform;
            }
        }
    }
}
