using UnityEngine;

public class CameraBehavior : MonoBehaviour
{
    private InputSystem_Actions _actions;
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
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    
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
    
    void Start()
    {
        //マウスカーソル非表示
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        //現在のカメラの向きを初期角度として取得
        Vector3 angles = transform.eulerAngles;
        _yaw = angles.y;
        _pitch = angles.x;
    }

    void LateUpdate()
    {
        if (_target == null) return;
        // 1. Input System から Look（マウス移動 / 右スティック）を取得
        Vector2 lookInput = _actions.Player.Look.ReadValue<Vector2>();
        // 2. 角度の加算
        _yaw += lookInput.x * _sensitivityX;
        float verticalDelta = lookInput.y * _sensitivityY * (_invertY ? 1f : -1f);
        _pitch += verticalDelta;
        // 3. 上下角度を制限（地面への潜り込みや真上反転を防止）
        _pitch = Mathf.Clamp(_pitch, _minPitch, _maxPitch);
        // 4. 回転クォータニオンの生成
        Quaternion cameraRotation = Quaternion.Euler(_pitch, _yaw, 0f);
        // 5. 注視点（キャラクターの胸付近）の座標
        Vector3 targetFocusPosition = _target.position + Vector3.up * _targetHeight;
        // 6. カメラの位置を算出（注視点から後ろへ _distance だけ下がった位置）
        if(GameSettings.Instance.IsFirstPersonMode)
        {
            transform.position = _target.position + _targetHeight * Vector3.up;
        }
        else
        {
            Vector3 offset = (cameraRotation * Vector3.right * _sideOffset) 
                            - (cameraRotation * Vector3.forward * _distance);
            transform.position = targetFocusPosition + offset;
        }
        // 7. カメラの向きを適用
        transform.rotation = cameraRotation;
    }
}
