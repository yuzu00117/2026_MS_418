using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aura.Wave
{
    /// @brief 手から波を出す。単発（Pulse）と連続（Beam）を Inspector の Fire Mode で切り替え、
    /// 壁などで跳ね返らせるかを Reflection Mode で決める
    /// 入力には依存しない。SetTriggerHeld などの公開の操作を、入力の部品やゲームの側から呼ぶ
    public sealed class WaveEmitter : MonoBehaviour
    {
        private const int MaxReflectionLimit = 16;                  ///< Max Reflections に決められる最大の回数
        private const float MinReflectionEnergyMultiplier = 0.05f;  ///< Reflection Energy Multiplier に決められる最小の割合
        private const float PreviewLineWidth = 0.015f;              ///< 予測線の太さ（m）
        private const float PreviewAlpha = 0.5f;                    ///< 予測線の始めの不透明度に掛ける割合。終わりに向かってさらに薄くなる
        private const float MinTargetSqrDistance = 0.0001f;         ///< 手の先から狙う先までの距離の 2 乗（m^2）がこれより小さければ、向きが決まらないとみなす

        [Header("波の種類と撃ち方")]
        [Tooltip("撃てる波の種類（WaveType のアセット）。切り替えると上から順に入れ替わり、最後の次は最初に戻る。空だと撃てない")]
        [SerializeField] private WaveType[] _waveTypes;                    ///< 撃てる波の種類。上から順に切り替える

        [Tooltip("今の波の種類の番号（Wave Types の何番目か。0 から数える）。最初に持っている種類になる\n" +
                 "実行中に変えると、その種類に切り替わる。範囲の外なら最後の種類になる")]
        [SerializeField, Min(0)] private int _typeIndex;                   ///< 今の波の種類の番号

        [Tooltip("オフにすると、波の種類を切り替えられない（Q / E などの切り替えが効かない）。決まった波しか撃てない場面に使う\n" +
                 "Type Index を Inspector で直接変えたときは、オフでも切り替わる")]
        [SerializeField] private bool _canSwitchType = true;               ///< 波の種類を切り替えられるか

        [Tooltip("撃ち方。実行中に変えてもよい\n" +
                 "Pulse: 押した瞬間に、波の塊を 1 発飛ばす（設定は「単発」の欄）\n" +
                 "Beam: 押している間、ビームを当て続ける（設定は「連続」の欄）")]
        [SerializeField] private WaveFireMode _fireMode = WaveFireMode.Pulse;  ///< 撃ち方。実行中に変えてもよい

        [Header("狙い")]
        [Tooltip("波が出る位置（手の先に置いた空の GameObject など）。Aim Origin が空のときは、この正面（青い矢印の向き）に撃つ\n" +
                 "空なら、この GameObject の位置から出る")]
        [SerializeField] private Transform _muzzle;                        ///< 波が出る位置（手の先）。空なら自分

        [Tooltip("狙いを決める視点（ふつうはカメラ）。視点の正面で最初に当たる位置へ向けて Muzzle から撃つので、画面の中心（照準）に飛ぶ\n" +
                 "空なら、Muzzle の正面に撃つ")]
        [SerializeField] private Transform _aimOrigin;                     ///< 狙いを決める視点（カメラ）。空なら Muzzle の正面に撃つ

        [Tooltip("この GameObject とその子のコライダーには、波が当たらない（自分の体に当たらないようにする）。ふつうはプレイヤーのいちばん上の GameObject を入れる\n" +
                 "跳ね返って戻ってきた波は、ここの下のコライダーに当たると止まる（受け手が付いていても波は渡さない）\n" +
                 "空なら、この GameObject のいちばん上の親を使う")]
        [SerializeField] private Transform _ignoreRoot;                    ///< この下のコライダーに当たらない（自分の体。跳ね返って戻った波はここで止まる）。空なら transform.root

        [Tooltip("波が当たるレイヤー。チェックを外したレイヤーの物は、波が素通りする（受け手にも届かない）")]
        [SerializeField] private LayerMask _hitMask = ~0;                  ///< 波が当たるレイヤー

        [Tooltip("Is Trigger をオンにしたコライダーに、波が当たるか\n" +
                 "Use Global: Project Settings > Physics の Queries Hit Triggers に従う\n" +
                 "Ignore: 当たらない（素通りする）\n" +
                 "Collide: 当たる。受け手のコライダーをトリガーにするときは、これにする\n" +
                 "当たったトリガーでは、WaveSurface の Response を Reflect にしない限り跳ね返らずに止まる")]
        [SerializeField] private QueryTriggerInteraction _triggerInteraction = QueryTriggerInteraction.Ignore;  ///< トリガーに当たるか

        [Header("反射（単発とビームの両方）")]
        [Tooltip("波が物に当たったときに跳ね返るか。実行中に変えてもよい（ビームはすぐ、単発は次に撃つ波から効く）\n" +
                 "None: 跳ね返らない。最初に当たった物で止まる（反射を入れる前と同じ動き。比べる用）\n" +
                 "All Surfaces: どの物でも跳ね返る。WaveSurface の Response を Absorb にした物と、トリガーのコライダーでは止まる\n" +
                 "Reflectors Only: 反射板（WaveSurface の Response を Reflect にした物）でだけ跳ね返り、ほかの物では止まる\n" +
                 "受け手に当たったときも、波を渡したうえで同じ決まりで跳ね返るか止まる（1 発で複数の受け手を起動できる）\n" +
                 "跳ね返るかは当たった瞬間（受け手に渡す前）に決まるので、受け手の On Activated などで鏡を切り替えても、次の波から効く")]
        [SerializeField] private WaveReflectionMode _reflectionMode = WaveReflectionMode.None;  ///< 跳ね返るかどうかの決め方。None なら反射なし

        [Tooltip("1 発（ビームは 1 本）が跳ね返る回数の上限（回。1〜16）。この回数だけ跳ね返った後は、次に当たった物で止まる（受け手なら波は届く）\n" +
                 "例: 1 なら、1 回跳ね返った先の物まで届く。Reflection Mode が None のときは使わない\n" +
                 "同じ受け手には、1 発（ビームは 1 フレーム）で 1 回だけ届く。跳ね返って同じ受け手にまた当たっても、エネルギーは増えない")]
        [SerializeField, Range(1, MaxReflectionLimit)] private int _maxReflections = 3;  ///< 跳ね返る回数の上限

        [Tooltip("1 回跳ね返るごとに、波のエネルギー（Pulse Energy、Beam Energy Per Second）に掛ける割合（0.05〜1）。1 なら減らない\n" +
                 "例: 0.5 なら 1 回で半分、2 回で 4 分の 1。跳ね返った先の受け手を、単発 1 発では起動させたくないときに下げる\n" +
                 "ビームを当てている間は受け手のエネルギーが減らないので、ビームでは下げても起動までの時間が延びるだけ")]
        [SerializeField, Range(MinReflectionEnergyMultiplier, 1.0f)] private float _reflectionEnergyMultiplier = 1.0f;  ///< 跳ね返るたびにエネルギーに掛ける割合

        [Tooltip("オンにすると、撃つ前から、跳ね返りを含めた波の通り道を細い線（予測線）で見せる。実行中に変えてもよい\n" +
                 "Reflection Mode が None でないときだけ使う（None のときは、この項目は Inspector に出ない）\n" +
                 "今の撃ち方（Fire Mode）の届く距離と当たり判定の太さで求める。ビームを出している間は、ビームと重なるので出さない\n" +
                 "予測線は見せるだけで、受け手に波は届かない")]
        [SerializeField] private bool _isReflectionPreviewEnabled = true;  ///< 跳ね返りを含めた通り道の予測線を出すか（Reflection Mode が None でないときだけ）

        [Header("単発（Fire Mode が Pulse のとき）")]
        [Tooltip("1 発が当たったときに、受け手へ渡すエネルギー。受け手は Required Energy まで溜まると起動する\n" +
                 "例: どちらも既定の 1 なら 1 発で起動する\n" +
                 "受け手のエネルギーは撃つ間にも Decay Per Second で減るので、1 発で足りないときは「Required Energy ÷ これ」発より多く要る\n" +
                 "跳ね返った後は、跳ね返るたびに Reflection Energy Multiplier を掛けた量になる")]
        [SerializeField, Min(0.0f)] private float _pulseEnergy = 1.0f;     ///< 1 発で渡すエネルギー

        [Tooltip("波の塊が飛ぶ速さ（m/s）。大きいほど、撃ってからすぐに当たる。0.1 以上")]
        [SerializeField, Min(0.1f)] private float _pulseSpeed = 30.0f;     ///< 進む速さ（m/s）

        [Tooltip("波の塊が届く距離（m）。跳ね返った後の道のりも合わせて数える。これだけ進むか、跳ね返らない物に当たると消える。0.1 以上")]
        [SerializeField, Min(0.1f)] private float _pulseRange = 40.0f;     ///< 届く距離（m）。跳ね返った後の道のりも含む

        [Tooltip("当たり判定の太さ（球の半径、m）。大きいほど、狙いが少しずれても当たる。0 なら太さのない線で判定する")]
        [SerializeField, Min(0.0f)] private float _pulseRadius = 0.15f;    ///< 当たり判定の半径（m）

        [Tooltip("見た目の波の塊の長さ（m）。当たり判定には影響しない。0.1 以上")]
        [SerializeField, Min(0.1f)] private float _pulseLength = 1.2f;     ///< 見た目の波の塊の長さ（m）

        [Tooltip("1 発撃ってから、次を撃てるまでの時間（秒）。連打しても、この間隔より速くは撃てない。0 なら制限なし")]
        [SerializeField, Min(0.0f)] private float _pulseInterval = 0.25f;  ///< 次を撃てるまでの時間（秒）

        [Tooltip("オンにすると、押し続けている間 Pulse Interval ごとに撃ち続ける（連射）。オフなら押すたびに 1 発")]
        [SerializeField] private bool _isPulseAutoFire;                    ///< 押し続けている間、間隔ごとに撃ち続けるか

        [Header("連続（Fire Mode が Beam のとき）")]
        [Tooltip("当て続けたとき、1 秒あたりに受け手へ渡すエネルギー。受け手は Required Energy まで溜まると起動する\n" +
                 "例: どちらも既定の 1 なら 1 秒当てると起動する。これを 2 にすると 0.5 秒で起動する\n" +
                 "跳ね返った後は、跳ね返るたびに Reflection Energy Multiplier を掛けた量になる")]
        [SerializeField, Min(0.0f)] private float _beamEnergyPerSecond = 1.0f;  ///< 1 秒あたりに渡すエネルギー

        [Tooltip("ビームが届く距離（m）。跳ね返った後の道のりも合わせて数える。0.1 以上")]
        [SerializeField, Min(0.1f)] private float _beamRange = 25.0f;      ///< 届く距離（m）。跳ね返った後の道のりも含む

        [Tooltip("当たり判定の太さ（球の半径、m）。大きいほど、狙いが少しずれても当たる。0 なら太さのない線で判定する")]
        [SerializeField, Min(0.0f)] private float _beamRadius = 0.1f;      ///< 当たり判定の半径（m）

        [Header("見た目")]
        [Tooltip("波の線（単発、ビーム、予測線）のマテリアル。空なら既定のマテリアルを使い、WaveType の Color で色を付ける\n" +
                 "自分で用意するなら、頂点カラーを使うシェーダーにすると WaveType の色が付く（予測線を薄く見せるのにも、頂点カラーのアルファを使う）\n" +
                 "実行中に変えると単発にだけ反映され、ビームと予測線には反映されない")]
        [SerializeField] private Material _lineMaterial;                   ///< 波の線のマテリアル。空なら頂点カラーの既定のもの

        private readonly List<Vector3> _beamCorners = new List<Vector3>();     ///< ビームの線を描く折れ線の点。毎フレーム使い回す
        private readonly List<Vector3> _previewCorners = new List<Vector3>();  ///< 予測線を描く折れ線の点。毎フレーム使い回す
        private WaveLine _beamLine;
        private WaveLine _previewLine;                      ///< 跳ね返りを含めた通り道の予測線
        private WaveTracer _beamTracer;                     ///< ビームの通り道をたどる。BeamTracer から使う
        private WaveTracer _previewTracer;                  ///< 予測線の通り道をたどる。PreviewTracer から使う
        private Action<WaveHit, bool> _deliveredCallback;   ///< OnWaveDelivered を指すデリゲート。DeliveredCallback から使う
        private WaveFireMode _appliedFireMode;              ///< 最後に反映した撃ち方。Inspector での変更に気付くために持つ
        private WaveReflectionMode _appliedReflectionMode;  ///< 最後に反映した反射の決め方。Inspector での変更に気付くために持つ
        private int _appliedTypeIndex;                      ///< 最後に反映した種類の番号。Inspector での変更に気付くために持つ
        private float _nextPulseTime;                       ///< 次に撃てる時刻
        private bool _isTriggerHeld;                        ///< 引き金を引いているか
        private bool _isBeamFiring;                         ///< ビームを出しているか

        /// @brief 波の種類が変わった後に起きる
        public event Action<WaveType> TypeChanged;

        /// @brief 撃ち方が変わった後に起きる
        public event Action<WaveFireMode> FireModeChanged;

        /// @brief 跳ね返るかどうかの決め方が変わった後に起きる
        public event Action<WaveReflectionMode> ReflectionModeChanged;

        /// @brief 単発の波を撃った後に起きる
        public event Action<WaveType> PulseFired;

        /// @brief ビームを出し始めた後に起きる
        public event Action<WaveType> BeamStarted;

        /// @brief ビームを止めた後に起きる
        public event Action BeamStopped;

        /// @brief 波が物に当たるたびに起きる。跳ね返ると、単発 1 発でも、ビームの 1 フレームでも何度も起きる
        /// 同じ受け手にまた当たったとき（1 発、ビームは 1 フレームの中で）と、跳ね返って撃った本人に当たったときは起きない
        /// bool は受け手が受け付けたか
        public event Action<WaveHit, bool> WaveDelivered;

        /// @brief 今の波の種類。種類が 1 つもなければ null
        public WaveType CurrentType => HasTypes ? _waveTypes[_typeIndex] : null;

        /// @brief 今の波の種類の番号
        public int TypeIndex => _typeIndex;

        /// @brief 撃てる波の種類の数
        public int TypeCount => _waveTypes != null ? _waveTypes.Length : 0;

        /// @brief 波の種類を切り替えられるか
        public bool CanSwitchType
        {
            get => _canSwitchType;
            set => _canSwitchType = value;
        }

        /// @brief 撃ち方。変えるとビームは止まる
        public WaveFireMode FireMode
        {
            get => _fireMode;
            set
            {
                _fireMode = value;
                ApplyFireMode();
            }
        }

        /// @brief 波が物に当たったときに跳ね返るかどうかの決め方
        /// 変えると、ビームは次のフレームから、単発は次に撃つ波から効く（飛んでいる単発は撃ったときの設定のまま）
        public WaveReflectionMode ReflectionMode
        {
            get => _reflectionMode;
            set
            {
                _reflectionMode = value;
                ApplyReflectionMode();
            }
        }

        /// @brief 1 発（ビームは 1 本）が跳ね返る回数の上限（1〜16）
        public int MaxReflections
        {
            get => _maxReflections;
            set => _maxReflections = Mathf.Clamp(value, 1, MaxReflectionLimit);
        }

        /// @brief 1 回跳ね返るごとに、波のエネルギーに掛ける割合（0.05〜1）
        public float ReflectionEnergyMultiplier
        {
            get => _reflectionEnergyMultiplier;
            set => _reflectionEnergyMultiplier = Mathf.Clamp(value, MinReflectionEnergyMultiplier, 1.0f);
        }

        /// @brief 跳ね返りを含めた通り道の予測線を出すか。Reflection Mode が None のときは、true でも出ない
        public bool IsReflectionPreviewEnabled
        {
            get => _isReflectionPreviewEnabled;
            set => _isReflectionPreviewEnabled = value;
        }

        /// @brief 引き金を引いているか
        public bool IsTriggerHeld => _isTriggerHeld;

        /// @brief ビームを出しているか
        public bool IsBeamFiring => _isBeamFiring;

        /// @brief 波が出る位置（手の先）。一人称と三人称で手が変わるときに差し替える
        public Transform Muzzle
        {
            get => _muzzle != null ? _muzzle : transform;
            set => _muzzle = value;
        }

        /// @brief 狙いを決める視点（カメラ）。null なら Muzzle の正面に撃つ
        public Transform AimOrigin
        {
            get => _aimOrigin;
            set => _aimOrigin = value;
        }

        private bool HasTypes => _waveTypes != null && _waveTypes.Length > 0;

        private Transform IgnoreRoot => _ignoreRoot != null ? _ignoreRoot : transform.root;

        // 次の 3 つは、使うときに作る。Awake を通らないとき（一度もアクティブになっていない GameObject から撃つ、
        // 実行中にスクリプトを書き換えて作り直された）も、波を受け手に渡して WaveDelivered を起こせるようにする
        // デリゲートは撃つたびに作らないよう、1 つを使い回す
        private Action<WaveHit, bool> DeliveredCallback => _deliveredCallback ??= OnWaveDelivered;

        private WaveTracer BeamTracer => _beamTracer ??= new WaveTracer(DeliveredCallback);

        private WaveTracer PreviewTracer => _previewTracer ??= new WaveTracer(null);

        //============================================================
        // 公開の操作
        //============================================================

        /// @brief 引き金の状態を伝える。入力の部品から毎フレーム呼んでよい
        /// 押した瞬間に単発を撃ち、押している間ビームを出し、離すとビームを止める
        /// @param isHeld 引き金を引いているか
        public void SetTriggerHeld(bool isHeld)
        {
            bool isPressed = isHeld && !_isTriggerHeld;
            _isTriggerHeld = isHeld;

            if (_fireMode == WaveFireMode.Pulse)
            {
                if (isPressed || (isHeld && _isPulseAutoFire))
                {
                    TryFirePulse();
                }
                return;
            }

            if (isHeld)
            {
                StartBeam();
            }
            else
            {
                StopBeam();
            }
        }

        /// @brief 撃ち方に関係なく、単発の波を 1 発撃つ
        /// @return 撃てたら true。間隔が空いていないか、種類がなければ false
        public bool TryFirePulse()
        {
            WaveType type = CurrentType;
            if (type == null || Time.time < _nextPulseTime)
            {
                return false;
            }
            _nextPulseTime = Time.time + _pulseInterval;

            Transform muzzle = Muzzle;
            var settings = new WavePulseSettings
            {
                Trace = CreateTraceSettings(type, WaveFireMode.Pulse, _pulseRadius, _pulseRange),
                Origin = muzzle.position,
                Direction = GetAimDirection(_pulseRange),
                Energy = _pulseEnergy,
                Up = muzzle.up,
                Speed = _pulseSpeed,
                Length = _pulseLength,
                LineMaterial = _lineMaterial,
                DeliveredCallback = DeliveredCallback,
            };
            WavePulse.Launch(settings);
            PulseFired?.Invoke(type);
            return true;
        }

        /// @brief 次の波の種類に切り替える。最後の次は最初に戻る
        public void SelectNextType()
        {
            if (HasTypes)
            {
                SelectType((_typeIndex + 1) % _waveTypes.Length);
            }
        }

        /// @brief 前の波の種類に切り替える。最初の前は最後に戻る
        public void SelectPreviousType()
        {
            if (HasTypes)
            {
                SelectType((_typeIndex - 1 + _waveTypes.Length) % _waveTypes.Length);
            }
        }

        /// @brief 番号を指定して波の種類を切り替える
        /// @param index 種類の番号
        /// @return 切り替えたら true。切り替えられないか、範囲の外なら false
        public bool SelectType(int index)
        {
            if (!_canSwitchType || !HasTypes || index < 0 || index >= _waveTypes.Length)
            {
                return false;
            }
            _typeIndex = index;
            ApplyTypeIndex();
            return true;
        }

        /// @brief アセットを指定して波の種類を切り替える
        /// @param type 波の種類。_waveTypes に入っているもの
        /// @return 切り替えたら true
        public bool SelectType(WaveType type)
        {
            return HasTypes && SelectType(Array.IndexOf(_waveTypes, type));
        }

        //============================================================
        // Unity のメッセージ
        //============================================================

        private void Awake()
        {
            _beamLine = WaveLine.Create("WaveBeam", transform, _lineMaterial);
            _beamLine.Hide();
            _previewLine = WaveLine.Create("WaveReflectionPreview", transform, _lineMaterial);
            _previewLine.Hide();
            _appliedFireMode = _fireMode;
            _appliedReflectionMode = _reflectionMode;
            _typeIndex = HasTypes ? Mathf.Clamp(_typeIndex, 0, _waveTypes.Length - 1) : 0;
            _appliedTypeIndex = _typeIndex;
        }

        private void OnDisable()
        {
            _isTriggerHeld = false;
            StopBeam();
            if (_previewLine != null)
            {
                _previewLine.Hide();
            }
        }

        private void Update()
        {
            // Inspector で実行中に変えた値を反映する
            if (_fireMode != _appliedFireMode)
            {
                ApplyFireMode();
            }
            if (_reflectionMode != _appliedReflectionMode)
            {
                ApplyReflectionMode();
            }
            if (_typeIndex != _appliedTypeIndex)
            {
                _typeIndex = HasTypes ? Mathf.Clamp(_typeIndex, 0, _waveTypes.Length - 1) : 0;
                ApplyTypeIndex();
            }
        }

        private void LateUpdate()
        {
            // カメラと手が動いた後に、ビームと予測線の判定と見た目を合わせる
            if (_isBeamFiring)
            {
                UpdateBeam(Time.deltaTime);
                // 受け手の処理でこの部品が無効にされたときは、OnDisable で消した予測線を出し直さない
                if (!isActiveAndEnabled)
                {
                    return;
                }
            }
            UpdateReflectionPreview();
        }

        //============================================================
        // 内部の処理
        //============================================================

        private void StartBeam()
        {
            if (_isBeamFiring || CurrentType == null)
            {
                return;
            }
            _isBeamFiring = true;
            BeamStarted?.Invoke(CurrentType);
        }

        private void StopBeam()
        {
            if (!_isBeamFiring)
            {
                return;
            }
            _isBeamFiring = false;
            if (_beamLine != null)
            {
                _beamLine.Hide();
            }
            BeamStopped?.Invoke();
        }

        private void UpdateBeam(float deltaTime)
        {
            WaveType type = CurrentType;
            if (type == null)
            {
                StopBeam();
                return;
            }

            Transform muzzle = Muzzle;
            Vector3 origin = muzzle.position;
            Vector3 direction = GetAimDirection(_beamRange);
            WaveTraceSettings settings = CreateTraceSettings(type, WaveFireMode.Beam, _beamRadius, _beamRange);
            WaveTracer tracer = BeamTracer;
            tracer.Begin(settings, origin, direction, _beamEnergyPerSecond * deltaTime);
            tracer.Advance(_beamRange);

            // 受け手の処理でビームが止められた（この部品が無効にされたなど）ときは、線を出し直さない
            if (!_isBeamFiring)
            {
                return;
            }

            tracer.GetCorners(0.0f, tracer.Travelled, true, _beamCorners);
            float phase = Time.time * type.ScrollSpeed;
            _beamLine.Draw(_beamCorners, muzzle.up, type, phase, isPacket: false);
        }

        /// @brief 予測線を出すときは、今の狙いから跳ね返りを含めた通り道をたどって描く。出さないときは消す
        private void UpdateReflectionPreview()
        {
            if (_previewLine == null)
            {
                return;
            }
            // ビームを出している間は、ビームそのものが通り道を見せるので出さない
            bool isVisible = _isReflectionPreviewEnabled && _reflectionMode != WaveReflectionMode.None && !_isBeamFiring;
            WaveType type = isVisible ? CurrentType : null;
            if (type == null)
            {
                _previewLine.Hide();
                return;
            }

            // 今の撃ち方で撃ったときと同じ距離と太さでたどる
            bool isPulse = _fireMode == WaveFireMode.Pulse;
            float range = isPulse ? _pulseRange : _beamRange;
            float radius = isPulse ? _pulseRadius : _beamRadius;
            WaveTraceSettings settings = CreateTraceSettings(type, _fireMode, radius, range);
            settings.IsPreview = true;
            WaveTracer tracer = PreviewTracer;
            tracer.Begin(settings, Muzzle.position, GetAimDirection(range), 0.0f);
            tracer.Advance(range);
            tracer.GetCorners(0.0f, tracer.Travelled, true, _previewCorners);

            Color color = type.Color;
            color.a *= PreviewAlpha;
            _previewLine.DrawStraight(_previewCorners, color, PreviewLineWidth);
        }

        /// @brief 通り道をたどる設定を、今の Inspector の値から作る
        private WaveTraceSettings CreateTraceSettings(WaveType type, WaveFireMode fireMode, float radius, float range)
        {
            return new WaveTraceSettings
            {
                Type = type,
                FireMode = fireMode,
                Source = gameObject,
                Radius = radius,
                Range = range,
                HitMask = _hitMask,
                TriggerInteraction = _triggerInteraction,
                IgnoreRoot = IgnoreRoot,
                ReflectionMode = _reflectionMode,
                MaxReflections = _maxReflections,
                ReflectionEnergyMultiplier = _reflectionEnergyMultiplier,
            };
        }

        /// @brief 波を出す向きを決める。視点があれば、視点の正面で当たる位置へ Muzzle から向ける
        private Vector3 GetAimDirection(float range)
        {
            Transform muzzle = Muzzle;
            if (_aimOrigin == null)
            {
                return muzzle.forward;
            }

            Vector3 aimPosition = _aimOrigin.position;
            Vector3 aimForward = _aimOrigin.forward;
            float aimRange = range + Vector3.Distance(aimPosition, muzzle.position);
            Vector3 target = WavePhysics.TryCast(aimPosition, aimForward, 0.0f, aimRange, _hitMask,
                _triggerInteraction, IgnoreRoot, out RaycastHit hit)
                ? hit.point
                : aimPosition + aimForward * aimRange;

            Vector3 toTarget = target - muzzle.position;
            // 狙う先が手より後ろ（壁に張り付いているときなど）なら、視点の向きにそのまま撃つ
            if (toTarget.sqrMagnitude < MinTargetSqrDistance || Vector3.Dot(toTarget, aimForward) <= 0.0f)
            {
                return aimForward;
            }
            return toTarget.normalized;
        }

        private void ApplyFireMode()
        {
            if (_fireMode == _appliedFireMode)
            {
                return;
            }
            _appliedFireMode = _fireMode;
            StopBeam();
            // 引いたまま切り替えたら、新しい撃ち方で撃ち始める
            if (_isTriggerHeld)
            {
                _isTriggerHeld = false;
                SetTriggerHeld(true);
            }
            FireModeChanged?.Invoke(_fireMode);
        }

        private void ApplyReflectionMode()
        {
            // ビームは毎フレームたどり直すので、止めずにそのまま次のフレームから効く
            if (_reflectionMode == _appliedReflectionMode)
            {
                return;
            }
            _appliedReflectionMode = _reflectionMode;
            ReflectionModeChanged?.Invoke(_reflectionMode);
        }

        private void ApplyTypeIndex()
        {
            if (_typeIndex == _appliedTypeIndex)
            {
                return;
            }
            _appliedTypeIndex = _typeIndex;
            TypeChanged?.Invoke(CurrentType);
        }

        private void OnWaveDelivered(WaveHit hit, bool isAccepted)
        {
            WaveDelivered?.Invoke(hit, isAccepted);
        }
    }
}
