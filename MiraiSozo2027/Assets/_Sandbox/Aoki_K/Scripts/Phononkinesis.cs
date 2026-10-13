using UnityEngine;
using UnityEngine.InputSystem;


[RequireComponent(typeof(LineRenderer))]
public class Phononkinesis : MonoBehaviour
{
    public enum State
    {
        Inactive,   // 通常時（能力OFF）
        Searching,  // オブジェクト探索中（能力ON・未選択）
        Holding     // オブジェクト掴み・運搬中
    }

    [Header("State")]
    [SerializeField] private State _currentState = State.Inactive;
    public State CurrentState => _currentState;

    [Header("Raycast & Detection")]
    [Tooltip("掴める最大射程距離（メートル）")]
    [SerializeField] private float _maxRaycastDistance = 20.0f;
    [Tooltip("掴む対象とするレイヤー")]
    [SerializeField] private LayerMask _grabbableLayer = ~0;

    [Header("Distance & Movement Settings")]
    [Tooltip("現在の距離（メートル）")]
    [SerializeField] private float _currentDistance = 6.0f;
    [SerializeField] private float _minDistance = 2.0f;
    [SerializeField] private float _maxDistance = 25.0f;

    [Header("Distance Adjustment Sensitivity (ObjectPlacer流用)")]
    [Tooltip("マウスホイール1ノッチあたりの移動量（メートル）")]
    [SerializeField] private float _wheelStep = 0.2f;
    [Tooltip("コントローラーボタン長押し時の移動速度（メートル/秒）")]
    [SerializeField] private float _controllerSpeed = 2.0f;

    [Header("Physics Settings (重さと追従挙動)")]
    [Tooltip("追従の基準速度")]
    [SerializeField] private float _baseFollowSpeed = 15.0f;
    [Tooltip("重さによる影響度")]
    [SerializeField] private float _massWeightFactor = 0.5f;
    [Tooltip("追従時の最大速度制限")]
    [SerializeField] private float _maxVelocity = 25.0f;

    [Header("Visual Effects（レイの可視化）")]
    [Tooltip("ビームの太さ")]
    [SerializeField] private float _beamWidth = 0.1f;
    [Tooltip("探索中のビーム色")]
    [SerializeField] private Color _searchColor = new Color(0.2f, 0.8f, 1.0f, 0.5f);
    [Tooltip("掴み中のビーム色")]
    [SerializeField] private Color _lockColor = new Color(1.0f, 0.6f, 0.0f, 0.6f);

    [Header("References")]
    [Tooltip("追従の基準となるカメラ（未アサイン時はメインカメラ）")]
    [SerializeField] private Transform _cameraTransform;
    [Tooltip("プレイヤーの頭（三人称時の基準点）")]
    [SerializeField] private Transform _targetHeadTransform;

    // 内部変数
    private InputSystem_Actions _actions;
    private PhononkinesisTarget _heldTarget;
    private PhononkinesisTarget _currentTarget;
    private RaycastHit _currentHit;
    private bool _hasTarget;

    // 掴んだ瞬間のローカルオフセット（横・縦）
    private float _localOffsetX;
    private float _localOffsetY;
    
    // 掴んだ際の回転状態
    private Quaternion _targetRotation;

    private LineRenderer _lineRenderer;

    public PhononkinesisTarget HeldTarget => _heldTarget;
    public bool HasTarget => _hasTarget;

    private void Awake()
    {
        _actions = new InputSystem_Actions();
        SetupLineRenderer();
    }

    private void OnEnable()
    {
        _actions.Enable();
    }

    private void OnDisable()
    {
        if (_currentState == State.Holding)
        {
            ReleaseObject();
        }
        _actions.Disable();
    }

    private void Start()
    {
        if (_cameraTransform == null && Camera.main != null)
        {
            _cameraTransform = Camera.main.transform;
        }
    }

    private void Update()
    {
        if (_currentState == State.Inactive)
        {
            if(_lineRenderer != null)
                _lineRenderer.enabled = false;
            return;
        }
        if (_cameraTransform == null) return;

        switch (_currentState)
        {
            case State.Searching:
                UpdateSearching();
                break;

            case State.Holding:
                UpdateHolding();
                break;
        }
        // ビーム見た目更新
        UpdateLaserVisual();
    }

    private void FixedUpdate()
    {
        if (_currentState == State.Holding && _heldTarget != null && _heldTarget.Rb != null)
        {
            ApplyPhysicsFollow();
        }
    }

    public void TogglePhononkinesis()
    {
        if (_currentState == State.Inactive)
        {
            _currentState = State.Searching;
        }
        else
        {
            if (_currentState == State.Holding)
            {
                ReleaseObject();
            }
            _currentState = State.Inactive;
            if(_lineRenderer != null)
                _lineRenderer.enabled = false;
        }
    }

    /// <summary>
    /// ① 探索状態
    /// </summary>
    private void UpdateSearching()
    {
        Ray ray = new Ray(_cameraTransform.position, _cameraTransform.forward);
        _hasTarget = Physics.Raycast(ray, out _currentHit, _maxRaycastDistance, _grabbableLayer);

        if (_hasTarget)
        {
            PhononkinesisTarget target = _currentHit.collider.GetComponentInParent<PhononkinesisTarget>();

            if (target != null && target.CanBeGrabbed)
            {
                _currentTarget = target;

                // 掴むボタン (Confirmed)
                if (_actions.Player.Confirmed.triggered)
                {
                    GrabObject(_currentTarget);
                }
                return;
            }
        }

        _currentTarget = null;
        _hasTarget = false;
    }

    /// <summary>
    /// オブジェクトを掴む（現在の位置を完全に維持して初期化）
    /// </summary>
    private void GrabObject(PhononkinesisTarget target)
    {
        _heldTarget = target;
        _heldTarget.OnGrabbed();

        _targetRotation = _heldTarget.Rb.rotation;

        // 基準点（一人称ならカメラ、三人称なら頭）
        Vector3 origin = GetOriginPosition();
        Vector3 toObj = _heldTarget.Rb.position - origin;

        // カメラのローカル軸（X:横, Y:縦, Z:前方）に分解して初期オフセットを記録
        _localOffsetX = Vector3.Dot(toObj, _cameraTransform.right);
        _localOffsetY = Vector3.Dot(toObj, _cameraTransform.up);
        _currentDistance = Vector3.Dot(toObj, _cameraTransform.forward);

        // クランプ範囲を現在距離に応じて安全に調整
        if (_currentDistance > _maxDistance) _maxDistance = _currentDistance + 5f;
        if (_currentDistance < _minDistance) _minDistance = Mathf.Max(1.0f, _currentDistance);

        _currentState = State.Holding;
    }

    /// <summary>
    /// ② 運搬状態
    /// </summary>
    private void UpdateHolding()
    {
        if (_heldTarget == null)
        {
            _currentState = State.Searching;
            return;
        }

        // ホイールやコントローラーでの距離調整
        HandleDistanceAdjustment();

        // 配置確定ボタン (Confirmed)
        if (_actions.Player.Confirmed.triggered)
        {
            ReleaseObject();
            _currentState = State.Inactive;
            if(_lineRenderer != null)
                _lineRenderer.enabled = false;
        }
    }

    /// <summary>
    /// 物理追従の計算
    /// </summary>
    private void ApplyPhysicsFollow()
    {
        Rigidbody rb = _heldTarget.Rb;
        Vector3 targetPos = CalculateTargetPosition();
        Vector3 diff = targetPos - rb.position;

        float mass = Mathf.Max(1.0f, rb.mass);
        float followSpeed = _baseFollowSpeed / (1.0f + mass * _massWeightFactor);

        Vector3 desiredVelocity = diff * followSpeed;
        if (desiredVelocity.sqrMagnitude > _maxVelocity * _maxVelocity)
        {
            desiredVelocity = desiredVelocity.normalized * _maxVelocity;
        }

        rb.linearVelocity = desiredVelocity;
        rb.MoveRotation(_targetRotation);
        rb.angularVelocity = Vector3.zero;
    }

    /// <summary>
    /// 目標座標の計算（掴んだ時の相対位置 + 距離調整）
    /// </summary>
    private Vector3 CalculateTargetPosition()
    {
        Vector3 origin = GetOriginPosition();

        // 基準点 + (横オフセット) + (縦オフセット) + (前方距離)
        return origin 
             + (_cameraTransform.right * _localOffsetX) 
             + (_cameraTransform.up * _localOffsetY) 
             + (_cameraTransform.forward * _currentDistance);
    }

    /// <summary>
    /// 視点に応じた基準点の取得
    /// </summary>
    private Vector3 GetOriginPosition()
    {
        if (GameSettings.Instance != null && GameSettings.Instance.IsFirstPersonMode)
        {
            return _cameraTransform.position;
        }
        else if (_targetHeadTransform != null)
        {
            return _targetHeadTransform.position;
        }
        else
        {
            return _cameraTransform.position;
        }
    }

    private void HandleDistanceAdjustment()
    {
        if (Mouse.current != null)
        {
            float mouseScrollY = _actions.Player.ScrollDistance.ReadValue<Vector2>().y;
            if (Mathf.Abs(mouseScrollY) > 0.01f)
            {
                float scrollDir = Mathf.Sign(mouseScrollY);
                _currentDistance += scrollDir * _wheelStep;
            }
        }

        float controllerAxis = _actions.Player.AdjustDistance.ReadValue<float>();
        if (Mathf.Abs(controllerAxis) > 0.01f)
        {
            _currentDistance += controllerAxis * _controllerSpeed * Time.deltaTime;
        }

        _currentDistance = Mathf.Clamp(_currentDistance, _minDistance, _maxDistance);
    }

    private void ReleaseObject()
    {
        if (_heldTarget != null)
        {
            _heldTarget.OnReleased();
            _heldTarget = null;
        }
    }

    /// <summary>
    /// ★ LineRenderer の初期セットアップ
    /// </summary>
    private void SetupLineRenderer()
    {
        _lineRenderer = GetComponent<LineRenderer>();
        _lineRenderer.positionCount = 2;
        _lineRenderer.startWidth = _beamWidth;
        _lineRenderer.endWidth = _beamWidth;
        _lineRenderer.useWorldSpace = true;
        _lineRenderer.enabled = false;
        // ★頂点カラーを素直に反映する Sprites/Default シェーダーを優先して適用
        Shader spriteShader = Shader.Find("Sprites/Default");
        if (spriteShader != null)
        {
            _lineRenderer.material = new Material(spriteShader);
        }
    }

    /// <summary>
    /// ★ Gameビュー用のレイ描画ロジック
    /// </summary>
    private void UpdateLaserVisual()
    {
        if (_lineRenderer == null) return;
        // 始点: 三人称ならプレイヤーの頭付近、一人称ならカメラ
        Vector3 startPos = (_targetHeadTransform != null) ? _targetHeadTransform.position : _cameraTransform.position;
        if (_currentState == State.Searching)
        {
            _lineRenderer.enabled = true;
            _lineRenderer.startWidth = _beamWidth;
            _lineRenderer.endWidth = _beamWidth;
            // 終点: 対象があればその衝突点、なければ射程限界まで
            Vector3 endPos = _hasTarget ? _currentHit.point : _cameraTransform.position + (_cameraTransform.forward * _maxRaycastDistance);
            _lineRenderer.SetPosition(0, startPos);
            _lineRenderer.SetPosition(1, endPos);
            // ★色の適用
            Color color = _hasTarget ? _lockColor : _searchColor;
            ApplyLineColor(color);
        }
        else if (_currentState == State.Holding && _heldTarget != null)
        {
            // 運搬中: プレイヤーとオブジェクトの中心をビームで繋ぎ続ける
            _lineRenderer.enabled = true;
            _lineRenderer.SetPosition(0, startPos);
            _lineRenderer.SetPosition(1, _heldTarget.Rb.position);
            // ★色の適用（掴み中の色）
            ApplyLineColor(_lockColor);
        }
        else
        {
            _lineRenderer.enabled = false;
        }
    }

    /// <summary>
    /// 頂点カラーとマテリアルプロパティの両方に色を流し込む安全な色変更
    /// </summary>
    private void ApplyLineColor(Color color)
    {
        // 1. 頂点カラーの更新
        _lineRenderer.startColor = color;
        _lineRenderer.endColor = color;
        // 2. マテリアルカラーの直接更新（どのシェーダーでも確実に発色させるための対策）
        if (_lineRenderer.material != null)
        {
            _lineRenderer.material.color = color;
            if (_lineRenderer.material.HasProperty("_BaseColor"))
            {
                _lineRenderer.material.SetColor("_BaseColor", color);
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (_currentState == State.Searching && _cameraTransform != null)
        {
            Gizmos.color = _hasTarget ? Color.green : Color.red;
            Gizmos.DrawRay(_cameraTransform.position, _cameraTransform.forward * _maxRaycastDistance);
        }
    }
}
