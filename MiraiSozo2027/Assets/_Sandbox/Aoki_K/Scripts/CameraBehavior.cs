using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using MiraiSozo.Options;
using Aura.Core;
using Aura.Entities;
using Aura.Kinesis;

namespace Aura.Cameras
{
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

        [Header("Gamepad Rotation Settings (スティックで回す速さ)")]
        [Tooltip("スティックをいっぱいに倒した時に、横へ1秒で回る角度（度/秒）。カメラ感度・横が最小の時の値")]
        [SerializeField, Min(0f)] private float _gamepadMinSpeedX = 60.0f;
        [Tooltip("スティックをいっぱいに倒した時に、横へ1秒で回る角度（度/秒）。カメラ感度・横が最大の時の値")]
        [SerializeField, Min(0f)] private float _gamepadMaxSpeedX = 360.0f;
        [Tooltip("スティックをいっぱいに倒した時に、縦へ1秒で回る角度（度/秒）。カメラ感度・縦が最小の時の値")]
        [SerializeField, Min(0f)] private float _gamepadMinSpeedY = 45.0f;
        [Tooltip("スティックをいっぱいに倒した時に、縦へ1秒で回る角度（度/秒）。カメラ感度・縦が最大の時の値")]
        [SerializeField, Min(0f)] private float _gamepadMaxSpeedY = 270.0f;
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
        [SerializeField] private bool _isIgnoreHeldObject = true;

        [Header("Player Transparency Settings (近距離時のプレイヤー透過)")]
        [Tooltip("プレイヤーが透け始めるカメラ距離")]
        [SerializeField] private float _fadeStartDistance = 3.5f;
        [Tooltip("プレイヤーが完全に非表示になるカメラ距離")]
        [SerializeField] private float _fadeEndDistance = 1.2f;

        [Header("ADS Settings")]
        [Tooltip("ADS時のカメラ距離（FadeStartDistanceより小さくするとADS時にプレイヤーが半透明になる)")]
        [SerializeField] private float _adsDistance = 3.0f;
        [Tooltip("ADS時のカメラの横方向オフセット")]
        [SerializeField] private float _adsSideOffset = 0.8f;
        [Tooltip("ADS時の注視点の高さ")]
        [SerializeField] private float _adsTargetHeight = 4.2f;
        [Tooltip("ADS時の視野角(FOV)")]
        [SerializeField] private float _adsFov = 40.0f;
        [Tooltip("通常⇔ADSの遷移時間（秒）")]
        [SerializeField] private float _adsTransitionDuration = 0.3f;
        [Tooltip("ADS位置を使う時に、ADS中に下へ向けられる最大角度（度）。ADS位置の向きが基準")]
        [SerializeField, Min(0f)] private float _adsMaxDownAngle = 30.0f;
        [Tooltip("ADS位置を使う時に、ADS中に上へ向けられる最大角度（度）。ADS位置の向きが基準")]
        [SerializeField, Min(0f)] private float _adsMaxUpAngle = 30.0f;
        [Tooltip("ADSを離した時に、上下に向けた分がADS位置の向きへ戻るまでの時間（秒）")]
        [SerializeField, Min(0f)] private float _adsPitchReturnDuration = 0.1f;
        // プレイヤーの子のADS位置。設定されていれば、ADS中はこの位置・向きへカメラを寄せる
        private Transform _adsAnchor;
        // ADS中に上下へ傾けるプレイヤーの子（見た目・銃口・エコーの発生位置・ADS位置をまとめた親）。当たり判定は含めない
        private Transform _adsPivot;
        private float _adsPitchOffset; // ADS位置の向きから上下に向けた角度（正で下向き）
        private float _adsPitchReturnSpeed; // ADSを離した時の角度から、指定時間で0へ戻す速さ（度/秒）
        private Camera _camera;
        private float _defaultFov = 60.0f;
        private bool _isAds = false;
        private float _adsWeight; // 通常：0.0~1.0：ADS
        private bool _isWallRestricted;//壁によって距離制限されているかどうか


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
        /// <summary>カメラの左右の向き（度）。ADS中にプレイヤーをカメラへ向けるために使う</summary>
        public float Yaw => _yaw;
        /// <summary>ADS中か</summary>
        public bool IsAds => _isAds;
        private const float MinOffsetDistance = 0.0001f; // フェード開始距離と終了距離の最小ギャップ
        private const float MinFadeDistanceGap = 0.01f; // フェード開始距離と終了距離の最小ギャップ
        private const float InvisibleAlphaThreshold = 0.001f; // 完全透明とみなすアルファ値の閾値
        private const float OpaqueAlphaThreshold = 0.999f; // 不透明とみなすアルファ値の閾値

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

        public void SetAds(bool isAds)
        {
            // 離した瞬間の角度から数えて、指定時間でちょうど戻りきる速さにする
            if (_isAds && !isAds)
            {
                _adsPitchReturnSpeed = _adsPitchReturnDuration > 0.0f
                    ? Mathf.Abs(_adsPitchOffset) / _adsPitchReturnDuration
                    : float.MaxValue;
            }
            _isAds = isAds;
        }

        /// <summary>
        /// ADS中にカメラを寄せる位置と向き（anchor）と、上下に傾けるプレイヤーの子（pivot）を設定する。
        /// anchorがnullなら距離・横オフセット・注視点の高さで寄せ、pivotがnullならカメラだけを上下に向ける
        /// </summary>
        public void SetAdsTransforms(Transform anchor, Transform pivot)
        {
            if (_adsPivot != null && _adsPivot != pivot)
            {
                // 差し替え前に傾けた分を残さない
                _adsPivot.localRotation = Quaternion.identity;
            }
            _adsAnchor = anchor;
            _adsPivot = pivot;
        }

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            if (_camera != null)
            {
                _defaultFov = _camera.fieldOfView;
            }
            _currentOffsetDistance = Mathf.Sqrt(_distance * _distance + _sideOffset * _sideOffset);
        }

        private void OnEnable()
        {
            GameManager.OnPlayerSpawned += HandlePlayerSpawned;
        }

        private void OnDisable()
        {
            GameManager.OnPlayerSpawned -= HandlePlayerSpawned;
        }

        private void OnDestroy()
        {
            CleanupMaterialInstances();
        }

        private void HandlePlayerSpawned(Player player)
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
            else if (GameManager.Instance != null && GameManager.Instance.CurrentPlayer != null)
            {
                InitializeTarget(GameManager.Instance.CurrentPlayer.transform);
            }
            else
            {
                Player existingPlayer = FindFirstObjectByType<Player>();
                if (existingPlayer != null)
                {
                    InitializeTarget(existingPlayer.transform);
                }
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
            Vector2 lookDelta = GetLookDelta(_actions.Player.Look);
            // 2. 角度の加算（反転フラグを反映）
            _yaw += lookDelta.x * (_invertX ? -1f : 1f);
            float verticalDelta = lookDelta.y * (_invertY ? 1f : -1f);
            bool hasAdsAnchor = _adsAnchor != null;
            // 一人称ではADS位置へ寄せないため、ADS中も通常どおり上下の向きで見る
            bool isFirstPerson = GameSettings.Instance != null && GameSettings.Instance.IsFirstPersonMode;
            if (_isAds && hasAdsAnchor && !isFirstPerson)
            {
                // ADS中の上下はADS位置の向きを基準に、指定した角度の範囲だけ動かす
                _adsPitchOffset = Mathf.Clamp(_adsPitchOffset + verticalDelta, -_adsMaxUpAngle, _adsMaxDownAngle);
            }
            else
            {
                _pitch += verticalDelta;
                _adsPitchOffset = Mathf.MoveTowards(_adsPitchOffset, 0.0f, _adsPitchReturnSpeed * Time.deltaTime);
            }
            // 3. 上下角度を制限（地面への潜り込みや真上反転を防止）
            _pitch = Mathf.Clamp(_pitch, _minPitch, _maxPitch);
            // 4. 回転クォータニオンの生成
            Quaternion cameraRotation = Quaternion.Euler(_pitch, _yaw, 0f);
            // 5. ADS遷移率（0.0f ～ 1.0f）の更新と各パラメータの線形補間
            float targetAdsWeight = _isAds ? 1.0f : 0.0f;
            float adsStep = _adsTransitionDuration > 0.0f ? (Time.deltaTime / _adsTransitionDuration) : 1.0f;
            _adsWeight = Mathf.MoveTowards(_adsWeight, targetAdsWeight, adsStep);
            // ADS位置があればカメラ全体をそこへ寄せるため、距離などの寄せ方は使わない
            float offsetAdsWeight = hasAdsAnchor ? 0.0f : _adsWeight;
            float currentTargetHeight = Mathf.Lerp(_targetHeight, _adsTargetHeight, offsetAdsWeight);
            float currentDistance = Mathf.Lerp(_distance, _adsDistance, offsetAdsWeight);
            float currentSideOffset = Mathf.Lerp(_sideOffset, _adsSideOffset, offsetAdsWeight);
            if (_camera != null)
            {
                float baseFov = GameOptions.Instance != null ? GameOptions.Instance.Values.fieldOfView : _defaultFov;
                _camera.fieldOfView = Mathf.Lerp(baseFov, _adsFov, _adsWeight);
            }
            // 6. 注視点（通常時は胸付近、ADS時は頭付近へ補間）の座標
            Vector3 targetFocusPosition = _target.position + Vector3.up * currentTargetHeight;
            // 7. カメラの位置を算出（一人称モード または 三人称壁判定付きオフセット）
            if (GameSettings.Instance != null && GameSettings.Instance.IsFirstPersonMode)
            {
                transform.position = targetFocusPosition;
                UpdatePlayerTransparency(0f);
            }
            else
            {
                Vector3 idealOffset = (cameraRotation * Vector3.right * currentSideOffset)
                                    - (cameraRotation * Vector3.forward * currentDistance);
                float idealDistance = idealOffset.magnitude;
                if (idealDistance > MinOffsetDistance)
                {
                    Vector3 castDirection = idealOffset / idealDistance;
                    float collisionDistance = CalculateCollisionDistance(targetFocusPosition, castDirection, idealDistance);
                    if (collisionDistance < idealDistance)
                    {
                        // 壁に当たっている場合
                        _isWallRestricted = true;
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
                    }
                    else if (_isWallRestricted)
                    {
                        // 壁から離れた直後の復帰中
                        _currentOffsetDistance = Mathf.MoveTowards(
                            _currentOffsetDistance,
                            idealDistance,
                            _distanceReturnSpeed * Time.deltaTime
                        );
                        if (_currentOffsetDistance >= idealDistance)
                        {
                            _currentOffsetDistance = idealDistance;
                            _isWallRestricted = false;
                        }
                    }
                    else
                    {
                        // 壁干渉なし（通常時およびADS遷移時は理想距離をそのまま反映）
                        _currentOffsetDistance = idealDistance;
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
            // 8. カメラの向きを適用
            transform.rotation = cameraRotation;

            // 9. ADS位置へ寄せる（一人称モードでは使わない）
            if (hasAdsAnchor && !isFirstPerson && _adsWeight > 0.0f)
            {
                ApplyAdsAnchor(targetFocusPosition);
            }

            // 10. プレイヤーの見た目（pivot）の向き
            if (_adsPivot != null)
            {
                UpdateAdsPivot(_isAds && hasAdsAnchor && !isFirstPerson);
            }
        }

        /// <summary>
        /// ADS中はpivotをカメラの左右・上下の向きに合わせる。ADS中でなければ、上下に向けた分だけを残して正面へ戻す。
        /// </summary>
        private void UpdateAdsPivot(bool isAiming)
        {
            // プレイヤー本体は物理の更新（FixedUpdate）でしか回らず、毎フレーム回るカメラより遅れてカクついて見える。
            // ADS中は見た目（pivot）の左右を毎フレームカメラの向きに合わせ、本体の遅れを打ち消す。
            Quaternion yawCorrection = isAiming
                ? Quaternion.Inverse(_target.rotation) * Quaternion.Euler(0.0f, _yaw, 0.0f)
                : Quaternion.identity;
            _adsPivot.localRotation = yawCorrection * Quaternion.Euler(_adsPitchOffset, 0.0f, 0.0f);
        }

        // 掴んでいる物を無視する設定の時だけ、掴んでいる物を返す
        private Transform GetIgnoredHeldTarget()
        {
            return (_isIgnoreHeldObject && _playerPhononkinesis != null && _playerPhononkinesis.HeldTarget != null)
                ? _playerPhononkinesis.HeldTarget.transform
                : null;
        }

        // プレイヤー自身・掴んでいる物・波の制御点（見えないコライダー）は、カメラの判定から除く
        private bool IsIgnoredHit(RaycastHit hit, Transform heldTargetTransform)
        {
            if (hit.collider == null) return true;
            Transform hitTransform = hit.transform;
            if (hitTransform == _target || hitTransform.IsChildOf(_target)) return true;
            if (heldTargetTransform != null && (hitTransform == heldTargetTransform || hitTransform.IsChildOf(heldTargetTransform))) return true;
            return hit.collider.name.StartsWith("BeamPoint");
        }

        /// <summary>
        /// このフレームで回す角度（度）を返す。反転は含めない
        /// </summary>
        private Vector2 GetLookDelta(InputAction lookAction)
        {
            Vector2 lookInput = lookAction.ReadValue<Vector2>();
            InputControl control = lookAction.activeControl;
            // マウス（Pointer）の値は1フレームで動いた量なので、そのまま感度を掛ける
            if (control == null || control.device is Pointer)
            {
                return new Vector2(lookInput.x * _sensitivityX, lookInput.y * _sensitivityY);
            }

            // スティックの値は倒している具合（-1～1）なので、1秒あたりの角度に時間を掛ける。
            // 毎フレームそのまま足すと、フレームレートで速さが変わり、マウスより大幅に遅くなるため。
            // 一時停止やスローの影響を受けないよう、実時間で進める
            float speedX = GetGamepadSpeed(_sensitivityX, _gamepadMinSpeedX, _gamepadMaxSpeedX);
            float speedY = GetGamepadSpeed(_sensitivityY, _gamepadMinSpeedY, _gamepadMaxSpeedY);
            return new Vector2(lookInput.x * speedX, lookInput.y * speedY) * Time.unscaledDeltaTime;
        }

        /// <summary>
        /// オプションのカメラ感度（最小～最大）を、スティックで回す速さ（度/秒）の最小～最大に置き換える
        /// </summary>
        private static float GetGamepadSpeed(float sensitivity, float minSpeed, float maxSpeed)
        {
            float ratio = Mathf.InverseLerp(OptionValues.MinimumSensitivity, OptionValues.MaximumSensitivity, sensitivity);
            return Mathf.Lerp(minSpeed, maxSpeed, ratio);
        }

        /// <summary>
        /// ADS位置の位置・向きへ、遷移率に合わせてカメラを寄せる
        /// </summary>
        private void ApplyAdsAnchor(Vector3 targetFocusPosition)
        {
            Vector3 adsPosition;
            Quaternion adsRotation;
            if (_adsPivot != null)
            {
                // pivotは照準の先へ向けるため、その向きでカメラを動かすとレイの向きが変わり続けて揺れる。
                // pivotから見たADS位置を、マウスで決めた左右・上下の向きで置き直し、カメラはpivotの向きに引っ張られないようにする
                Quaternion aimRotation = Quaternion.Euler(0.0f, _yaw, 0.0f) * Quaternion.Euler(_adsPitchOffset, 0.0f, 0.0f);
                Quaternion pivotInverse = Quaternion.Inverse(_adsPivot.rotation);
                adsPosition = _adsPivot.position + aimRotation * (pivotInverse * (_adsAnchor.position - _adsPivot.position));
                adsRotation = aimRotation * (pivotInverse * _adsAnchor.rotation);
            }
            else
            {
                // プレイヤーの回転は物理の更新で少し遅れるため、プレイヤーから見たADS位置をカメラの左右の向きで置き直す
                Quaternion yawRotation = Quaternion.Euler(0.0f, _yaw, 0.0f);
                Quaternion targetInverse = Quaternion.Inverse(_target.rotation);
                adsPosition = _target.position + yawRotation * (targetInverse * (_adsAnchor.position - _target.position));
                adsRotation = yawRotation * (targetInverse * _adsAnchor.rotation) * Quaternion.Euler(_adsPitchOffset, 0.0f, 0.0f);
            }

            // 寄せた先が壁の向こうにならないよう、注視点からの間に壁があれば手前で止める
            Vector3 cameraPosition = Vector3.Lerp(transform.position, adsPosition, _adsWeight);
            Vector3 offset = cameraPosition - targetFocusPosition;
            float offsetDistance = offset.magnitude;
            if (offsetDistance > MinOffsetDistance)
            {
                Vector3 castDirection = offset / offsetDistance;
                cameraPosition = targetFocusPosition + castDirection * CalculateCollisionDistance(targetFocusPosition, castDirection, offsetDistance);
            }
            transform.position = cameraPosition;
            transform.rotation = Quaternion.Slerp(transform.rotation, adsRotation, _adsWeight);
            UpdatePlayerTransparency(Vector3.Distance(transform.position, targetFocusPosition));
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

            Transform heldTargetTransform = GetIgnoredHeldTarget();

            for (int i = 0; i < hitCount; i++)
            {
                RaycastHit hit = _hitBuffer[i];
                if (IsIgnoredHit(hit, heldTargetTransform)) continue;

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

            float startDist = Mathf.Max(_fadeStartDistance, _fadeEndDistance + MinFadeDistanceGap); // フェード開始距離と終了距離の最小ギャップを確保
            float targetAlpha = Mathf.Clamp01(Mathf.InverseLerp(_fadeEndDistance, startDist, cameraDistance));

            if (Mathf.Approximately(_currentPlayerAlpha, targetAlpha)) return;
            _currentPlayerAlpha = targetAlpha;

            // 完全透明距離以下の場合はRenderer自体を非表示にしてカメラNear平面へのめり込みを完全に防ぐ
            if (targetAlpha <= InvisibleAlphaThreshold)
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
            if (targetAlpha >= OpaqueAlphaThreshold)
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
}
