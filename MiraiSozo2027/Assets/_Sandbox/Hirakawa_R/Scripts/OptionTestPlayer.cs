using UnityEngine;

namespace MiraiSozo.Options
{
    /// <summary>OptionTestScene専用。保存した移動・ジャンプキーとカメラ設定を確認する。</summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class OptionTestPlayer : MonoBehaviour
    {
        private CharacterController _controller;
        private float _verticalSpeed;

        private void Awake() => _controller = GetComponent<CharacterController>();

        private void Update()
        {
            if (OptionGameManager.Actions == null || OptionGameManager.Instance.IsPaused) return;
            var camera = Camera.main;
            if (camera == null) return;
            Vector2 input = OptionGameManager.Actions.Player.Move.ReadValue<Vector2>();
            Vector3 forward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(camera.transform.right, Vector3.up).normalized;
            var movement = Vector3.ClampMagnitude(forward * input.y + right * input.x, 1f);
            if (_controller.isGrounded)
            {
                _verticalSpeed = -1f;
                if (OptionGameManager.Actions.Player.Jump.WasPressedThisFrame()) _verticalSpeed = 5f;
            }
            _verticalSpeed += Physics.gravity.y * Time.deltaTime;
            _controller.Move((movement * 4f + Vector3.up * _verticalSpeed) * Time.deltaTime);
            if (transform.position.y < -10f)
            {
                _controller.enabled = false;
                transform.position = new Vector3(0, 1.1f, 0);
                _controller.enabled = true;
                _verticalSpeed = 0;
            }
        }
    }
}
