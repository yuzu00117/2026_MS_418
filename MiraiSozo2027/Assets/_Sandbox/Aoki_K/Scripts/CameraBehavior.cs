using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class CameraBehavior : MonoBehaviour
{
    private struct PlayerRendererData
    {
        public Renderer Renderer;
        public Material[] OriginalSharedMaterials;
    }

    private struct PlayerMaterialData
    {
        public Material Material;
        public Color OriginalColor;
        public bool HasBaseColor;
        public bool HasColor;
        public int OriginalRenderQueue;
    }

    private InputSystem_Actions _actions => GameManager.Actions;
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
    [SerializeField] private bool _invertX = false; //横方向反転（チェックを入れると左右反転）
    [SerializeField] private bool _invertY = false; //縦方向反転（チェックを入れると上下反転）

    [Header("Collision Settings (壁めり込み防止)")]
    [Tooltip("カメラの当たり判定の対象レイヤー（デフォルトは Ignore Raycast 以外）")]
    [SerializeField] private LayerMask _collisionLayers = ~(1 << 2);
    [Tooltip("カメラの球状判定の半径（Near Clip Planeと同程度を推奨）")]
    [SerializeField] private float _collisionRadius = 0.3f;
    [Tooltip("壁から離すマージン距離")]
    [SerializeField] private float _collisionMargin = 0.1f;
    [Tooltip("カメラが注視点に近づける最小距離")]
    [SerializeField] private float _minCollisionDistance = 0.3f;
    [Tooltip("壁から離れた際に元の距離へ戻る補間速度")]
    [SerializeField] private float _distanceReturnSpeed = 12.0f;
    [Tooltip("フォノンキネシスで掴んでいるオブジェクトとのカメラ衝突を無視するか")]
    [SerializeField] private bool _ignoreHeldObject = true;

    [Header("Player Transparency Settings (近距離時のプレイヤー透過)")]
    [Tooltip("プレイヤーが透け始めるカメラ距離")]
    [SerializeField] private float _fadeStartDistance = 3.5f;
    [Tooltip("プレイヤーが完全に非表示になるカメラ距離")]
    [SerializeField] private float _fadeEndDistance = 1.2f;

    // 内部キャッシュ・状態管理用変数
    private readonly RaycastHit[] _hitBuffer = new RaycastHit[16];
    private readonly List<PlayerRendererData> _playerRenderers = new List<PlayerRendererData>();
    private readonly List<PlayerMaterialData> _playerMaterials = new List<PlayerMaterialData>();

    private Transform _cachedTarget;
    private Phononkinesis _playerPhononkinesis;
    private float _currentOffsetDistance;
    private float _currentPlayerAlpha = -1f;
    private bool _isMaterialTransparent;

    // シェーダープロパティIDキャッシュ
    private static readonly int PropBaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int PropColor = Shader.PropertyToID("_Color");
    private static readonly int PropSurface = Shader.PropertyToID("_Surface");
    private static readonly int PropBlend = Shader.PropertyToID("_Blend");
    private static readonly int PropSrcBlend = Shader.PropertyToID("_SrcBlend");
    private static readonly int PropDstBlend = Shader.PropertyToID("_DstBlend");
    private static readonly int PropSrcBlendAlpha = Shader.PropertyToID("_SrcBlendAlpha");
    private static readonly int PropDstBlendAlpha = Shader.PropertyToID("_DstBlendAlpha");
    private static readonly int PropZWrite = Shader.PropertyToID("_ZWrite");
    private static readonly int PropBlendModePreserveSpecular = Shader.PropertyToID("_BlendModePreserveSpecular");

    public float SensitivityX => _sensitivityX;
    public float SensitivityY => _sensitivityY;
    public bool InvertX => _invertX;
    public bool InvertY => _invertY;

    /// <summary>
    /// オプション設定からカメラの感度・反転を反映する
    /// </summary>
    public void ApplyOptions(float sensitivityX, float sensitivityY, bool invertX, bool invertY)
    {
        _sensitivityX = sensitivityX;
        _sensitivityY = sensitivityY;
        _invertX = invertX;
        _invertY = invertY;
    }

    private void Awake()
    {
        _currentOffsetDistance = Mathf.Sqrt(_distance * _distance + _sideOffset * _sideOffset);
    }

    private void OnEnable()
    {
        PlayerController.OnPlayerSpawned += HandlePlayerSpawned;
    }

    private void OnDisable()
    {
        PlayerController.OnPlayerSpawned -= HandlePlayerSpawned;
    }

    private void OnDestroy()
    {
        CleanupMaterialInstances();
    }

    private void HandlePlayerSpawned(PlayerController player)
    {
        if (player != null)
        {
            InitializeTarget(player.transform);
        }
    }

    private void Start()
    {
        //マウスカーソル非表示
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        //現在のカメラの向きを初期角度として取得
        Vector3 angles = transform.eulerAngles;
        _yaw = angles.y;
        _pitch = angles.x;

        if (_target != null)
        {
            InitializeTarget(_target);
        }
        else if (PlayerController.Instance != null)
        {
            InitializeTarget(PlayerController.Instance.transform);
        }
    }

    private void LateUpdate()
    {
        if (_target == null || _actions == null) return;

        if (_cachedTarget != _target)
        {
            InitializeTarget(_target);
        }

        // ポーズ中はカメラ回転を停止
        if (GameManager.Instance != null && GameManager.Instance.IsPaused) return;

        // 1. Input System から Look（マウス移動 / 右スティック）を取得
        Vector2 lookInput = _actions.Player.Look.ReadValue<Vector2>();
        // 2. 角度の加算（反転フラグを反映）
        _yaw += lookInput.x * _sensitivityX * (_invertX ? -1f : 1f);
        float verticalDelta = lookInput.y * _sensitivityY * (_invertY ? 1f : -1f);
        _pitch += verticalDelta;
        // 3. 上下角度を制限（地面への潜り込みや真上反転を防止）
        _pitch = Mathf.Clamp(_pitch, _minPitch, _maxPitch);
        // 4. 回転クォータニオンの生成
        Quaternion cameraRotation = Quaternion.Euler(_pitch, _yaw, 0f);
        // 5. 注視点（キャラクターの胸〜頭付近）の座標
        Vector3 targetFocusPosition = _target.position + Vector3.up * _targetHeight;

        // 6. カメラの位置を算出（一人称モード または 三人称壁判定付きオフセット）
        if (GameSettings.Instance != null && GameSettings.Instance.IsFirstPersonMode)
        {
            transform.position = targetFocusPosition;
            UpdatePlayerTransparency(0f);
        }
        else
        {
            Vector3 idealOffset = (cameraRotation * Vector3.right * _sideOffset)
                                - (cameraRotation * Vector3.forward * _distance);
            float idealDistance = idealOffset.magnitude;

            if (idealDistance > 0.0001f)
            {
                Vector3 castDirection = idealOffset / idealDistance;
                float collisionDistance = CalculateCollisionDistance(targetFocusPosition, castDirection, idealDistance);

                // 壁に近づく際はめり込みを防ぐため即座に距離を縮め、離れる際は滑らかに戻す
                if (collisionDistance < _currentOffsetDistance)
                {
                    _currentOffsetDistance = collisionDistance;
                }
                else
                {
                    _currentOffsetDistance = Mathf.MoveTowards(
                        _currentOffsetDistance,
                        collisionDistance,
                        _distanceReturnSpeed * Time.deltaTime
                    );
                }

                transform.position = targetFocusPosition + castDirection * _currentOffsetDistance;
                UpdatePlayerTransparency(_currentOffsetDistance);
            }
            else
            {
                transform.position = targetFocusPosition;
                UpdatePlayerTransparency(0f);
            }
        }

        // 7. カメラの向きを適用
        transform.rotation = cameraRotation;
    }

    /// <summary>
    /// 注視点から理想のカメラ位置へSphereCastを行い、壁の手前までの距離を算出する
    /// </summary>
    private float CalculateCollisionDistance(Vector3 origin, Vector3 direction, float maxDistance)
    {
        float closestDistance = maxDistance;

        int hitCount = Physics.SphereCastNonAlloc(
            origin,
            _collisionRadius,
            direction,
            _hitBuffer,
            maxDistance,
            _collisionLayers,
            QueryTriggerInteraction.Ignore
        );

        Transform heldTargetTransform = (_ignoreHeldObject && _playerPhononkinesis != null && _playerPhononkinesis.HeldTarget != null)
            ? _playerPhononkinesis.HeldTarget.transform
            : null;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit hit = _hitBuffer[i];
            if (hit.collider == null) continue;

            Transform hitTransform = hit.transform;

            // プレイヤー自身およびその子オブジェクトのコライダーは除外
            if (hitTransform == _target || hitTransform.IsChildOf(_target)) continue;

            // フォノンキネシスで現在掴んでいるオブジェクトは除外
            if (heldTargetTransform != null && (hitTransform == heldTargetTransform || hitTransform.IsChildOf(heldTargetTransform))) continue;

            // 波の制御点（見えないコライダー）は除外
            if (hit.collider.name.StartsWith("BeamPoint")) continue;

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
            }
        }

        if (closestDistance < maxDistance)
        {
            return Mathf.Clamp(closestDistance - _collisionMargin, _minCollisionDistance, maxDistance);
        }

        return maxDistance;
    }

    /// <summary>
    /// 追従ターゲットを設定し、透過制御用のRenderer・Materialをキャッシュする
    /// </summary>
    private void InitializeTarget(Transform newTarget)
    {
        // すでに同じターゲットで初期化済みの場合は二重初期化（マテリアルの破棄）を防ぐ
        if (_cachedTarget == newTarget && _playerRenderers.Count > 0)
        {
            _target = newTarget;
            return;
        }

        CleanupMaterialInstances();

        _target = newTarget;
        _cachedTarget = newTarget;
        _currentOffsetDistance = Mathf.Sqrt(_distance * _distance + _sideOffset * _sideOffset);
        _currentPlayerAlpha = -1f;
        _isMaterialTransparent = false;

        if (_target == null) return;

        _playerPhononkinesis = _target.GetComponent<Phononkinesis>();

        Renderer[] renderers = _target.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer rend in renderers)
        {
            // LineRenderer（フォノンキネシスの光線）やパーティクル等は除外し、メッシュのみ対象とする
            if (!(rend is SkinnedMeshRenderer) && !(rend is MeshRenderer)) continue;

            // インスタンス化前の元の共有マテリアル配列を保持しておく
            Material[] originalShared = rend.sharedMaterials;
            _playerRenderers.Add(new PlayerRendererData
            {
                Renderer = rend,
                OriginalSharedMaterials = originalShared
            });

            Material[] materials = rend.materials;
            foreach (Material mat in materials)
            {
                if (mat == null) continue;

                bool hasBaseColor = mat.HasProperty(PropBaseColor);
                bool hasColor = mat.HasProperty(PropColor);
                Color originalColor = Color.white;

                if (hasBaseColor)
                {
                    originalColor = mat.GetColor(PropBaseColor);
                }
                else if (hasColor)
                {
                    originalColor = mat.GetColor(PropColor);
                }

                _playerMaterials.Add(new PlayerMaterialData
                {
                    Material = mat,
                    OriginalColor = originalColor,
                    HasBaseColor = hasBaseColor,
                    HasColor = hasColor,
                    OriginalRenderQueue = mat.renderQueue
                });
            }
        }
    }

    /// <summary>
    /// カメラと注視点の距離に応じてプレイヤーモデルの透過率および表示状態を切り替える
    /// </summary>
    private void UpdatePlayerTransparency(float cameraDistance)
    {
        if (_playerRenderers.Count == 0) return;

        float startDist = Mathf.Max(_fadeStartDistance, _fadeEndDistance + 0.01f);
        float targetAlpha = Mathf.Clamp01(Mathf.InverseLerp(_fadeEndDistance, startDist, cameraDistance));

        if (Mathf.Approximately(_currentPlayerAlpha, targetAlpha)) return;
        _currentPlayerAlpha = targetAlpha;

        // 完全透明距離以下の場合はRenderer自体を非表示にしてカメラNear平面へのめり込みを完全に防ぐ
        if (targetAlpha <= 0.001f)
        {
            for (int i = 0; i < _playerRenderers.Count; i++)
            {
                Renderer rend = _playerRenderers[i].Renderer;
                if (rend != null)
                {
                    rend.enabled = false;
                }
            }
            return;
        }

        // 表示を有効化
        for (int i = 0; i < _playerRenderers.Count; i++)
        {
            Renderer rend = _playerRenderers[i].Renderer;
            if (rend != null && !rend.enabled)
            {
                rend.enabled = true;
            }
        }

        // 通常距離（不透明）に戻った場合
        if (targetAlpha >= 0.999f)
        {
            if (_isMaterialTransparent)
            {
                for (int i = 0; i < _playerMaterials.Count; i++)
                {
                    PlayerMaterialData data = _playerMaterials[i];
                    if (data.Material == null) continue;

                    SetMaterialOpaque(data);
                    ApplyMaterialAlpha(data, 1.0f);
                }
                _isMaterialTransparent = false;
            }
            return;
        }

        // 半透明フェード領域に入った場合
        if (!_isMaterialTransparent)
        {
            for (int i = 0; i < _playerMaterials.Count; i++)
            {
                PlayerMaterialData data = _playerMaterials[i];
                if (data.Material == null) continue;

                SetMaterialTransparent(data);
            }
            _isMaterialTransparent = true;
        }

        for (int i = 0; i < _playerMaterials.Count; i++)
        {
            PlayerMaterialData data = _playerMaterials[i];
            if (data.Material == null) continue;

            ApplyMaterialAlpha(data, targetAlpha);
        }
    }

    private static void ApplyMaterialAlpha(PlayerMaterialData data, float alphaMultiplier)
    {
        Color color = data.OriginalColor;
        color.a = data.OriginalColor.a * alphaMultiplier;

        if (data.HasBaseColor)
        {
            data.Material.SetColor(PropBaseColor, color);
        }
        if (data.HasColor)
        {
            data.Material.SetColor(PropColor, color);
        }
    }

    private static void SetMaterialTransparent(PlayerMaterialData data)
    {
        Material mat = data.Material;
        if (mat.HasProperty(PropSurface))
        {
            mat.SetFloat(PropSurface, 1.0f);
        }
        if (mat.HasProperty(PropBlend))
        {
            mat.SetFloat(PropBlend, 0.0f);
        }
        if (mat.HasProperty(PropBlendModePreserveSpecular))
        {
            mat.SetFloat(PropBlendModePreserveSpecular, 0.0f);
        }
        if (mat.HasProperty(PropSrcBlend))
        {
            mat.SetInt(PropSrcBlend, (int)BlendMode.SrcAlpha);
        }
        if (mat.HasProperty(PropDstBlend))
        {
            mat.SetInt(PropDstBlend, (int)BlendMode.OneMinusSrcAlpha);
        }
        if (mat.HasProperty(PropSrcBlendAlpha))
        {
            mat.SetInt(PropSrcBlendAlpha, (int)BlendMode.One);
        }
        if (mat.HasProperty(PropDstBlendAlpha))
        {
            mat.SetInt(PropDstBlendAlpha, (int)BlendMode.OneMinusSrcAlpha);
        }
        if (mat.HasProperty(PropZWrite))
        {
            mat.SetInt(PropZWrite, 0);
        }

        mat.SetOverrideTag("RenderType", "Transparent");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.SetShaderPassEnabled("DepthOnly", false);
        mat.renderQueue = (int)RenderQueue.Transparent;
    }

    private static void SetMaterialOpaque(PlayerMaterialData data)
    {
        Material mat = data.Material;
        if (mat.HasProperty(PropSurface))
        {
            mat.SetFloat(PropSurface, 0.0f);
        }
        if (mat.HasProperty(PropSrcBlend))
        {
            mat.SetInt(PropSrcBlend, (int)BlendMode.One);
        }
        if (mat.HasProperty(PropDstBlend))
        {
            mat.SetInt(PropDstBlend, (int)BlendMode.Zero);
        }
        if (mat.HasProperty(PropSrcBlendAlpha))
        {
            mat.SetInt(PropSrcBlendAlpha, (int)BlendMode.One);
        }
        if (mat.HasProperty(PropDstBlendAlpha))
        {
            mat.SetInt(PropDstBlendAlpha, (int)BlendMode.Zero);
        }
        if (mat.HasProperty(PropZWrite))
        {
            mat.SetInt(PropZWrite, 1);
        }

        mat.SetOverrideTag("RenderType", "Opaque");
        mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.SetShaderPassEnabled("DepthOnly", true);
        mat.renderQueue = data.OriginalRenderQueue;
    }

    private void CleanupMaterialInstances()
    {
        // ランタイム用マテリアルを破棄する前に、Rendererへ元の共有マテリアルを戻してMissingを防ぐ
        for (int i = 0; i < _playerRenderers.Count; i++)
        {
            PlayerRendererData rendererData = _playerRenderers[i];
            if (rendererData.Renderer != null && rendererData.OriginalSharedMaterials != null)
            {
                rendererData.Renderer.sharedMaterials = rendererData.OriginalSharedMaterials;
                rendererData.Renderer.enabled = true;
            }
        }

        for (int i = 0; i < _playerMaterials.Count; i++)
        {
            if (_playerMaterials[i].Material != null)
            {
                Destroy(_playerMaterials[i].Material);
            }
        }
        _playerMaterials.Clear();
        _playerRenderers.Clear();
    }
}
