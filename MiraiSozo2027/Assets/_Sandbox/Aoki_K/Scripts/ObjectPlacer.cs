using UnityEngine;
using UnityEngine.InputSystem;

public class ObjectPlacer : MonoBehaviour
{
    [Header("Prefabs")]
    [Tooltip("仮表示（プレビュー）用の半透明プレハブ")]
    [SerializeField] private GameObject _previewPrefab;

    [Tooltip("確定時に生成される不透明プレハブ")]
    [SerializeField] private GameObject _placedPrefab;

    [Header("Placement Settings")]
    [Tooltip("カメラからの設置距離（メートル）")]
    [SerializeField] private float _distanceFromCamera = 6.0f;
    [Header("最小距離")]
    [SerializeField] private float _minDistance = 2.0f;
    [Header("最大距離")]
    [SerializeField] private float _maxDistance = 10.0f;
    
    [Header("距離調整感度")]
    [Tooltip("マウスホイール１ノッチ当たりの移動量（メートル）")]
    [SerializeField] private float _wheelStep = 0.1f;
    [Tooltip("コントローラーボタンの長押し時の移動速度（メートル/秒）")]
    [SerializeField] private float _controllerSpeed = 1.0f;

    [Tooltip("プレイヤーの頭")]
    [SerializeField] private Transform _targetHeadTransform;

    [Tooltip("追従の基準となるカメラ（未アサイン時はメインカメラ）")]
    [SerializeField] private Transform _cameraTransform;

    [SerializeField] private bool _placeMode = false;
    public bool IsPlaceMode => _placeMode;

    private GameObject _previewInstance;
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

    void Start()
    {
        // カメラの自動取得
        if (_cameraTransform == null && Camera.main != null)
        {
            _cameraTransform = Camera.main.transform;
        }

        // 仮の球オブジェクトを1つだけ生成して保持
        if (_previewPrefab != null)
        {
            _previewInstance = Instantiate(_previewPrefab);
            _previewInstance.SetActive(false);
            
            // プレビューオブジェクト自体のコライダーがレイキャストや物理に干渉しないよう無効化
            Collider col = _previewInstance.GetComponent<Collider>();
            if (col != null)
            {
                col.enabled = false;
            }
        }
    }

    void Update()
    {
        if (_cameraTransform == null) return;
        if(!_placeMode) return;

        // 1. カメラの向いている方向の一定距離先を計算
        HandleDistanceAdjustment();
        Vector3 targetPosition;
        if(GameSettings.Instance != null && GameSettings.Instance.IsFirstPersonMode)
        {
            targetPosition = _cameraTransform.position + (_cameraTransform.forward * _distanceFromCamera);
        }
        else if (_targetHeadTransform != null)
        {
            //Vector3 cameraToTarget = Vector3.Normalize(_targetHeadTransform.position - _cameraTransform.position);
            targetPosition = _targetHeadTransform.position + (_cameraTransform.forward * _distanceFromCamera);
        }
        else
        {
            targetPosition = _cameraTransform.position + (_cameraTransform.forward * _distanceFromCamera);
        }

        // 2. 仮オブジェクトをその座標へ追従させる
        if (_previewInstance != null)
        {
            _previewInstance.transform.position = targetPosition;
            // 必要に応じてカメラの向きと回転を合わせる
            _previewInstance.transform.rotation = _cameraTransform.rotation;
        }

        // 3. 確定入力
        if (_actions.Player.Confirmed.triggered)
        {
            PlaceObject(targetPosition);
        }
    }

    /// <summary>
    /// 確定用オブジェクトを生成
    /// </summary>
    private void PlaceObject(Vector3 position)
    {
        if (_placedPrefab == null) return;

        // 確定オブジェクトをインスタンス化
        Instantiate(_placedPrefab, position, Quaternion.identity);
    }

    private void HandleDistanceAdjustment()
    {
        // マウスホイール
        if(Mouse.current != null)
        {
            float mouseScrollY = _actions.Player.ScrollDistance.ReadValue<Vector2>().y;
            if(Mathf.Abs(mouseScrollY) > 0.01f)
            {
                float scrollDir = Mathf.Sign(mouseScrollY);
                _distanceFromCamera += scrollDir * _wheelStep;
            }
        }

        // コントローラー
        float controllerAxis = _actions.Player.AdjustDistance.ReadValue<float>();
        if(Mathf.Abs(controllerAxis) > 0.01f)
        {
            _distanceFromCamera += controllerAxis * _controllerSpeed * Time.deltaTime;
        }

        // 距離制限
        _distanceFromCamera = Mathf.Clamp(_distanceFromCamera, _minDistance, _maxDistance);
    }

    private void OnDestroy()
    {
        // シーン遷移やオブジェクト破棄時にプレビューも片付ける
        if (_previewInstance != null)
        {
            Destroy(_previewInstance);
        }
    }

    public void TogglePlaceMode()
    {
        _placeMode = !_placeMode;
        if(!_placeMode)
            _previewInstance.SetActive(false);
        else
            _previewInstance.SetActive(true);
    }
}
