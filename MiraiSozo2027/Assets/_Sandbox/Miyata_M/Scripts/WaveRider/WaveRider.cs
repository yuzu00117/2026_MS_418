using System.Collections.Generic;
using Aura.Inspector;
using Aura.ObjectInfo;
using Aura.Wave;
using UnityEngine;

namespace Aura.Gimmick
{
    /// @brief WaveRider が波の線の終点に着いたときの動き
    public enum WaveRiderEndMode
    {
        [InspectorName("終点で止まる")]
        Stop = 0,

        [InspectorName("折り返す")]
        PingPong = 1,
    }

    /// @brief WaveRider が波に乗れなくなったとき（波が遮られた、止まった、スイッチが止まった）の動き
    public enum WaveRiderLostWaveMode
    {
        [InspectorName("その場で止まる")]
        StopInPlace = 0,

        [InspectorName("慣性で進んでから止まる")]
        Inertia = 1,

        [InspectorName("初期位置へ戻る")]
        ReturnToStart = 2,
    }

    /// @brief タレットが出す波（ビームの線）に運ばれるオブジェクト。上に乗った物（プレイヤーを含む）も一緒に運ぶ
    /// ・反応する波の種類は WaveReaction（電磁波/超音波のチェック）で決める。反応しない波では動かず、波が無いときと同じ扱いになる
    /// ・波が出ている間は常に線の上にいて、線の上の道のり s を一定の速さで進める。波が曲がっても跳ね返っても、s を保ったまま線に沿う
    /// ・波が無くなったら（遮られた、止まった）、「波が無くなったとき」の設定に従う（その場で止まる / 慣性で進んでから止まる / 初期位置へ戻る）
    ///   止まったあとや戻る途中で波が来たら、今の位置に一番近い線の上の点から乗り直す
    /// ・Kinematic な Rigidbody として物理ステップ（FixedUpdate）で動く。上に乗った Rigidbody には、同じ移動量を足して運ぶ
    ///   （プレイヤーは毎ステップ水平の速度を入力で上書きするため、摩擦では運ばれない）
    /// ・波の線は WaveEmitter.BeamCorners から読む。WaveEmitter が LateUpdate で更新した、直前のフレームの線を使う
    /// ・慣性で進むときは、壁などに当たったらその手前で止まる（波に乗っている間は、今までどおり当たらず通り抜ける）
    /// ・波に乗っている間は、先端が終点に着いたところで止まり、壁などに食い込んだ分は横方向に押し戻す（砲台など波を出すものは除く）
    /// ・「スイッチで起動する」をオンにすると、スイッチが起動している間だけ波に乗る（Activate / Deactivate をスイッチにつなぐ）
    /// ・当たり判定は、このオブジェクト自身に付ける。ビームが自分の面で止まって線が縮まないよう、
    ///   Substance の「波への反応」を PassThrough にする（SO_Substance_WaveRiderBody など）
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WaveReaction))]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class WaveRider : MonoBehaviour
    {
        private const float MinDirectionSqrMagnitude = 0.25f; ///< 進む向きの長さの 2 乗がこれより小さければ、向きが取れなかったとみなす
        private const float TurnDegreesPerSecond = 720.0f;    ///< 進む向きに回転を合わせるときの、1 秒あたりの最大の回転（度）
        private const float BeamEndTolerance = 0.05f;         ///< 線が縮んで、自分の道のりより短くなった差がこれ以内なら、揺れとみなして乗り続ける（m）
        private const float MinPushDistance = 0.001f;        ///< 壁から押し戻す重なりの最小（m）。これ以下は接しているだけとみなす
        private const float MinPushHorizontalSqrMagnitude = 0.25f; ///< 押し出しの向きの横成分の長さの 2 乗がこれ以下なら、ほぼ縦向きなので押し戻さない
        private const float PassengerSlabHeight = 0.3f;      ///< 上面の上で乗っている物を探す、薄い箱の高さ（m）
        private const int OverlapBufferSize = 32;            ///< 上面で同時に調べる Collider の最大数。超えた分は乗っている物として数えない
        private const float TopContactTolerance = 0.1f;      ///< 相手の足元が自分の上面からこの高さ以内に潜っていても、上に乗っているとみなす（m）

        [InspectorLabel("乗る波を出すもの")]
        [Tooltip("乗る波を出す WaveEmitter（タレットなど）。空なら、反応する波を出している中で、今の位置から一番近い線に乗る")]
        [SerializeField] private WaveEmitter _emitter;        ///< 乗る波を出すもの。空なら近い線を探す

        [InspectorLabel("運ばれる速さ（m/s）")]
        [Tooltip("波の線に沿って進む速さ（m/s）。曲がった所の角も、道のりにそのまま数える。0 なら乗っても進まない")]
        [SerializeField, Min(0.0f)] private float _carrySpeed = 2.0f;  ///< 波に運ばれる速さ（m/s）

        [InspectorLabel("波が無くなったとき")]
        [Tooltip("波が遮られた、止まった、スイッチが止まったときの動き\n" +
                 "・その場で止まる: 今の位置で止まる\n" +
                 "・慣性で進んでから止まる: 直前の速さと向きのまま進み、「慣性の減速（m/s²）」で減速して止まる\n" +
                 "・初期位置へ戻る: ゲーム開始時の位置へ「戻る速さ」で戻る")]
        [SerializeField] private WaveRiderLostWaveMode _lostWaveMode = WaveRiderLostWaveMode.StopInPlace;  ///< 波が無くなったときの動き

        [InspectorLabel("慣性の減速（m/s²）")]
        [Tooltip("「慣性で進んでから止まる」のとき、1 秒あたりに減る速さ（m/s²）。大きいほど早く止まる。0 なら止まらず進み続ける\n" +
                 "慣性で進む間に壁などに当たったら、その手前で止まる（上に乗っている物は無視する）")]
        [SerializeField, Min(0.0f)] private float _inertiaDeceleration = 3.0f;  ///< 慣性で進むときの減速（m/s²）

        [InspectorLabel("戻る速さ（m/s）")]
        [Tooltip("「初期位置へ戻る」のとき、ゲーム開始時の位置へ戻る速さ（m/s）。0 なら戻らず、その場で止まる")]
        [SerializeField, Min(0.0f)] private float _returnSpeed = 2.0f;  ///< 初期位置へ戻る速さ（m/s）

        [InspectorLabel("乗り直せる距離（m）")]
        [Tooltip("一度波に乗ったあと、乗り直すとき、自分から波の線までの距離がこれ以内のときだけ乗る（m）。乗るときは線の上へ移る\n" +
                 "波が物に遮られて止まったあと、遮りが無くなれば、そのまま乗り直す。線から遠いと乗らないので、飛ばされない")]
        [SerializeField, Min(0.0f)] private float _rejoinDistance = 0.3f;  ///< 乗り直せる線までの距離（m）

        [InspectorLabel("線からの高さのずれ（m）")]
        [Tooltip("波の線から、ワールドの真上へずらす量（m）。線の上に乗せるときの、足元の高さ合わせに使う")]
        [SerializeField] private float _heightOffset;           ///< 線から真上へずらす量（m）

        [InspectorLabel("線へ寄る速さ（m/s）")]
        [Tooltip("波に乗った時点で線から離れていたとき、その分を縮めて線の上へ寄っていく速さ（m/s）。急に飛ばないよう、この速さで少しずつ寄る\n" +
                 "0 にすると線へは寄らず、乗った時点のずれ（高さなど）を保ったまま進む")]
        [SerializeField, Min(0.0f)] private float _approachSpeed = 2.0f;  ///< 乗った時点の線とのずれを縮める速さ（m/s）

        [InspectorLabel("進む向きに回転を合わせる")]
        [Tooltip("オンにすると、乗っている間、線の進む向き（Y 軸まわり）を正面に向ける。曲がった所では少しずつ向きを変える\n" +
                 "「上の物を運ぶ」がオンなら、上に乗った物も自分の中心まわりに一緒に回る")]
        [SerializeField] private bool _isFacingTravelDirection;  ///< 進む向きに回転を合わせるか

        [InspectorLabel("終点に着いたとき")]
        [Tooltip("線の終点に着いたときの動き。折り返すと、先頭まで戻ってまた終点へ向かう（波が出ている間、往復する）")]
        [SerializeField] private WaveRiderEndMode _endMode = WaveRiderEndMode.Stop;  ///< 終点に着いたときの動き

        [InspectorLabel("上の物を運ぶ")]
        [Tooltip("オンにすると、上に乗っている Rigidbody（プレイヤーなど）を、自分と同じ移動量だけ一緒に運ぶ\n" +
                 "プレイヤーを乗せるなら、オンのままにする")]
        [SerializeField] private bool _isCarryingPassengers = true;  ///< 上に乗った物を運ぶか

        [InspectorLabel("スイッチで起動する")]
        [Tooltip("オンにすると、スイッチ（ReactionTrigger、PressurePlate など）が起動している間だけ波に乗る。止まっている間は、波が無いときと同じ動きになる\n" +
                 "スイッチの On Activated に Activate、On Deactivated に Deactivate をつなぐ\n" +
                 "オフなら、スイッチは関係なく、波だけで動く")]
        [SerializeField] private bool _isRequiringActivation;  ///< スイッチで起動した間だけ波に乗るか

        [InspectorLabel("始めから起動しておく")]
        [Tooltip("「スイッチで起動する」がオンのとき、ゲーム開始時から起動しているか（Activate を呼ばなくても波に乗る）")]
        [SerializeField] private bool _isActiveOnStart;        ///< ゲーム開始時から起動しているか

        private bool _isActive;               ///< スイッチで起動しているか（「スイッチで起動する」がオンのときだけ使う）
        private WaveReaction _reaction;       ///< 反応する波の種類
        private Rigidbody _body;              ///< 自分の Rigidbody（Kinematic）
        private Vector3 _initialPosition;     ///< ゲーム開始時の位置（ワールド座標）
        private WaveEmitter _currentEmitter;  ///< 今乗っている波を出しているもの。乗っていなければ null
        private float _distance;              ///< 今の線の上の道のり s（m）
        private float _direction = 1.0f;      ///< 線の上を進む向き（1: 終点へ、-1: 先頭へ。折り返しで使う）
        private bool _hasBoarded;             ///< 一度でも波に乗ったか（乗り直しの距離の制限を、最初の 1 回だけ外すため）
        private Vector3 _attachOffset;       ///< 線の上の点（高さのずれを含む）から見た、自分の位置のずれ。乗った時点で求め、少しずつ 0 へ縮める
        private Quaternion _turnDelta = Quaternion.identity;  ///< 今のステップで自分が回った分。上に乗った物を回すのに使う
        private Vector3 _velocity;           ///< 直前のステップの速度（m/s）。慣性で進むときの初速になる

        private Collider[] _ownColliders;     ///< 自分と子の Collider（上面の位置を求める）
        private readonly Collider[] _overlapBuffer = new Collider[OverlapBufferSize];  ///< 上面の重なりを受ける入れ物（確保を避ける）
        private readonly HashSet<Rigidbody> _passengers = new HashSet<Rigidbody>();  ///< 直前の物理ステップで上に乗っていた物

        /// @brief 波に乗っているか
        public bool IsRiding => _currentEmitter != null;

        /// @brief スイッチで起動したことにする。スイッチの On Activated につなぐ
        [ShowInEventMenu]
        public void Activate()
        {
            _isActive = true;
        }

        /// @brief スイッチが止まったことにする。スイッチの On Deactivated につなぐ。波に乗っていれば降りて、「波が無くなったとき」の動きになる
        [ShowInEventMenu]
        public void Deactivate()
        {
            _isActive = false;
        }

        /// @brief 起動と停止を入れ替える
        [ShowInEventMenu]
        public void Toggle()
        {
            _isActive = !_isActive;
        }

        private void Awake()
        {
            _isActive = _isActiveOnStart;
            _reaction = GetComponent<WaveReaction>();
            _body = GetComponent<Rigidbody>();
            _body.isKinematic = true;
            _body.interpolation = RigidbodyInterpolation.Interpolate;
            _initialPosition = _body.position;
            _ownColliders = GetComponentsInChildren<Collider>();
        }

        // 実行中に子の Collider が増えたり減ったりしたら、上面を求める対象を取り直す
        private void OnTransformChildrenChanged()
        {
            _ownColliders = GetComponentsInChildren<Collider>();
        }

        private void OnDisable()
        {
            _passengers.Clear();
        }

        private void FixedUpdate()
        {
            float deltaTime = Time.fixedDeltaTime;
            Vector3 current = _body.position;
            _turnDelta = Quaternion.identity;
            WaveEmitter emitter = ResolveEmitter(current);

            Vector3 next;
            if (emitter == null)
            {
                _currentEmitter = null;   // 進む向き（_direction）は残す。乗り直したとき、続きの向きで進むため
                next = LoseWave(current, deltaTime);
            }
            else
            {
                next = Ride(emitter, deltaTime);
                _velocity = (next - current) / deltaTime;
            }

            if (emitter != null)
            {
                next = PushOutOfWalls(next, emitter);
            }
            DetectPassengers();
            CarryPassengers(current, next);
            _body.MovePosition(next);
        }

        /// @brief 次の位置で壁などに食い込んでいたら、横方向だけ押し戻した位置を返す（波を出しているものは除く）
        /// 波の線は壁の面（反射する所）を通るため、中心が線の上だと壁に半分食い込む。その分を壁から離す
        /// 動く物（Kinematic でない Rigidbody）と、床のような縦方向の重なりは相手にしない
        /// 計算量: O(C × K)（C は自分の Collider の数、K は重なった Collider の数）
        private Vector3 PushOutOfWalls(Vector3 next, WaveEmitter emitter)
        {
            Vector3 push = Vector3.zero;
            foreach (Collider own in _ownColliders)
            {
                if (own == null || !own.enabled || own.isTrigger)
                {
                    continue;
                }

                // 反射の曲がり角では回りながら進むので、回ったあとの向きで調べる（回る途中の角が壁に食い込むため）
                Vector3 ownPosition = next + _turnDelta * (own.transform.position - _body.position);
                Quaternion ownRotation = _turnDelta * own.transform.rotation;
                float reach = own.bounds.extents.magnitude;   // 回っても収まるよう、どの向きにも同じ広さで探す
                int count = Physics.OverlapBoxNonAlloc(ownPosition + _turnDelta * (own.bounds.center - own.transform.position), Vector3.one * reach, _overlapBuffer, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    Collider other = _overlapBuffer[i];
                    Rigidbody otherBody = other.attachedRigidbody;
                    // 波を出しているもの（砲台など）は、線の始まりで必ず重なるので押し戻さない
                    if (other.transform.IsChildOf(transform) || other.transform.IsChildOf(emitter.transform) || (otherBody != null && !otherBody.isKinematic))
                    {
                        continue;
                    }

                    // 縦に重なっていない物（床など）は相手にしない
                    float ownCenterY = ownPosition.y + (own.bounds.center.y - own.transform.position.y);
                    float overlapY = Mathf.Min(ownCenterY + own.bounds.extents.y, other.bounds.max.y) - Mathf.Max(ownCenterY - own.bounds.extents.y, other.bounds.min.y);
                    if (overlapY <= MinPushDistance)
                    {
                        continue;
                    }

                    // 背の高い壁だと、一番浅い押し出しは「真上」になってしまう。高さを相手の中心に揃えて、横向きの押し出しを出させる
                    Vector3 levelPosition = ownPosition + Vector3.up * (other.bounds.center.y - ownCenterY);
                    if (Physics.ComputePenetration(own, levelPosition, ownRotation,
                            other, other.transform.position, other.transform.rotation, out Vector3 direction, out float distance))
                    {
                        direction.y = 0.0f;
                        if (distance > MinPushDistance && direction.sqrMagnitude > MinPushHorizontalSqrMagnitude)
                        {
                            push += direction / direction.sqrMagnitude * distance;   // 横向きの分だけで、押し出す長さを割り戻す
                        }
                    }
                }
            }
            return next + push;
        }

        /// @brief 自分の上面のすぐ上にある Rigidbody を、乗っている物として集める
        /// 接触の通知（OnCollisionStay）は、相手が眠ると届かず、運び損ねて滑るので、毎ステップ上面を直接調べる
        /// 計算量: O(C + K)（C は自分の Collider の数、K は上面で重なった Collider の数）
        private void DetectPassengers()
        {
            _passengers.Clear();
            if (!_isCarryingPassengers)
            {
                return;
            }

            if (!TryGetOwnBounds(out Bounds bounds))
            {
                return;
            }

            Vector3 center = new Vector3(bounds.center.x, bounds.max.y + PassengerSlabHeight * 0.5f - TopContactTolerance, bounds.center.z);
            Vector3 halfExtents = new Vector3(bounds.extents.x, PassengerSlabHeight * 0.5f, bounds.extents.z);
            int count = Physics.OverlapBoxNonAlloc(center, halfExtents, _overlapBuffer, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Rigidbody other = _overlapBuffer[i].attachedRigidbody;
                if (other == null || other == _body || other.isKinematic)
                {
                    continue;
                }
                // 足元が上面より下にある物（横から触れているだけの物）は乗っていないとみなす
                if (_overlapBuffer[i].bounds.min.y >= bounds.max.y - TopContactTolerance)
                {
                    _passengers.Add(other);
                }
            }
        }

        /// @brief 今のステップで実際に乗る波を決める。乗れなければ null（「波が無くなったとき」の動きになる）
        /// ・波が物に遮られて、線が自分の位置より手前までに縮んだときは、乗れない（遮られた位置へ飛ばされないため）
        /// ・一度乗ったあとに乗り直すときは、線までの距離が「乗り直せる距離」以内のときだけ乗る。乗るときは、今の位置に一番近い線の上の点から始める
        /// 計算量: O(N)（N は線の点の数）
        private WaveEmitter ResolveEmitter(Vector3 current)
        {
            WaveEmitter emitter = FindEmitter();
            if (emitter == null)
            {
                return null;
            }

            var corners = emitter.BeamCorners;
            if (emitter != _currentEmitter)
            {
                float distance = WaveLinePath.Project(corners, GetLinePosition(current), out float sqrDistance);
                // 最初に乗るときは、どれだけ離れていても線の上へ移る。一度乗ったあとは、近いときだけ乗り直す
                if (_hasBoarded && sqrDistance > _rejoinDistance * _rejoinDistance)
                {
                    return null;
                }
                if (!_hasBoarded)
                {
                    _direction = 1.0f;   // 最初に乗るときだけ終点へ向かう。乗り直すときは遮られる前の向きを続ける
                }
                _hasBoarded = true;
                _currentEmitter = emitter;
                _distance = distance;
                // 線の上へ飛ばず、今の位置から少しずつ寄るため、乗った時点のずれを覚える
                Vector3 boardPoint = WaveLinePath.GetPointAt(corners, distance, out Vector3 _);
                _attachOffset = current - (boardPoint + Vector3.up * _heightOffset);
            }
            else if (_distance > WaveLinePath.GetLength(corners) + BeamEndTolerance)
            {
                return null;
            }
            return emitter;
        }

        /// @brief 乗る波を出しているものを決める
        /// 今乗っているものが有効ならそれを使い続ける（近さで乗り換えて、行き来しないため）
        /// 計算量: O(E × N)（E は波を出しているものの数、N は線の点の数）。_emitter 指定なら O(1)
        private WaveEmitter FindEmitter()
        {
            if (_emitter != null)
            {
                return CanRide(_emitter) ? _emitter : null;
            }
            if (_currentEmitter != null && CanRide(_currentEmitter))
            {
                return _currentEmitter;
            }

            Vector3 linePosition = GetLinePosition(_body.position);
            WaveEmitter nearest = null;
            float nearestSqrDistance = float.MaxValue;
            foreach (WaveEmitter candidate in WaveEmitter.ActiveEmitters)
            {
                if (!CanRide(candidate))
                {
                    continue;
                }
                WaveLinePath.Project(candidate.BeamCorners, linePosition, out float sqrDistance);
                if (sqrDistance < nearestSqrDistance)
                {
                    nearestSqrDistance = sqrDistance;
                    nearest = candidate;
                }
            }
            return nearest;
        }

        /// @brief その WaveEmitter の波に乗れるか。ビームを出していて、線があり、反応する波の種類であること
        private bool CanRide(WaveEmitter emitter)
        {
            BeamSourceInfo source = emitter.CurrentSource;
            return (!_isRequiringActivation || _isActive)
                && emitter.isActiveAndEnabled
                && emitter.IsFiring
                && emitter.FireShape == WaveFireShape.Beam
                && source != null
                && emitter.BeamCorners.Count >= 2
                && _reaction.CanReactTo(source.WaveKind);
        }

        /// @brief 波に乗れないときの 1 ステップ分の動きで、次の位置を返す。「波が無くなったとき」の設定に従う
        private Vector3 LoseWave(Vector3 current, float deltaTime)
        {
            switch (_lostWaveMode)
            {
                case WaveRiderLostWaveMode.Inertia:
                    float speed = Mathf.Max(0.0f, _velocity.magnitude - _inertiaDeceleration * deltaTime);
                    _velocity = speed > 0.0f ? _velocity.normalized * speed : Vector3.zero;
                    return MoveByInertia(current, deltaTime);

                case WaveRiderLostWaveMode.ReturnToStart:
                    _velocity = Vector3.zero;
                    return Vector3.MoveTowards(current, _initialPosition, _returnSpeed * deltaTime);

                default:
                    _velocity = Vector3.zero;
                    return current;
            }
        }

        /// @brief 慣性で 1 ステップ分進める。壁などに当たる手前で止まる（当たったら速さは 0 になる）
        /// 上に乗っている物は当たり判定から外す。SweepTestAll は配列を作るが、慣性で進んでいる間だけなので許容する
        /// 計算量: O(H)（H は進む先の当たり判定の数）
        private Vector3 MoveByInertia(Vector3 current, float deltaTime)
        {
            Vector3 step = _velocity * deltaTime;
            float stepLength = step.magnitude;
            if (stepLength <= 0.0f)
            {
                return current;
            }

            Vector3 direction = step / stepLength;
            float nearestDistance = stepLength;
            bool isBlocked = false;
            foreach (RaycastHit hit in _body.SweepTestAll(direction, stepLength, QueryTriggerInteraction.Ignore))
            {
                Rigidbody hitBody = hit.rigidbody;
                if (hitBody != null && _passengers.Contains(hitBody))
                {
                    continue;
                }
                if (hit.distance < nearestDistance)
                {
                    nearestDistance = hit.distance;
                    isBlocked = true;
                }
            }

            if (isBlocked)
            {
                _velocity = Vector3.zero;
            }
            return current + direction * nearestDistance;
        }

        /// @brief 位置から高さのずれを引いた、線と比べる位置を返す（乗り直しの最寄りの点を、ずれに引きずられずに探すため）
        private Vector3 GetLinePosition(Vector3 position)
        {
            return position - Vector3.up * _heightOffset;
        }

        /// @brief 1 ステップ分、線に沿って進めて、次の位置を返す（回転も合わせる）
        private Vector3 Ride(WaveEmitter emitter, float deltaTime)
        {
            var corners = emitter.BeamCorners;
            // 波は中心を通るので、終点（壁など）に中心まで行くと半分食い込む。先端が終点に着く所で止める
            WaveLinePath.GetPointAt(corners, float.MaxValue, out Vector3 endDirection);
            float length = Mathf.Max(0.0f, WaveLinePath.GetLength(corners) - GetFrontExtent(endDirection));

            _distance += _carrySpeed * _direction * deltaTime;
            if (_distance >= length)
            {
                _distance = length;
                if (_endMode == WaveRiderEndMode.PingPong)
                {
                    _direction = -1.0f;
                }
            }
            else if (_distance <= 0.0f)
            {
                _distance = 0.0f;
                _direction = 1.0f;
            }

            Vector3 point = WaveLinePath.GetPointAt(corners, _distance, out Vector3 travelDirection);
            FaceTravelDirection(travelDirection * _direction, deltaTime);
            _attachOffset = Vector3.MoveTowards(_attachOffset, Vector3.zero, _approachSpeed * deltaTime);
            return point + Vector3.up * _heightOffset + _attachOffset;
        }

        /// @brief 自分の（トリガーでない）当たり判定をまとめた、軸平行の箱を求める
        /// @return 使える当たり判定があれば true
        /// 計算量: O(C)（C は自分の当たり判定の数）
        private bool TryGetOwnBounds(out Bounds bounds)
        {
            bounds = default;
            bool hasBounds = false;
            foreach (Collider own in _ownColliders)
            {
                if (own == null || !own.enabled || own.isTrigger)
                {
                    continue;
                }
                if (hasBounds)
                {
                    bounds.Encapsulate(own.bounds);
                }
                else
                {
                    bounds = own.bounds;
                    hasBounds = true;
                }
            }
            return hasBounds;
        }

        /// @brief 自分の中心から、ある向きへ一番出っ張っている所までの距離を返す（m）
        /// 回転しても足りるよう、当たり判定をまとめた軸平行の箱で測る。当たり判定が無ければ 0
        /// 計算量: O(C)（C は自分の当たり判定の数）
        private float GetFrontExtent(Vector3 direction)
        {
            if (!TryGetOwnBounds(out Bounds bounds))
            {
                return 0.0f;
            }

            Vector3 absDirection = new Vector3(Mathf.Abs(direction.x), Mathf.Abs(direction.y), Mathf.Abs(direction.z));
            float extent = Vector3.Dot(bounds.center - _body.position, direction) + Vector3.Dot(bounds.extents, absDirection);
            return Mathf.Max(0.0f, extent);
        }

        /// @brief 進む向き（Y 軸まわり）へ、少しずつ回転を合わせる
        private void FaceTravelDirection(Vector3 travelDirection, float deltaTime)
        {
            travelDirection.y = 0.0f;
            if (!_isFacingTravelDirection || travelDirection.sqrMagnitude < MinDirectionSqrMagnitude)
            {
                return;
            }
            Quaternion target = Quaternion.LookRotation(travelDirection.normalized, Vector3.up);
            Quaternion rotated = Quaternion.RotateTowards(_body.rotation, target, TurnDegreesPerSecond * deltaTime);
            _turnDelta = rotated * Quaternion.Inverse(_body.rotation);   // 上に乗った物も、この分だけ自分の中心まわりに回す
            _body.MoveRotation(rotated);
        }

        /// @brief 上に乗っていた物を、自分の移動と回転と同じだけ動かす
        /// 位置と向きを直接変える（MovePosition は、プレイヤー自身の歩きと食い合うため使わない）
        /// 回転するときは、自分の中心まわりに回るので、端に乗っていても同じ場所に乗り続ける
        /// 計算量: O(P)（P は乗っている物の数）
        private void CarryPassengers(Vector3 current, Vector3 next)
        {
            if (_passengers.Count == 0)
            {
                return;
            }

            bool isTurning = _turnDelta != Quaternion.identity;
            if (!isTurning && next == current)
            {
                return;
            }
            foreach (Rigidbody passenger in _passengers)
            {
                if (passenger == null)
                {
                    continue;
                }
                passenger.position = next + _turnDelta * (passenger.position - current);
                if (isTurning)
                {
                    passenger.rotation = _turnDelta * passenger.rotation;
                }
            }
        }
    }
}
