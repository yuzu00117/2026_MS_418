using System.Collections.Generic;
using Aura.Inspector;
using Aura.ObjectInfo;
using Aura.Wave;
using UnityEngine;
using UnityEngine.Events;

namespace Aura.Gimmick
{
    /// @brief タレットが出す波（ビームの線）に運ばれるオブジェクト。上に乗った物（プレイヤーを含む）も一緒に運ぶ
    /// ・反応する波の種類は WaveReaction（電磁波/超音波のチェック）で決める。反応しない波では動かず、波が無いときと同じ扱いになる
    /// ・波が出ている間は常に線の上にいて、線の上の道のり s を一定の速さで進める。波が曲がっても跳ね返っても、s を保ったまま線に沿う
    /// ・波が無くなったら（遮られた、止まった）、「波が無くなったとき」の設定に従う（その場で止まる / 慣性で進んでから止まる / 初期位置へ戻る / 経路探索（NavMesh）で初期位置へ戻る）
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
        private const float ContactDistance = 0.001f;       ///< 当たった距離がこれ以下なら、すでに面に接しているとみなす（m）
        private const float MinSlideSqrMagnitude = 1.0e-8f;   ///< 面に沿って滑らせる量の長さの 2 乗がこれ以下なら、滑らせない
        private const float NavReplanInterval = 0.5f;         ///< 経路探索で戻るとき、経路を引き直す間隔の最短（秒）
        private const float ReturnedDistance = 0.01f;        ///< 初期位置にこの距離以内に着いたら、戻り終えたとみなす（m）
        private const float ReturnedAngle = 0.5f;            ///< 初期の向きにこの角度以内に着いたら、向きも戻ったとみなす（度）
        private const int OverlapBufferSize = 32;            ///< 上面で同時に調べる Collider の最大数。超えた分は乗っている物として数えない
        private const float TopContactTolerance = 0.1f;      ///< 相手の足元が自分の上面からこの高さ以内に潜っていても、上に乗っているとみなす（m）

        [InspectorLabel("乗る波を出すもの")]
        [Tooltip("乗る波を出す WaveEmitter（タレットなど）。必ず指定する。空だとエラーを出し、波に乗らない")]
        [SerializeField] private WaveEmitter _emitter;        ///< 乗る波を出すもの（必須）

        [InspectorLabel("運ばれる速さ（m/s）")]
        [Tooltip("波の線に沿って進む速さ（m/s）。曲がった所の角も、道のりにそのまま数える。0 なら乗っても進まない")]
        [SerializeField, Min(0.0f)] private float _carrySpeed = 2.0f;  ///< 波に運ばれる速さ（m/s）

        [InspectorLabel("波が無くなったとき")]
        [Tooltip("波が遮られた、止まった、スイッチが止まったときの動き\n" +
                 "・その場で止まる: 今の位置で止まる\n" +
                 "・慣性で進んでから止まる: 直前の速さと向きのまま進み、「慣性の減速（m/s²）」で減速して止まる\n" +
                 "・初期位置へ戻る: ゲーム開始時の位置へ「戻る速さ」でまっすぐ戻る。途中に壁などがあれば、その手前で止まる\n" +
                 "・経路探索で初期位置へ戻る: NavMesh の経路で壁を避けて戻る。NavMesh のベイクが必要（無ければまっすぐ戻る）")]
        [SerializeField] private WaveRiderLostWaveMode _lostWaveMode = WaveRiderLostWaveMode.StopInPlace;  ///< 波が無くなったときの動き

        [InspectorLabel("慣性の減速（m/s²）")]
        [Tooltip("「慣性で進んでから止まる」のとき、1 秒あたりに減る速さ（m/s²）。大きいほど早く止まる。0 なら止まらず進み続ける\n" +
                 "慣性で進む間に壁などに当たったら、その手前で止まる（上に乗っている物は無視する）")]
        [SerializeField, Min(0.0f)] private float _inertiaDeceleration = 3.0f;  ///< 慣性で進むときの減速（m/s²）

        [InspectorLabel("戻る速さ（m/s）")]
        [Tooltip("「初期位置へ戻る」のとき、ゲーム開始時の位置へ戻る速さ（m/s）。0 なら戻らず、その場で止まる")]
        [SerializeField, Min(0.0f)] private float _returnSpeed = 2.0f;  ///< 初期位置へ戻る速さ（m/s）

        [InspectorLabel("最初に乗れる距離（m）")]
        [Tooltip("最初に波に乗るとき、自分から波の線までの距離がこれ以内のときだけ乗る（m）。離れすぎていると、波が出ていても乗らない\n" +
                 "遠くにあるライダーが、壁越しに線へ引き込まれないための上限")]
        [SerializeField, Min(0.0f)] private float _boardDistance = 3.0f;  ///< 最初に乗れる線までの距離（m）

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

        [InspectorLabel("初期位置へ戻り終えたとき")]
        [Tooltip("波に乗ったあと、「初期位置へ戻る」系の動きで初期位置（向きを合わせる設定なら初期の向きも）に着いたときに 1 回呼ぶ\n" +
                 "壁などに阻まれて着けなかったときは呼ばない。次に波に乗ったあと、また戻り終えたときに呼ぶ")]
        [SerializeField] private UnityEvent _onReturned = new UnityEvent();  ///< 初期位置へ戻り終えたときに呼ぶ

        private bool _isActive;               ///< スイッチで起動しているか（「スイッチで起動する」がオンのときだけ使う）
        private WaveReaction _reaction;       ///< 反応する波の種類
        private Rigidbody _body;              ///< 自分の Rigidbody（Kinematic）
        private Vector3 _initialPosition;     ///< ゲーム開始時の位置（ワールド座標）
        private Quaternion _initialRotation;  ///< ゲーム開始時の向き。初期位置へ戻るとき、この向きにも戻す
        private WaveEmitter _currentEmitter;  ///< 今乗っている波を出しているもの。乗っていなければ null
        private float _distance;              ///< 今の線の上の道のり s（m）
        private float _direction = 1.0f;      ///< 線の上を進む向き（1: 終点へ、-1: 先頭へ。折り返しで使う）
        private bool _hasBoarded;             ///< 一度でも波に乗ったか（乗り直しの距離の制限を、最初の 1 回だけ外すため）
        private Vector3 _attachOffset;       ///< 線の上の点（高さのずれを含む）から見た、自分の位置のずれ。乗った時点で求め、少しずつ 0 へ縮める
        private Quaternion _turnDelta = Quaternion.identity;  ///< 今のステップで自分が回った分。上に乗った物を回すのに使う
        private Vector3 _velocity;           ///< 直前のステップの速度（m/s）。慣性で進むときの初速になる
        private readonly WaveRiderNavReturn _navReturn = new WaveRiderNavReturn();  ///< 経路探索で戻るときの経路
        private float _nextNavPlanTime;     ///< 次に経路を引いてよい時刻（Time.time）
        private bool _isNavPlanFailed;       ///< 直前の経路探索が失敗したか（失敗の間は、まっすぐ戻る）
        private bool _hasWarnedNavMesh;      ///< NavMesh の経路が引けない警告を出したか（毎ステップ出さないため）
        private bool _isReturnPending;       ///< 波に乗ったあと、まだ初期位置へ戻り終えていないか（戻り終えた通知を 1 回だけ出すため）
        private bool _hasWarnedOverlapOverflow;  ///< 上面の Collider が入れ物の数を超えた警告を出したか（毎ステップ出さないため）

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

        /// @brief 乗る波を出すものを差し替える。コードで生成したライダーに、生成後に割り当てるときに使う
        /// 乗っていた波とは別のものになるので、降りて「波が無くなったとき」の動きから始める。null は割り当てない（エラーを出す）
        public void SetEmitter(WaveEmitter emitter)
        {
            if (emitter == null)
            {
                Debug.LogError("WaveRider.SetEmitter: null は割り当てられません", this);
                return;
            }
            _emitter = emitter;
            _currentEmitter = null;
            _hasBoarded = false;   // 別の波なので、最初に乗る距離の上限（遠くから引き込まない）で判定し直す
        }

        /// @brief 「初期位置へ戻る」で戻る先を変える。コードで生成したあとに位置を動かしたときや、置き直したときに使う
        /// 初期位置は Awake の位置で決まるため、Instantiate のあとに transform を動かすと、戻る先が生成時の位置のままになる
        public void SetInitialPosition(Vector3 position)
        {
            _initialPosition = position;
            _navReturn.Reset();   // 引いてあった経路は古い目標向きなので捨てる
            _nextNavPlanTime = 0.0f;
        }

        /// @brief 「初期位置へ戻る」で戻る先の向きを変える。生成したあとに向きを変えたときに使う
        /// 初期の向きは Awake の向きで決まるため、Instantiate のあとに回すと、戻る向きが生成時のままになる
        public void SetInitialRotation(Quaternion rotation)
        {
            _initialRotation = rotation;
        }

        private void Awake()
        {
            if (_emitter == null)
            {
                Debug.LogError("WaveRider: 「乗る波を出すもの」（_emitter）が未設定です。波に乗りません", this);
            }
            _isActive = _isActiveOnStart;
            _reaction = GetComponent<WaveReaction>();
            _body = GetComponent<Rigidbody>();
            _body.isKinematic = true;
            _body.interpolation = RigidbodyInterpolation.Interpolate;
            _initialPosition = _body.position;
            _initialRotation = _body.rotation;
            _ownColliders = GetComponentsInChildren<Collider>();
        }

        // 実行中に子の Collider が増えたり減ったりしたら、上面を求める対象を取り直す
        private void OnTransformChildrenChanged()
        {
            _ownColliders = GetComponentsInChildren<Collider>();
        }

        // 再び有効になったとき、無効の前の慣性や、乗っていた波、引いてあった経路を持ち越さない
        private void OnEnable()
        {
            _velocity = Vector3.zero;
            _currentEmitter = null;
            _attachOffset = Vector3.zero;
            _navReturn.Reset();
            _nextNavPlanTime = 0.0f;
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
                NotifyIfReturned(next);
            }
            else
            {
                next = Ride(emitter, deltaTime);
                _isReturnPending = true;
                _navReturn.Reset();   // 乗っている間に動いた分、戻る経路は引き直す
                _nextNavPlanTime = 0.0f;
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
        /// 自分の Collider が複数あっても同じ壁を二重に押さないよう、Collider ごとの押し戻しのうち一番大きいものだけを使う
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

                Vector3 ownPush = GetPushOfCollider(own, next, emitter);
                if (ownPush.sqrMagnitude > push.sqrMagnitude)
                {
                    push = ownPush;
                }
            }
            return next + push;
        }

        /// @brief 自分の Collider 1 つが、次の位置で壁などから受ける押し戻し（横方向）を返す
        /// 計算量: O(K)（K は重なった Collider の数）
        private Vector3 GetPushOfCollider(Collider own, Vector3 next, WaveEmitter emitter)
        {
            Vector3 push = Vector3.zero;
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
            return push;
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
            if (count >= OverlapBufferSize && !_hasWarnedOverlapOverflow)
            {
                _hasWarnedOverlapOverflow = true;
                Debug.LogWarning("WaveRider: 上面の Collider が多すぎて、乗っている物を取りこぼす恐れがあります（最大 " + OverlapBufferSize + " 個）", this);
            }
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
        /// 乗る波は _emitter だけ。近い線を探すと、遠くの線へ引き込まれて壁を抜けるため、必ず指定させる
        /// 計算量: O(N)（N は線の点の数）
        private WaveEmitter ResolveEmitter(Vector3 current)
        {
            WaveEmitter emitter = _emitter;
            if (emitter == null || !CanRide(emitter))
            {
                return null;
            }

            var corners = emitter.BeamCorners;
            if (emitter != _currentEmitter)
            {
                float distance = WaveLinePath.Project(corners, GetLinePosition(current), out float sqrDistance);
                // 最初に乗るときも、乗り直すときも、線までの距離が上限以内のときだけ乗る（遠くから壁越しに引き込まれないため）
                float limit = _hasBoarded ? _rejoinDistance : _boardDistance;
                if (sqrDistance > limit * limit)
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

        /// @brief その WaveEmitter の波に乗れるか。ビームを出していて、線があり、WaveReaction が有効で、反応する波の種類であること
        private bool CanRide(WaveEmitter emitter)
        {
            BeamSourceInfo source = emitter.CurrentSource;
            return (!_isRequiringActivation || _isActive)
                && emitter.isActiveAndEnabled
                && emitter.IsFiring
                && emitter.FireShape == WaveFireShape.Beam
                && source != null
                && emitter.BeamCorners.Count >= 2
                && _reaction.enabled
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
                    RestoreRotation(deltaTime);
                    return ReturnStraight(current, deltaTime);

                case WaveRiderLostWaveMode.ReturnByNavMesh:
                    _velocity = Vector3.zero;
                    RestoreRotation(deltaTime);
                    return ReturnAlongNavMesh(current, deltaTime);

                default:
                    _velocity = Vector3.zero;
                    return current;
            }
        }

        /// @brief 初期位置へ戻る間に、向きも初期の向きへ少しずつ戻す。進む向きに回転を合わせていたときだけ
        /// 上に乗った物も、乗っている間と同じく、この分だけ自分の中心まわりに回す
        private void RestoreRotation(float deltaTime)
        {
            // 戻る速さが 0 のときは「戻らずその場で止まる」ので、向きも戻さない
            if (!_isFacingTravelDirection || _returnSpeed <= 0.0f)
            {
                return;
            }
            Quaternion rotated = Quaternion.RotateTowards(_body.rotation, _initialRotation, TurnDegreesPerSecond * deltaTime);
            Quaternion delta = rotated * Quaternion.Inverse(_body.rotation);
            if (IsRotationBlocked(delta))
            {
                return;   // 回ると壁に食い込むなら、このステップは回さない（位置が動いて空いたら、また回る）
            }
            _turnDelta = delta;
            _body.MoveRotation(rotated);
        }

        /// @brief 自分の中心まわりに delta だけ回ると、壁などに横から食い込むか
        /// 位置の移動は壁の手前で止めるが、回転は別なので、壁際で回って角が食い込まないよう調べる
        /// 乗っている物と動く Rigidbody と自分の子は相手にしない。縦に重ならない物（床）も相手にしない
        /// 計算量: O(C × K)（C は自分の Collider の数、K は重なった Collider の数）
        private bool IsRotationBlocked(Quaternion delta)
        {
            Vector3 bodyPosition = _body.position;
            foreach (Collider own in _ownColliders)
            {
                if (own == null || !own.enabled || own.isTrigger)
                {
                    continue;
                }

                Vector3 ownPosition = bodyPosition + delta * (own.transform.position - bodyPosition);
                Quaternion ownRotation = delta * own.transform.rotation;
                Vector3 ownCenter = bodyPosition + delta * (own.bounds.center - bodyPosition);
                float reach = own.bounds.extents.magnitude;
                int count = Physics.OverlapBoxNonAlloc(ownCenter, Vector3.one * reach, _overlapBuffer, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    Collider other = _overlapBuffer[i];
                    Rigidbody otherBody = other.attachedRigidbody;
                    if (other.transform.IsChildOf(transform) || (otherBody != null && (!otherBody.isKinematic || _passengers.Contains(otherBody))))
                    {
                        continue;
                    }

                    // 縦に重ならない物（床など）は相手にしない。背の高い壁は、高さを相手の中心に揃えて横向きの押し出しを出させる
                    float overlapY = Mathf.Min(ownCenter.y + own.bounds.extents.y, other.bounds.max.y) - Mathf.Max(ownCenter.y - own.bounds.extents.y, other.bounds.min.y);
                    if (overlapY <= MinPushDistance)
                    {
                        continue;
                    }
                    Vector3 levelPosition = ownPosition + Vector3.up * (other.bounds.center.y - ownCenter.y);
                    if (Physics.ComputePenetration(own, levelPosition, ownRotation, other, other.transform.position, other.transform.rotation, out Vector3 direction, out float distance)
                        && distance > MinPushDistance)
                    {
                        direction.y = 0.0f;
                        if (direction.sqrMagnitude > MinPushHorizontalSqrMagnitude)
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        /// @brief 波に乗ったあとの戻る動きで、初期位置（と初期の向き）に着いたら、戻り終えた通知を 1 回出す
        private void NotifyIfReturned(Vector3 position)
        {
            bool isReturnMode = _lostWaveMode == WaveRiderLostWaveMode.ReturnToStart || _lostWaveMode == WaveRiderLostWaveMode.ReturnByNavMesh;
            if (!_isReturnPending || !isReturnMode)
            {
                return;
            }
            bool isAtPosition = (position - _initialPosition).sqrMagnitude <= ReturnedDistance * ReturnedDistance;
            bool isAtRotation = !_isFacingTravelDirection || Quaternion.Angle(_body.rotation, _initialRotation) <= ReturnedAngle;
            if (isAtPosition && isAtRotation)
            {
                _isReturnPending = false;
                _onReturned.Invoke();
            }
        }

        /// @brief 初期位置へまっすぐ 1 ステップ分戻る。途中の壁は通り抜けず、その手前で止まる（慣性と同じ）
        private Vector3 ReturnStraight(Vector3 current, float deltaTime)
        {
            Vector3 target = Vector3.MoveTowards(current, _initialPosition, _returnSpeed * deltaTime);
            return SweepMove(current, target - current, out bool _);
        }

        /// @brief NavMesh の経路で初期位置へ 1 ステップ分戻る。経路が引けないときは、まっすぐ戻る
        /// 壁に当たって止まったら経路を捨て、次のステップで今の位置から引き直す
        private Vector3 ReturnAlongNavMesh(Vector3 current, float deltaTime)
        {
            if (!_navReturn.HasPlan)
            {
                // 経路探索は重いので、引けなかったときと壁で止まったときは、間隔をあけて引き直す
                if (Time.time < _nextNavPlanTime)
                {
                    return _isNavPlanFailed ? ReturnStraight(current, deltaTime) : current;
                }
                _nextNavPlanTime = Time.time + NavReplanInterval;
                _isNavPlanFailed = !_navReturn.Plan(current, _initialPosition);
                if (_isNavPlanFailed)
                {
                    if (!_hasWarnedNavMesh)
                    {
                        _hasWarnedNavMesh = true;
                        Debug.LogWarning("WaveRider: NavMesh の経路が引けないので、まっすぐ戻ります。NavMesh をベイクしてあるか確認してください", this);
                    }
                    return ReturnStraight(current, deltaTime);
                }
                _hasWarnedNavMesh = false;   // 引けたので、あとでまた引けなくなったら、もう一度警告を出す
            }

            Vector3 target = _navReturn.Step(_returnSpeed * deltaTime);
            Vector3 next = SweepMove(current, target - current, out bool isBlocked);
            if (isBlocked)
            {
                _navReturn.Reset();
            }
            return next;
        }

        /// @brief 慣性で 1 ステップ分進める。壁などに当たる手前で止まる（当たったら速さは 0 になる）
        private Vector3 MoveByInertia(Vector3 current, float deltaTime)
        {
            Vector3 next = SweepMove(current, _velocity * deltaTime, out bool isBlocked);
            if (isBlocked)
            {
                _velocity = Vector3.zero;
            }
            return next;
        }

        /// @brief step だけ進めた位置を返す。壁などに当たるなら、その手前までにする
        /// すでに面に接している所（床の上など）で止まってしまわないよう、接している面に沿って 1 回だけ滑らせる
        /// 計算量: O(H)（H は進む先の当たり判定の数）
        private Vector3 SweepMove(Vector3 current, Vector3 step, out bool isBlocked)
        {
            Vector3 next = SweepOnce(current, step, out isBlocked, out Vector3 contactNormal, out float hitDistance);
            if (isBlocked && hitDistance < ContactDistance)
            {
                Vector3 slide = Vector3.ProjectOnPlane(step, contactNormal);
                if (slide.sqrMagnitude > MinSlideSqrMagnitude)
                {
                    next = SweepOnce(current, slide, out isBlocked, out _, out _);
                }
            }
            return next;
        }

        /// @brief step だけ進めた位置を返す。壁などに当たるなら、その手前までにして、当たった面の向きと距離も返す
        /// 上に乗っている物は当たり判定から外す。SweepTestAll は配列を作るが、波に乗っていない間だけなので許容する
        /// 計算量: O(H)（H は進む先の当たり判定の数）
        private Vector3 SweepOnce(Vector3 current, Vector3 step, out bool isBlocked, out Vector3 contactNormal, out float hitDistance)
        {
            isBlocked = false;
            contactNormal = Vector3.zero;
            float stepLength = step.magnitude;
            hitDistance = stepLength;
            if (stepLength <= 0.0f)
            {
                return current;
            }

            Vector3 direction = step / stepLength;
            foreach (RaycastHit hit in _body.SweepTestAll(direction, stepLength, QueryTriggerInteraction.Ignore))
            {
                Rigidbody hitBody = hit.rigidbody;
                if (hitBody != null && _passengers.Contains(hitBody))
                {
                    continue;
                }
                if (hit.distance < hitDistance)
                {
                    hitDistance = hit.distance;
                    contactNormal = hit.normal;
                    isBlocked = true;
                }
            }
            return current + direction * hitDistance;
        }

        /// @brief 位置から高さのずれを引いた、線と比べる位置を返す（乗り直しの最寄りの点を、ずれに引きずられずに探すため）
        private Vector3 GetLinePosition(Vector3 position)
        {
            return position - Vector3.up * _heightOffset;
        }

        /// @brief 線の上の道のりを 1 ステップ分進める。終点では止まる（折り返すなら向きを変える）
        /// 始まりでも終点と同じく、広がり分だけ手前で折り返す
        private void AdvanceDistance(IReadOnlyList<Vector3> corners, float deltaTime)
        {
            // 波は中心を通るので、終点（壁など）に中心まで行くと半分食い込む。先端が終点に着く所で止める
            WaveLinePath.GetPointAt(corners, float.MaxValue, out Vector3 endDirection);
            float length = Mathf.Max(0.0f, WaveLinePath.GetLength(corners) - GetFrontExtent(endDirection));

            // 始まりも終点と同じく、後ろ側の広がり分だけ手前で折り返す（砲台は押し戻しの対象外なので、食い込まないため）
            WaveLinePath.GetPointAt(corners, 0.0f, out Vector3 startDirection);
            float startLimit = Mathf.Min(GetFrontExtent(-startDirection), length);

            _distance += _carrySpeed * _direction * deltaTime;
            if (_distance >= length)
            {
                _distance = length;
                if (_endMode == WaveRiderEndMode.PingPong)
                {
                    _direction = -1.0f;
                }
            }
            else if (_direction < 0.0f && _distance <= startLimit)
            {
                _distance = startLimit;
                _direction = 1.0f;
            }
        }

        /// @brief 1 ステップ分、線に沿って進めて、次の位置を返す（回転も合わせる）
        private Vector3 Ride(WaveEmitter emitter, float deltaTime)
        {
            var corners = emitter.BeamCorners;
            AdvanceDistance(corners, deltaTime);

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
