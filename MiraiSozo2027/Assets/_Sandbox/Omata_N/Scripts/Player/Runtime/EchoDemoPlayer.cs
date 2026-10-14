// 仮プレイヤーの移動とカメラ。スキャンの入力・状態は既存のControllerへ分離する。
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Layouts;
using UnityEngine.Rendering;

namespace Echo.Echolocation.Samples
{
    /// <summary>衝突付きの移動、マウス視点、一人称／三人称切り替え。</summary>
    [DisallowMultipleComponent, RequireComponent(typeof(CharacterController)), DefaultExecutionOrder(100)]
    public sealed class EchoDemoPlayer : MonoBehaviour
    {
        [SerializeField, Tooltip("目の位置。上下回転だけを適用します。")]
        [UnityEngine.Serialization.FormerlySerializedAs("lookPivot")]
        private Transform _lookPivot;
        [SerializeField, Tooltip("両視点で共用するCameraです。")]
        [UnityEngine.Serialization.FormerlySerializedAs("viewCamera")]
        private Camera _viewCamera;
        [SerializeField, Tooltip("一人称では影だけを残すモデルです。")]
        [UnityEngine.Serialization.FormerlySerializedAs("bodyRenderers")]
        private Renderer[] _bodyRenderers = new Renderer[0];
        [SerializeField, Min(0), Tooltip("WASD／左スティックによる移動速度（m/秒）。")]
        [UnityEngine.Serialization.FormerlySerializedAs("moveSpeed")]
        private float _moveSpeed = 5;
        [SerializeField, Min(.001f), Tooltip("マウス1ピクセルあたりの回転角（度）。")]
        [UnityEngine.Serialization.FormerlySerializedAs("mouseSensitivity")]
        private float _mouseSensitivity = .12f;
        [SerializeField, Min(0), Tooltip("右スティックによる最大回転速度（度/秒）。")]
        [UnityEngine.Serialization.FormerlySerializedAs("gamepadLookSpeed")]
        private float _gamepadLookSpeed = 120;
        [SerializeField, Range(10, 89), Tooltip("上下の視点角制限（度）。")]
        [UnityEngine.Serialization.FormerlySerializedAs("pitchLimit")]
        private float _pitchLimit = 80;
        [SerializeField, Min(.1f), Tooltip("下向きの加速度（m/秒²）。")]
        [UnityEngine.Serialization.FormerlySerializedAs("gravity")]
        private float _gravity = 20;
        [SerializeField, Min(.5f), Tooltip("三人称Cameraの目からの距離（m）。障害物の手前まで短縮します。")]
        [UnityEngine.Serialization.FormerlySerializedAs("thirdPersonDistance")]
        private float _thirdPersonDistance = 4.5f;
        [SerializeField, Tooltip("三人称Cameraの追加の高さ（m）。世界の上方向へずらします。0で従来の位置、負の値で低くなります。一人称には影響しません。")]
        [UnityEngine.Serialization.FormerlySerializedAs("thirdPersonHeight")]
        private float _thirdPersonHeight;
        [SerializeField, Range(.05f, .5f), Tooltip("カメラの壁抜け防止用半径（m）。")]
        [UnityEngine.Serialization.FormerlySerializedAs("cameraCollisionRadius")]
        private float _cameraCollisionRadius = .2f;
        [SerializeField, Tooltip("カメラの障害物レイヤー。自分自身とTriggerは除外します。")]
        [UnityEngine.Serialization.FormerlySerializedAs("cameraCollisionLayers")]
        private LayerMask _cameraCollisionLayers = ~0;
        [SerializeField, Tooltip("再生開始時から三人称にします。")]
        [UnityEngine.Serialization.FormerlySerializedAs("startThirdPerson")]
        private bool _isThirdPersonOnStart;
        [SerializeField, Tooltip("開始時にカーソルを固定します。Escで解放、左クリックで再固定できます。")]
        [UnityEngine.Serialization.FormerlySerializedAs("lockCursorOnStart")]
        private bool _isCursorLockedOnStart = true;
        [SerializeField, InputControl(layout = "Button"), Tooltip("視点を切り替える操作。初期値Z。変更は次回再生時に反映します。")]
        [UnityEngine.Serialization.FormerlySerializedAs("viewTogglePath")]
        private string _viewTogglePath = "<Keyboard>/z";
        private CharacterController _motor;
        private InputAction _move;
        private InputAction _mouseLook;
        private InputAction _padLook;
        private InputAction _toggleView;
        private float _yaw;
        private float _pitch;
        private float _verticalVelocity;
        private bool _isFocused = true;
        private bool _isLookSkipped;
        private bool _hasCursorOwnership;
        private ShadowCastingMode[] _originalShadows;
        private readonly RaycastHit[] _cameraHits = new RaycastHit[128];
        /// <summary>現在の三人称状態。スキャンとは独立する。</summary>
        public bool IsThirdPerson { get; private set; }
        /// <summary>高さも含めた、目の位置からCameraまでの実際の直線距離（m）。</summary>
        public float CurrentCameraDistance { get; private set; }

        /// <summary>三人称だけに加える世界の上方向の高さ（m）。変更は即時にカメラへ反映する。</summary>
        public float ThirdPersonHeight
        {
            get => _thirdPersonHeight;
            set
            {
                _thirdPersonHeight = float.IsFinite(value) ? value : 0;
                UpdateCamera();
            }
        }

        /// <summary>共用するCamera。</summary>
        public Camera ViewCamera => _viewCamera;

        /// <summary>セットアップ時にCameraとモデルを接続する。</summary>
        public void ConfigureRig(Transform pivot, Camera camera, Renderer[] renderers)
        {
            _lookPivot = pivot;
            _viewCamera = camera;
            _bodyRenderers = renderers ?? new Renderer[0];
        }

        void OnValidate()
        {
            _moveSpeed = Mathf.Max(0, _moveSpeed);
            _thirdPersonDistance = Mathf.Max(.5f, _thirdPersonDistance);
            if (!float.IsFinite(_thirdPersonHeight))
                _thirdPersonHeight = 0;
            _mouseSensitivity = Mathf.Max(.001f, _mouseSensitivity);
            _cameraCollisionRadius = Mathf.Clamp(_cameraCollisionRadius, .05f, .5f);
        }

        void OnEnable()
        {
            _motor = GetComponent<CharacterController>();
            _yaw = transform.eulerAngles.y;
            _pitch = _lookPivot ? Mathf.DeltaAngle(0, _lookPivot.localEulerAngles.x) : 0;
            _originalShadows = new ShadowCastingMode[_bodyRenderers.Length];
            for (int i = 0; i < _bodyRenderers.Length; i++)
                if (_bodyRenderers[i])
                    _originalShadows[i] = _bodyRenderers[i].shadowCastingMode;
            _move = new InputAction("移動", InputActionType.Value, expectedControlType: "Vector2");
            _move.AddCompositeBinding("2DVector").With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s").With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            _move.AddBinding("<Gamepad>/leftStick");
            _mouseLook = new InputAction("マウス視点", InputActionType.Value, "<Mouse>/delta");
            _padLook = new InputAction("スティック視点", InputActionType.Value, "<Gamepad>/rightStick");
            _toggleView = new InputAction("視点切替", InputActionType.Button, _viewTogglePath);
            _move.Enable();
            _mouseLook.Enable();
            _padLook.Enable();
            _toggleView.Enable();
            _isFocused = Application.isFocused || Application.isBatchMode;
            _isLookSkipped = true;
            _verticalVelocity = 0;
            SetThirdPerson(_isThirdPersonOnStart);
        }

        void Start()
        {
            if (_isCursorLockedOnStart && _isFocused)
                CaptureCursor();
        }

        void CaptureCursor()
        {
            _isLookSkipped = true;
            // 自動試験ではデスクトップのカーソルを変更しない。
            if (Application.isBatchMode)
                return;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            _hasCursorOwnership = true;
        }

        void ReleaseCursor()
        {
            if (!_hasCursorOwnership)
                return;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            _hasCursorOwnership = false;
        }

        void OnApplicationFocus(bool isValue)
        {
            _isFocused = isValue || Application.isBatchMode;
            if (!_isFocused)
                ReleaseCursor();
            _isLookSkipped = true;
        }

        void OnDisable()
        {
            _move?.Dispose();
            _mouseLook?.Dispose();
            _padLook?.Dispose();
            _toggleView?.Dispose();
            ReleaseCursor();
            if (_originalShadows != null)
                for (int i = 0; i < _bodyRenderers.Length && i < _originalShadows.Length; i++)
                    if (_bodyRenderers[i])
                        _bodyRenderers[i].shadowCastingMode = _originalShadows[i];
        }

        void Update()
        {
            if (!_lookPivot || !_viewCamera || !_isFocused)
                return;
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                ReleaseCursor();
                return;
            }

            bool isCaptured = Application.isBatchMode || Cursor.lockState == CursorLockMode.Locked;
            if (!isCaptured && Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                CaptureCursor();
                return;
            }

            if (_toggleView.WasPressedThisFrame())
                SetThirdPerson(!IsThirdPerson);
            if (isCaptured)
            {
                if (_isLookSkipped)
                    _isLookSkipped = false;
                else
                    ApplyLook(_mouseLook.ReadValue<Vector2>() * _mouseSensitivity + _padLook.ReadValue<Vector2>() * (_gamepadLookSpeed * Time.deltaTime));
            }

            Move(isCaptured ? _move.ReadValue<Vector2>() : Vector2.zero, Time.deltaTime);
        }

        /// <summary>角度差を加える。マウス変位にはdeltaTimeを二重乗算しない。</summary>
        public void ApplyLook(Vector2 degrees)
        {
            _yaw = Mathf.Repeat(_yaw + degrees.x, 360);
            _pitch = Mathf.Clamp(_pitch - degrees.y, -_pitchLimit, _pitchLimit);
            transform.rotation = Quaternion.Euler(0, _yaw, 0);
            if (_lookPivot)
                _lookPivot.localRotation = Quaternion.Euler(_pitch, 0, 0);
        }

        /// <summary>視点の水平向きへ移動し、重力と衝突をCharacterControllerで処理する。</summary>
        public void Move(Vector2 input, float deltaTime)
        {
            if (!_motor)
                _motor = GetComponent<CharacterController>();
            if (!_motor.enabled || deltaTime <= 0)
                return;
            input = Vector2.ClampMagnitude(input, 1);
            if (_motor.isGrounded && _verticalVelocity < 0)
                _verticalVelocity = -2;
            _verticalVelocity = Mathf.Max(_verticalVelocity - _gravity * deltaTime, -50);
            Vector3 horizontal = (transform.right * input.x + transform.forward * input.y) * _moveSpeed;
            var flags = _motor.Move((horizontal + Vector3.up * _verticalVelocity) * deltaTime);
            if ((flags & CollisionFlags.Below) != 0 && _verticalVelocity < 0)
                _verticalVelocity = -2;
        }

        /// <summary>同じCameraで視点を切り替える。スキャン中心と向き参照を変更しない。</summary>
        public void SetThirdPerson(bool isValue)
        {
            IsThirdPerson = isValue;
            foreach (var r in _bodyRenderers)
                if (r)
                    r.shadowCastingMode = isValue ? ShadowCastingMode.On : ShadowCastingMode.ShadowsOnly;
            UpdateCamera();
        }

        void LateUpdate() => UpdateCamera();
        void UpdateCamera()
        {
            if (!_lookPivot || !_viewCamera)
                return;
            // 上下に視点を振っても、高さ設定そのものは世界の上方向へ独立して加える。
            Vector3 offset = IsThirdPerson ? -_lookPivot.forward * _thirdPersonDistance + Vector3.up * _thirdPersonHeight : Vector3.zero;
            float distance = offset.magnitude;
            Vector3 direction = distance > 0 ? offset / distance : Vector3.zero;
            if (distance > 0)
            {
                // 高さを適用した最終位置までの斜めの経路を調べ、天井・壁も手前で止める。
                int count = Physics.SphereCastNonAlloc(_lookPivot.position, _cameraCollisionRadius, direction, _cameraHits, distance, _cameraCollisionLayers, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                    if (!_cameraHits[i].transform.IsChildOf(transform))
                        distance = Mathf.Min(distance, Mathf.Max(0, _cameraHits[i].distance - .05f));
            }

            CurrentCameraDistance = distance;
            _viewCamera.transform.SetPositionAndRotation(_lookPivot.position + direction * distance, _lookPivot.rotation);
        }
    }
}
