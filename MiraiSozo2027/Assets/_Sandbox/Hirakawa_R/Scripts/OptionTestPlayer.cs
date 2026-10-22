using UnityEngine;

namespace MiraiSozo.Options
{
    /// <summary>Scene_OptionTestScene専用。保存した移動・ジャンプキーとカメラ設定を確認する。</summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class OptionTestPlayer : MonoBehaviour
    {
        [SerializeField] private float _moveSpeed = 4f;
        [SerializeField] private float _jumpSpeed = 5f;
        [SerializeField] private float _groundedSpeed = -1f;
        [SerializeField] private float _respawnThreshold = -10f;
        [SerializeField] private Vector3 _respawnPosition = new Vector3(0f, 1.1f, 0f);
        private CharacterController _controller;
        private Transform _cameraTransform;
        private float _verticalSpeed;

        private void Awake() => _controller = GetComponent<CharacterController>();

        private void Start()
        {
            var mainCamera = Camera.main;
            if (mainCamera != null) _cameraTransform = mainCamera.transform;
        }

        private void Update()
        {
            if (OptionGameManager.Actions == null || OptionGameManager.Instance.IsPaused) return;
            if (_cameraTransform == null) return;
            Vector2 input = OptionGameManager.Actions.Player.Move.ReadValue<Vector2>();
            Vector3 forward = Vector3.ProjectOnPlane(_cameraTransform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(_cameraTransform.right, Vector3.up).normalized;
            var movement = Vector3.ClampMagnitude(forward * input.y + right * input.x, 1f);
            if (_controller.isGrounded)
            {
                _verticalSpeed = _groundedSpeed;
                if (OptionGameManager.Actions.Player.Jump.WasPressedThisFrame()) _verticalSpeed = _jumpSpeed;
            }
            _verticalSpeed += Physics.gravity.y * Time.deltaTime;
            _controller.Move((movement * _moveSpeed + Vector3.up * _verticalSpeed) * Time.deltaTime);
            if (transform.position.y < _respawnThreshold) Respawn();
        }

        private void Respawn()
        {
            _controller.enabled = false;
            transform.position = _respawnPosition;
            _controller.enabled = true;
            _verticalSpeed = 0f;
        }
    }
}
