using UnityEngine;

namespace MiraiSozo.Options
{
    public class OptionCameraBehavior : MonoBehaviour
    {
        private InputSystem_Actions _actions => OptionGameManager.Actions;
        private float _yaw;
        private float _pitch;

        [Header("Camera Settings")]
        [Tooltip("三人称時のカメラのオフセット")]
        [SerializeField] private float _distance = 10.0f;
        [Tooltip("右にずらす量")]
        [SerializeField] private float _sideOffset = 0.8f;

        [Header("Target Settings")]
        [SerializeField] private Transform _target;
        [SerializeField] private float _targetHeight = 4.0f;

        [Header("Rotation Settings")]
        [SerializeField] private float _sensitivityX = 0.2f;
        [SerializeField] private float _sensitivityY = 0.2f;
        [SerializeField] private float _minPitch = 30.0f; //見下ろし限界（度）
        [SerializeField] private float _maxPitch = 120.0f; //見上げ限界（度）
        [SerializeField] private bool _invertY = false; //縦方向反転（チェックを入れると上下反転）
        [SerializeField] private bool _invertX = false;

        public float SensitivityX => _sensitivityX;
        public float SensitivityY => _sensitivityY;
        public bool InvertX => _invertX;
        public bool InvertY => _invertY;

        public void ApplyOptions(float sensitivityX, float sensitivityY, bool invertX, bool invertY)
        {
            _sensitivityX = sensitivityX;
            _sensitivityY = sensitivityY;
            _invertX = invertX;
            _invertY = invertY;
        }
        private void Start()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // 初回入力でカメラの向きが飛ばないよう、配置時の角度を引き継ぐ。
            Vector3 angles = transform.eulerAngles;
            _yaw = angles.y;
            _pitch = angles.x;
        }

        private void LateUpdate()
        {
            if (_target == null || _actions == null || (OptionGameManager.Instance != null && OptionGameManager.Instance.IsPaused)) return;
            Vector2 lookInput = _actions.Player.Look.ReadValue<Vector2>();
            _yaw += lookInput.x * _sensitivityX * (_invertX ? -1f : 1f);
            float verticalDelta = lookInput.y * _sensitivityY * (_invertY ? 1f : -1f);
            _pitch += verticalDelta;
            // 地面への潜り込みや真上での反転を避ける。
            _pitch = Mathf.Clamp(_pitch, _minPitch, _maxPitch);
            Quaternion cameraRotation = Quaternion.Euler(_pitch, _yaw, 0f);
            Vector3 targetFocusPosition = _target.position + Vector3.up * _targetHeight;
            if(OptionGameSettings.Instance != null && OptionGameSettings.Instance.IsFirstPersonMode)
            {
                transform.position = _target.position + _targetHeight * Vector3.up;
            }
            else
            {
                Vector3 offset = (cameraRotation * Vector3.right * _sideOffset) 
                                - (cameraRotation * Vector3.forward * _distance);
                transform.position = targetFocusPosition + offset;
            }
            transform.rotation = cameraRotation;
        }
    }
}
