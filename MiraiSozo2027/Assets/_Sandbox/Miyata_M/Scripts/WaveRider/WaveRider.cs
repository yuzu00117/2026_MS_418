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
        private const float NavReplanInterval = 0.5f;         ///< 経路探索で戻るとき、経路を引き直す間隔の最短（秒）
        private const float ReturnedDistance = 0.01f;        ///< 初期位置にこの距離以内に着いたら、戻り終えたとみなす（m）
        private const float ReturnedAngle = 0.5f;            ///< 初期の向きにこの角度以内に着いたら、向きも戻ったとみなす（度）

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

        [InspectorLabel("線に付いていく速さの上限（m/s）")]
        [Tooltip("砲台が回ったり波が曲がったりして、線そのものが動いたとき、線に付いていく速さの上限（m/s）。速く動く線には遅れて付いていく\n" +
                 "1 ステップに大きく飛んで壁を抜けたり、上に乗った物を飛ばしたりしないための上限。「運ばれる速さ」+「線へ寄る速さ」より小さくても、そちらを上限にする")]
        [SerializeField, Min(0.0f)] private float _maxFollowSpeed = 10.0f;  ///< 線が動いたとき、線に付いていく速さの上限（m/s）

        [InspectorLabel("線から遅れて降りる距離（m）")]
        [Tooltip("線そのものが速く動いて、付いていく速さの上限で追いつけず、線からこの距離より遅れたら、波から降りる（m）。降りたあとは「波が無くなったとき」の動きになる\n" +
                 "線が大きく離れたとき、空中をまっすぐ追いかけて壁を抜けないための上限。0 なら降りずに追いかけ続ける")]
        [SerializeField, Min(0.0f)] private float _dropLagDistance = 1.0f;  ///< 線からこの距離より遅れたら降りる（m）

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
        private bool _isActiveSet;            ///< Activate などで起動の状態が決まったか（Awake より前に呼ばれても、「始めから起動しておく」で上書きしないため）
        private WaveReaction _reaction;       ///< 反応する波の種類
        private Rigidbody _body;              ///< 自分の Rigidbody（Kinematic）
        private Vector3 _initialPosition;     ///< ゲーム開始時の位置（ワールド座標）
        private Quaternion _initialRotation;  ///< ゲーム開始時の向き。初期位置へ戻るとき、この向きにも戻す
        private WaveEmitter _currentEmitter;  ///< 今乗っている波を出しているもの。乗っていなければ null
        private float _distance;              ///< 今の線の上の道のり s（m）
        private float _direction = 1.0f;      ///< 線の上を進む向き（1: 終点へ、-1: 先頭へ。折り返しで使う）
        private bool _hasBoarded;             ///< 一度でも波に乗ったか（乗り直しの距離の制限を、最初の 1 回だけ外すため）
        private Vector3 _attachOffset;       ///< 線の上の点（高さのずれを含む）から見た、自分の位置のずれ。乗った時点で求め、少しずつ 0 へ縮める
        private float _lineDeviation;        ///< 最後に乗っていたステップで、線の上の点（高さのずれを含む）から離れていた距離（m）。乗り直せる距離に足す
        private float _followLag;            ///< 直前のステップで、線に付いていく速さの上限で抑えたため、目指す位置から遅れた距離（m）
        private Quaternion _turnDelta = Quaternion.identity;  ///< 今のステップで自分が回った分。上に乗った物を回すのに使う
        private Vector3 _velocity;           ///< 直前のステップの速度（m/s）。慣性で進むときの初速になる
        private readonly WaveRiderNavReturn _navReturn = new WaveRiderNavReturn();  ///< 経路探索で戻るときの経路
        private float _nextNavPlanTime;     ///< 次に経路を引いてよい時刻（Time.time）
        private bool _isNavPlanFailed;       ///< 直前の経路探索が失敗したか（失敗の間は、まっすぐ戻る）
        private bool _hasWarnedNavMesh;      ///< NavMesh の経路が引けない警告を出したか（毎ステップ出さないため）
        private bool _isReturnPending;       ///< 波に乗ったあと、まだ初期位置へ戻り終えていないか（戻り終えた通知を 1 回だけ出すため）
        private bool _hasWarnedOverlapOverflow;  ///< 上面の Collider が入れ物の数を超えた警告を出したか（毎ステップ出さないため）

        private WaveRiderCollision _collision;  ///< 当たり判定まわりの計算（押し戻し、回転の可否、乗っている物の検出、壁の手前で止める移動）
        private readonly HashSet<Rigidbody> _passengers = new HashSet<Rigidbody>();  ///< 直前の物理ステップで上に乗っていた物

        /// @brief 波に乗っているか
        public bool IsRiding => _currentEmitter != null;

        /// @brief スイッチで起動したことにする。スイッチの On Activated につなぐ
        [ShowInEventMenu]
        public void Activate()
        {
            SetActive(true);
        }

        /// @brief スイッチが止まったことにする。スイッチの On Deactivated につなぐ。波に乗っていれば降りて、「波が無くなったとき」の動きになる
        [ShowInEventMenu]
        public void Deactivate()
        {
            SetActive(false);
        }

        /// @brief 起動と停止を入れ替える
        [ShowInEventMenu]
        public void Toggle()
        {
            SetActive(!IsActivated());
        }

        /// 起動の状態を決める。Awake より前（別の物の Awake からスイッチのイベントが来たときなど）でも、決めた状態を残す
        private void SetActive(bool isActive)
        {
            _isActive = isActive;
            _isActiveSet = true;
        }

        /// 今起動しているか。Awake より前で、まだ決まっていなければ「始めから起動しておく」の値
        private bool IsActivated()
        {
            return _isActiveSet ? _isActive : _isActiveOnStart;
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
            _lineDeviation = 0.0f;
            _followLag = 0.0f;
        }

        /// @brief 「初期位置へ戻る」で戻る先を変える。コードで生成したあとに位置を動かしたときや、置き直したときに使う
        /// 初期位置は Awake の位置で決まるため、Instantiate のあとに transform を動かすと、戻る先が生成時の位置のままになる
        public void SetInitialPosition(Vector3 position)
        {
            _initialPosition = position;
            ResetNavPlan();   // 引いてあった経路は古い目標向きなので捨てる
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
            _isActive = IsActivated();   // Awake より前に Activate などが呼ばれていれば、その状態を残す
            _isActiveSet = true;
            _reaction = GetComponent<WaveReaction>();
            _body = GetComponent<Rigidbody>();
            _body.isKinematic = true;
            _body.interpolation = RigidbodyInterpolation.Interpolate;
            _initialPosition = _body.position;
            _initialRotation = _body.rotation;
            _collision = new WaveRiderCollision(_body, transform);
        }

        // 実行中に子の Collider が増えたり減ったりしたら、当たり判定に使う Collider を取り直す
        private void OnTransformChildrenChanged()
        {
            _collision?.RefreshColliders();
        }

        // 再び有効になったとき、無効の前の慣性や、乗っていた波、引いてあった経路を持ち越さない
        private void OnEnable()
        {
            _velocity = Vector3.zero;
            _currentEmitter = null;
            _attachOffset = Vector3.zero;
            _lineDeviation = 0.0f;
            _followLag = 0.0f;
            ResetNavPlan();
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
                ResetBoardingAtStart(next);
            }
            else
            {
                Vector3 desired = Ride(emitter, deltaTime, out Vector3 linePoint);
                next = LimitFollowStep(current, desired, deltaTime);
                _followLag = Vector3.Distance(desired, next);
                _isReturnPending = true;
                ResetNavPlan();   // 乗っている間に動いた分、戻る経路は引き直す
                _velocity = (next - current) / deltaTime;
                // 反射の曲がり角では回りながら進むので、回ったあとの向きで押し戻す。波を出しているもの（砲台など）は押し戻さない
                next = _collision.PushOutOfWalls(next, _turnDelta, emitter.transform);
                // 乗った時点のずれや押し戻しで線から離れていても、途切れた直後に同じ所から乗り直せるよう、離れていた距離を覚える
                _lineDeviation = Vector3.Distance(next, linePoint);
            }

            DetectPassengers();
            CarryPassengers(current, next);
            _body.MovePosition(next);
        }

        /// @brief 引いてあった戻る経路を捨て、次のステップですぐ引き直せるようにする
        private void ResetNavPlan()
        {
            _navReturn.Reset();
            _nextNavPlanTime = 0.0f;
        }

        /// @brief 上に乗っている物を集める（運ばない設定なら空にする）。上面の Collider が多すぎたら、警告を 1 回出す
        /// 計算量: O(C + K)（C は自分の Collider の数、K は上面で重なった Collider の数）
        private void DetectPassengers()
        {
            if (!_isCarryingPassengers)
            {
                _passengers.Clear();
                return;
            }

            bool isOverflow = _collision.DetectPassengers(_passengers);
            if (isOverflow && !_hasWarnedOverlapOverflow)
            {
                _hasWarnedOverlapOverflow = true;
                Debug.LogWarning("WaveRider: 上面の Collider が多すぎて、乗っている物を取りこぼす恐れがあります（最大 " + WaveRiderCollision.OverlapBufferSize + " 個）", this);
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
                // 乗り直すときは、最後に乗っていた間の線からのずれ（乗った時点のずれ、押し戻し）の分も許す。その場で止まっていれば必ず乗り直せる
                float limit = _hasBoarded ? _rejoinDistance + _lineDeviation : _boardDistance;
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
                _followLag = 0.0f;
                // 終点の近くで乗ったとき、次のステップで端まで一気に戻されないよう、先に動ける範囲に収める（収めた分は下のずれに入り、少しずつ寄る）
                GetTravelLimits(corners, out float startLimit, out float endLimit);
                _distance = WaveRiderTravel.ClampOnBoard(distance, _direction, startLimit, endLimit);
                // 線の上へ飛ばず、今の位置から少しずつ寄るため、乗った時点のずれを覚える
                Vector3 boardPoint = WaveLinePath.GetPointAt(corners, _distance, out Vector3 _);
                _attachOffset = current - (boardPoint + Vector3.up * _heightOffset);
            }
            else if (_distance > WaveLinePath.GetLength(corners) + BeamEndTolerance)
            {
                return null;
            }
            else if (_dropLagDistance > 0.0f && _followLag > _dropLagDistance)
            {
                // 線が大きく離れたので降りる。遠く離れた所から乗り直さないよう、離れていた距離は許さない（「乗り直せる距離」だけで判定する）
                _lineDeviation = 0.0f;
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
            if (Quaternion.Angle(_body.rotation, _initialRotation) <= 0.0f)
            {
                return;   // すでに初期の向きなら、壁との重なりを調べるまでもない（置いてあるだけの間の毎ステップの負荷を避ける）
            }
            // 位置の移動は壁の手前で止めるが、回転は別なので、回ると壁に食い込むなら、このステップは回さない（位置が動いて空いたら、また回る）
            TurnTowards(_initialRotation, deltaTime, true);
        }

        /// @brief target の向きへ、1 ステップで回れる分だけ回り、その分を _turnDelta に残す（上に乗った物も、この分だけ自分の中心まわりに回す）
        /// @param isCheckingWalls true なら、回ると壁などに横から食い込むとき、このステップは回さない
        /// 計算量: isCheckingWalls が true なら O(C × K)（C は自分の Collider の数、K は重なった Collider の数）、false なら O(1)
        private void TurnTowards(Quaternion target, float deltaTime, bool isCheckingWalls)
        {
            Quaternion rotated = Quaternion.RotateTowards(_body.rotation, target, TurnDegreesPerSecond * deltaTime);
            Quaternion delta = rotated * Quaternion.Inverse(_body.rotation);
            if (isCheckingWalls && _collision.IsRotationBlocked(delta, _passengers))
            {
                return;
            }
            _turnDelta = delta;
            _body.MoveRotation(rotated);
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

        /// @brief 波が無い間に初期位置にいれば、次は最初に乗るときと同じ「最初に乗れる距離」で乗れるようにする
        /// 初期位置が線から「乗り直せる距離」より離れていると、戻ったあと二度と乗れなくなるため。向きも最初と同じく終点へ向かう
        private void ResetBoardingAtStart(Vector3 position)
        {
            if ((position - _initialPosition).sqrMagnitude <= ReturnedDistance * ReturnedDistance)
            {
                _hasBoarded = false;
            }
        }

        /// @brief 初期位置へまっすぐ 1 ステップ分戻る。途中の壁は通り抜けず、その手前で止まる（慣性と同じ）
        private Vector3 ReturnStraight(Vector3 current, float deltaTime)
        {
            Vector3 target = Vector3.MoveTowards(current, _initialPosition, _returnSpeed * deltaTime);
            return _collision.SweepMove(current, target - current, _passengers, out bool _);
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
            Vector3 next = _collision.SweepMove(current, target - current, _passengers, out bool isBlocked);
            if (isBlocked)
            {
                _navReturn.Reset();
            }
            return next;
        }

        /// @brief 慣性で 1 ステップ分進める。壁などに当たる手前で止まる（当たったら速さは 0 になる）
        private Vector3 MoveByInertia(Vector3 current, float deltaTime)
        {
            Vector3 next = _collision.SweepMove(current, _velocity * deltaTime, _passengers, out bool isBlocked);
            if (isBlocked)
            {
                _velocity = Vector3.zero;
            }
            return next;
        }

        /// @brief 位置から高さのずれを引いた、線と比べる位置を返す（乗り直しの最寄りの点を、ずれに引きずられずに探すため）
        private Vector3 GetLinePosition(Vector3 position)
        {
            return position - Vector3.up * _heightOffset;
        }

        /// @brief 線の上で動ける道のりの範囲を求める（WaveRiderTravel の図を参照）
        /// @param startLimit 折り返すときの始まり側の端。endLimit を超えない
        /// @param endLimit 終点側の端
        /// 計算量: O(N + C)（N は線の点の数、C は自分の Collider の数）
        private void GetTravelLimits(IReadOnlyList<Vector3> corners, out float startLimit, out float endLimit)
        {
            // 波は中心を通るので、終点（壁など）に中心まで行くと半分食い込む。先端が終点に着く所で止める
            WaveLinePath.GetPointAt(corners, float.MaxValue, out Vector3 endDirection);
            endLimit = Mathf.Max(0.0f, WaveLinePath.GetLength(corners) - _collision.GetFrontExtent(endDirection));

            // 始まりも終点と同じく、後ろ側の広がり分だけ手前で折り返す（砲台は押し戻しの対象外なので、食い込まないため）
            WaveLinePath.GetPointAt(corners, 0.0f, out Vector3 startDirection);
            startLimit = Mathf.Min(_collision.GetFrontExtent(-startDirection), endLimit);
        }

        /// @brief 線の上の道のりを 1 ステップ分進める。終点では止まる（折り返すなら向きを変える）
        /// 始まりでも終点と同じく、広がり分だけ手前で折り返す
        private void AdvanceDistance(IReadOnlyList<Vector3> corners, float deltaTime)
        {
            GetTravelLimits(corners, out float startLimit, out float endLimit);
            _distance = WaveRiderTravel.Advance(_distance, ref _direction, _carrySpeed * deltaTime, startLimit, endLimit,
                _endMode == WaveRiderEndMode.PingPong);
        }

        /// @brief 1 ステップ分、線に沿って進めて、次の位置を返す（回転も合わせる）
        /// @param linePoint 線の上の点（高さのずれを含む）。ずれが無ければ、ここへ動く
        private Vector3 Ride(WaveEmitter emitter, float deltaTime, out Vector3 linePoint)
        {
            var corners = emitter.BeamCorners;
            AdvanceDistance(corners, deltaTime);

            Vector3 point = WaveLinePath.GetPointAt(corners, _distance, out Vector3 travelDirection);
            FaceTravelDirection(travelDirection * _direction, deltaTime);
            _attachOffset = Vector3.MoveTowards(_attachOffset, Vector3.zero, _approachSpeed * deltaTime);
            linePoint = point + Vector3.up * _heightOffset;
            return linePoint + _attachOffset;
        }

        /// @brief 乗っている間の 1 ステップの移動を、線に付いていく速さの上限までに抑える
        /// 線の上の道のりだけを保つので、線そのものが動く（砲台が回る、波が曲がる）と、その分だけ一気に動いてしまう
        /// 抑えた分は覚えず、次のステップでまた線の上の点を目指すので、線が止まれば上限の速さで追いつく
        private Vector3 LimitFollowStep(Vector3 current, Vector3 next, float deltaTime)
        {
            float maxSpeed = Mathf.Max(_maxFollowSpeed, _carrySpeed + _approachSpeed);
            return Vector3.MoveTowards(current, next, maxSpeed * deltaTime);
        }

        /// @brief 進む向き（Y 軸まわり）へ、少しずつ回転を合わせる
        /// 乗っている間は、回って食い込んだ分を押し戻しで離すので、壁との重なりで回るのを止めない
        private void FaceTravelDirection(Vector3 travelDirection, float deltaTime)
        {
            travelDirection.y = 0.0f;
            if (!_isFacingTravelDirection || travelDirection.sqrMagnitude < MinDirectionSqrMagnitude)
            {
                return;
            }
            TurnTowards(Quaternion.LookRotation(travelDirection.normalized, Vector3.up), deltaTime, false);
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
