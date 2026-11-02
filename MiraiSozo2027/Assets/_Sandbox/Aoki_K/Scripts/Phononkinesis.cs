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
    public bool IsActive => _currentState != State.Inactive;

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
    private float _activeMinDistance;
    private float _activeMaxDistance;

    [Header("Distance Adjustment Sensitivity")]
    [Tooltip("マウスホイール1ノッチあたりの移動量（メートル）")]
    [SerializeField] private float _wheelStep = 0.2f;
    [Tooltip("コントローラーボタン長押し時の移動速度（メートル/秒）")]
    [SerializeField] private float _controllerSpeed = 8.0f;

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
    public Transform CameraTransform { get => _cameraTransform; set => _cameraTransform = value; }
    [Tooltip("プレイヤーの頭（三人称時の基準点）")]
    [SerializeField] private Transform _targetHeadTransform;
    [SerializeField] private Transform _playerCenter;
    [SerializeField] private Transform _playerFeet;

    // 内部変数
    private InputSystem_Actions _actions => GameManager.Actions;
    private PhononkinesisTarget _heldTarget;
    private PhononkinesisTarget _currentTarget;
    private RaycastHit _currentHit;
    private bool _hasTarget;

    // 掴んだ瞬間のローカルオフセット（横・縦）
    private float _localOffsetX;
    private float _localOffsetY;
    
    // 掴んだ際の回転状態
    private Quaternion _targetRotation;

    private Collider _playerCollider;
    private Collider _heldTargetCollider;

    private LineRenderer _lineRenderer;

    public PhononkinesisTarget HeldTarget => _heldTarget;
    public bool HasTarget => _hasTarget;

    private void Awake()
    {
        SetupLineRenderer();
        _playerCollider = GetComponent<Collider>();
    }

    private void OnEnable()
    {

    }

    private void OnDisable()
    {
        if (_currentState == State.Holding)
        {
            ReleaseObject();
        }

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
        _heldTargetCollider = _heldTarget.GetComponentInChildren<Collider>();
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
        //遠くでつかんだ場合はその位置を一旦の上限とする
        _activeMaxDistance = Mathf.Max(_maxDistance, _currentDistance);
        //近距離でつかんだ場合は0.5fを最低距離とする
        _activeMinDistance = Mathf.Min(_minDistance, Mathf.Max(0.5f, _currentDistance));

        _currentState = State.Holding;
    }

    /// <summary>
    /// 運搬状態
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
        if(IsPlayerAboveOrRiding() && desiredVelocity.y > 0f)
        {//プレイヤーが上部にいる場合は上昇推力を０
            desiredVelocity.y = 0f;
        }
        if (IsNearOrTouchingPlayer())
        {//プレイヤーに接触している場合はプレイヤーに向かう推力をゼロにする
            //プレイヤーの中心座標
            Vector3 playerCenter = _playerCenter.position != null ?
                                    _playerCenter.position : transform.position;
            Vector3 toPlayer = (playerCenter - rb.position).normalized;

            //速度ベクトルとプレイや間でのベクトルの内積(プレイヤーに向かう度合い)
            //(正：プレイヤーに近づく　負：プレイヤーから離れる)
            float approachSpeed = Vector3.Dot(desiredVelocity, toPlayer);

            if(approachSpeed > 0f)
            {//プレイヤーに向かう場合はゼロ
                desiredVelocity = Vector3.zero;
            }
        }

        rb.linearVelocity = desiredVelocity;
        rb.MoveRotation(_targetRotation);
        rb.angularVelocity = Vector3.zero;
    }

    /// <summary>
    /// プレイヤーがオブジェクトに乗っている、または真上の空間にいるかを判定
    /// </summary>
    /// <returns></returns>
    private bool IsPlayerAboveOrRiding()
    {
        if (_heldTarget == null || _heldTarget.Rb == null) return false;
        if (_heldTarget.IsPlayerRiding) return true;
        // プレイヤーの足元座標と中心座標
        Vector3 feetPos = _playerFeet != null ? _playerFeet.position : transform.position;
        Vector3 objPos = _heldTarget.Rb.position;
        // 足元がオブジェクトの中心より下にあるなら「上にはいない」
        if (feetPos.y < objPos.y) return false;
        // オブジェクトのコライダー範囲（＋プレイヤーの半径マージン）の真上にいるかチェック
        Collider col = _heldTarget.GetComponentInChildren<Collider>();
        if (col != null)
        {
            Bounds b = col.bounds;
            float margin = 1.2f; // プレイヤーの横幅分の余裕
            bool inX = feetPos.x >= b.min.x - margin && feetPos.x <= b.max.x + margin;
            bool inZ = feetPos.z >= b.min.z - margin && feetPos.z <= b.max.z + margin;
            // オブジェクトの上面から上方向 4m 以内に足がある（ジャンプ中含む）なら真上とみなす
            bool inY = feetPos.y <= b.max.y + 4.0f;
            return inX && inZ && inY;
        }
        return false;
    }

    private bool IsNearOrTouchingPlayer()
    {
        if(_heldTarget == null || _heldTarget.Rb == null) return false;
        if(_heldTarget.IsTouchingPlayer) return true;

        if(_playerCollider != null && _heldTargetCollider != null)
        {
            Vector3 playerCenter = _playerCenter != null ? _playerCenter.position : transform.position;
            // ターゲットの表面からプレイヤー表面までで最も近い点
            Vector3 closestOnTarget = _heldTargetCollider.ClosestPoint(playerCenter);
            // プレイヤーの表面からターゲット表面までで最も近い点
            Vector3 closestOnPlayer = _playerCollider.ClosestPoint(closestOnTarget);
            float surfaceDistance = Vector3.Distance(closestOnTarget, closestOnPlayer);
            return surfaceDistance < _minDistance;
        }
        return false;
    }

    /// <summary>
    /// 目標座標の計算（掴んだ時の相対位置 + 距離調整）
    /// </summary>
    private Vector3 CalculateTargetPosition()
    {
        Vector3 origin = GetOriginPosition();

        // 基準点 + (横オフセット) + (縦オフセット) + (前方距離)
        Vector3 targetPos = origin 
             + (_cameraTransform.right * _localOffsetX) 
             + (_cameraTransform.up * _localOffsetY) 
             + (_cameraTransform.forward * _currentDistance);

        if(IsPlayerAboveOrRiding())
        {
            // プレイヤーの足元の座標
            float maxAllowedY = _heldTarget.Rb.position.y;
            if(targetPos.y > maxAllowedY)
            {
                targetPos.y = maxAllowedY;
            }
        }

        return targetPos;
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

        float distanceInput = _actions.Player.AdjustDistance.ReadValue<float>();
        if (Mathf.Abs(distanceInput) > 0.01f) //微細な入力検知
        {

            // 入力デバイス検知
            var activeControl = _actions.Player.AdjustDistance.activeControl;
            bool isMouse = activeControl != null && activeControl.device is Mouse;

            if((IsPlayerAboveOrRiding() || IsNearOrTouchingPlayer()) 
                && distanceInput < -0.01f)
            {//プレイヤーに接触した状態で、距離を縮める入力があった場合は、距離調整を無効化する
                return;
            }

            if(IsNearOrTouchingPlayer() && distanceInput > 0.01f)
            {//プレイヤーに接触した状態で、距離を伸ばす入力があって
            //目標距離が実際のオブジェクトの位置より手前にめり込んでいる場合同期
                float actualDistance = Vector3.Dot(_heldTarget.Rb.position
                - GetOriginPosition(), _cameraTransform.forward);
                if(actualDistance > _currentDistance)
                {
                    _currentDistance = actualDistance;
                }
            }

            //マウスホイール用の大きな入力検知
            if(isMouse)
            {
                float scrollDir = Mathf.Sign(distanceInput);
                _currentDistance += scrollDir * _wheelStep;
            }
            //コントローラー用の小さな入力検知
            else
            {
                _currentDistance += distanceInput * _controllerSpeed * Time.deltaTime;
            }
        }

        //距離制限
        _currentDistance = Mathf.Clamp(_currentDistance, _activeMinDistance, _activeMaxDistance);

        //一旦の上限より近づいたら、本来の上限値まで戻す
        if(_activeMaxDistance > _maxDistance)
        {
            _activeMaxDistance = Mathf.Max(_maxDistance, _currentDistance);
        }

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
    /// LineRenderer の初期セットアップ
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
    /// Gameビュー用のレイ描画ロジック
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
