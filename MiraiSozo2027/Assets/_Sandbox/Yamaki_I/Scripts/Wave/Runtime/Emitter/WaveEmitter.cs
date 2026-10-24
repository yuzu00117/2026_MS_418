using System;
using System.Collections.Generic;
using Aura.Inspector;
using Aura.ObjectInfo;
using UnityEngine;

namespace Aura.Wave
{
    /// @brief ObjectInfo の情報だけで動くビーム。プレイヤーの手からも、フィールドのギミックからも出せる
    /// ・ビームの種類・強さ・届く距離は、撃ち手の本体に 1 つ付けた BeamSourceInfo（B1〜B3）から読む
    ///   プレイヤーの波の切り替え（Select Next Source）は、その BeamSourceInfo の「波の種類」を電磁波⇔超音波に入れ替える
    /// ・跳ね返るかは、当たった物の素材（Substance の SubstanceType、O6）で決める。素材の無い物では止まる
    /// ・当たった物に WaveReaction（O3、O4）と ReactionTrigger があれば、ビームを渡す。弱点（WeakPoint、O5）なら強さに倍率を掛ける
    /// ・ビームは、一定の間隔の点（BeamPoint）と点どうしをつなぐ直線で作る。
    ///   点を念力で掴んで動かすと、掴んだ点の前後が曲がる（BeamSourceInfo の Is Bendable がオンのときだけ）
    /// ・撃ち方は「ビーム（点と線）」と「扇（近距離）」の 2 つ（Fire Shape）。扇は正面から「扇の半角（度）」以内の物すべてに
    ///   同時に波を渡す。曲げ・跳ね返りはせず、壁の裏には届かない
    public sealed class WaveEmitter : MonoBehaviour
    {
        private const int MaxReflectionLimit = 16;           ///< Max Reflections に決められる最大の回数
        private const float SurfaceOffset = 0.01f;           ///< 跳ね返った後、次の判定を始める位置を面から離す距離（m）
        private const float MinNormalSqrMagnitude = 0.5f;    ///< 面の法線の長さの 2 乗がこれより小さければ、向きが取れなかったとみなす
        private const float MinTargetSqrDistance = 0.0001f;  ///< 手の先から狙う先までの距離の 2 乗（m^2）がこれより小さければ、向きが決まらないとみなす
        private const float MinSegmentLength = 0.0001f;      ///< 点と線のビームで、これより短い線は当たり判定をしない（m）
        private const float MinPointSpacing = 0.05f;         ///< Point Spacing に決められる最小の間隔（m）
        private const float MinBendRange = 0.1f;             ///< Bend Range に決められる最小の範囲（m）
        private const float BodyLineWidth = 0.01f;           ///< ビームの実体の線（点どうしをつなぐ直線）を見せるときの太さ（m）
        private const float BodyLineWhiteBlend = 0.5f;       ///< ビームの実体の線の色を、ビームの色から白へ寄せる割合

        [Header("ビームの種類")]
        [InspectorLabel("ビームの情報")]
        [Tooltip("出すビームの情報（BeamSourceInfo。波の種類・強さ・届く距離・念力で曲げられるか）。撃ち手の本体に 1 つ付けておく\n" +
                 "空なら、この GameObject か親に付いた BeamSourceInfo を自動で使う（ふつうは空のままでよい）")]
        [SerializeField] private BeamSourceInfo _source;              ///< 出すビームの情報。空なら自分か親から探す

        [InspectorLabel("入力で波の種類を切り替える")]
        [Tooltip("オンにすると、Select Next Source / Select Previous Source（チームの PlayerController では Q キー／ゲームパッドの LT）で、" +
                 "「ビームの情報」の「波の種類」を電磁波⇔超音波に入れ替える（プレイヤー用）\n" +
                 "オフなら、入力では変わらない（決まった種類だけを出すタレットや敵）。UnityEvent やコードからの Switch Wave Kind と、コードからの Select Wave Kind は、オフでも効く")]
        [SerializeField] private bool _canSwitchWaveKindByInput;      ///< 入力で波の種類を切り替えるか

        [InspectorLabel("（古い形）出せるビーム")]
        [Tooltip("古い形。BeamSourceInfo を付けた子の GameObject を並べ、Select Next Source で上から順に選び直す\n" +
                 "1 つでも入っていると古い形で動き、「ビームの情報」と「入力で波の種類を切り替える」は使わない\n" +
                 "新しい形にするには、撃ち手の本体に BeamSourceInfo を 1 つ付けて、この一覧を空にする（空のときは Inspector に出ない）")]
        [SerializeField] private BeamSourceInfo[] _sources;           ///< （古い形）出せるビーム

        [InspectorLabel("（古い形）今のビームの番号")]
        [Tooltip("古い形の「出せるビーム」の何番目か（0 から数える）。実行中に変えると、そのビームに切り替わる")]
        [SerializeField, Min(0)] private int _sourceIndex;            ///< （古い形）今のビームの番号

        [InspectorLabel("ビームを出したもの")]
        [Tooltip("出したもの。出し始めるときに、BeamSourceInfo の SourceKind に入れる（シートの B5）\n" +
                 "受け手の反応には使わない（受け手は波の種類だけで反応する）")]
        [SerializeField] private BeamSourceKind _sourceKind = BeamSourceKind.Player;  ///< 出したもの

        [InspectorLabel("始めから出し続ける")]
        [Tooltip("オンにすると、始めからビームを出し続ける（フィールドのギミック用）。入力の部品が無くても出る\n" +
                 "Set Trigger Held を UnityEvent から呼ぶと、出す・止めるを切り替えられる")]
        [SerializeField] private bool _isFiringOnStart;               ///< 始めからビームを出し続けるか

        [Header("撃ち方")]
        [InspectorLabel("撃ち方")]
        [Tooltip("波の出し方。実行中に変えてもよい（引き金を引いたままなら、次のフレームから新しい撃ち方で出る）\n" +
                 "ビーム（点と線）: 長射程。素材で跳ね返り、点を念力で掴んで曲げられる\n" +
                 "扇（近距離）: 低射程・広範囲。「扇の届く距離（m）」の中で、正面から「扇の半角（度）」以内の物すべてに同時に波を渡す。曲げ・跳ね返りはせず、壁の裏には届かない\n" +
                 "切り替えの入力は、ゲームの側から Toggle Fire Shape を呼ぶ（チームの PlayerController では B キー／ゲームパッドの十字キーの上）")]
        [SerializeField] private WaveFireShape _fireShape = WaveFireShape.Beam;  ///< 撃ち方

        [InspectorLabel("扇の届く距離（m）")]
        [Tooltip("扇が届く距離（m）。ビームの届く距離（BeamSourceInfo の「届く距離（m）」）とは別で、扇は近距離の設計。0 なら届かない\n" +
                 "波の種類と強さは、ビームと同じく「ビームの情報」（BeamSourceInfo）から読む")]
        [SerializeField, Min(0.0f)] private float _fanRange = 6.0f;  ///< 扇の届く距離（m）

        [InspectorLabel("扇の半角（度）")]
        [Tooltip("扇の広がりの半分の角度（度）。正面からこの角度以内の物に当たる（例: 40 なら全体で 80 度の扇）。見た目の弧も同じ広がりで描く\n" +
                 "当たるかは、出る位置から見た「候補のいちばん近い点」の向きで決める（大きい物は、端が扇に入っていても、いちばん近い点が外なら当たらない）")]
        [SerializeField, Range(1.0f, 89.0f)] private float _fanHalfAngle = 40.0f;  ///< 扇の半角（度）

        [Header("狙い")]
        [InspectorLabel("ビームが出る位置")]
        [Tooltip("ビームが出る位置（手の先に置いた空の GameObject など）。空なら、この GameObject の位置から出る\n" +
                 "一人称と三人称で手が変わるときは、コードから Muzzle を差し替える\n" +
                 "「狙いを決める視点」が空のときは、この正面（青い矢印の向き）に出す")]
        [SerializeField] private Transform _muzzle;                ///< ビームが出る位置

        [InspectorLabel("狙いを決める視点")]
        [Tooltip("狙いを決める視点（ふつうはカメラ）。その正面で最初に当たる位置へ向けて出すので、画面の中心（照準）に飛ぶ\n" +
                 "空なら、「ビームが出る位置」の正面に出す（ギミックのビームはこちら）")]
        [SerializeField] private Transform _aimOrigin;             ///< 狙いを決める視点

                                                                    /// @brief 波が出る位置（手の先）。一人称と三人称で手が変わるときに差し替える
        public Transform Muzzle {
            get => _muzzle != null ? _muzzle : transform;
            set => _muzzle = value;
        }

        /// @brief 狙いを決める視点（カメラ）。null なら Muzzle の正面に撃つ
        public Transform AimOrigin {
            get => _aimOrigin;
            set => _aimOrigin = value;
        }

        [InspectorLabel("当たらない自分の体")]
        [Tooltip("この GameObject とその子のコライダーには、ビームが当たらない（自分の体に当たらないようにする）\n" +
                 "跳ね返って戻ってきたビームは、ここの下のコライダーに当たると止まる\n" +
                 "空なら、この GameObject を使う")]
        [SerializeField] private Transform _ignoreRoot;               ///< この下のコライダーに当たらない。空なら自分

        [InspectorLabel("当たるレイヤー")]
        [Tooltip("ビームが当たるレイヤー。チェックを外したレイヤーの物は、ビームが素通りする")]
        [SerializeField] private LayerMask _hitMask = ~0;             ///< ビームが当たるレイヤー

        [InspectorLabel("トリガーに当たるか")]
        [Tooltip("Is Trigger をオンにしたコライダーに、ビームが当たるか")]
        [SerializeField] private QueryTriggerInteraction _triggerInteraction = QueryTriggerInteraction.Ignore;  ///< トリガーに当たるか

        [Header("ビーム")]
        [InspectorLabel("ビームの判定の半径（m）")]
        [Tooltip("当たり判定の太さ（球の半径、m）。大きいほど、狙いが少しずれても当たる。0 なら太さのない線で判定する")]
        [SerializeField, Min(0.0f)] private float _radius = 0.1f;     ///< 当たり判定の半径（m）

        [InspectorLabel("跳ね返る回数の上限")]
        [Tooltip("1 本のビームが跳ね返る回数の上限（回。1〜16）。この回数だけ跳ね返った後は、次に当たった物で止まる")]
        [SerializeField, Range(1, MaxReflectionLimit)] private int _maxReflections = 8;  ///< 跳ね返る回数の上限

        [InspectorLabel("線のマテリアル")]
        [Tooltip("ビームの線と扇の弧のマテリアル。空なら既定のマテリアルを使い、WaveType の「波の色」で色を付ける")]
        [SerializeField] private Material _lineMaterial;              ///< ビームの線と扇の弧のマテリアル。空なら既定のもの

        [Header("点と線")]
        [InspectorLabel("点の間隔（m）")]
        [Tooltip("ビームは、この間隔で並べた見えない点（BeamPoint）と、点どうしをつなぐ直線でできている。当たり判定はこの直線で行い、見た目の波はこの線に沿って流れる\n" +
                 "点は念力で掴める（ふつうの Object と同じく、コライダーと Rigidbody を持つ。念力の側の印は「点のプレハブ」に付ける）。掴んで動かすと、「支点での曲げ方」のとおりに曲がる\n" +
                 "曲げられるのは、今のビームの BeamSourceInfo の「念力で曲げられる」がオンのときだけ（オフでも点は並ぶが、掴めない）\n" +
                 "点どうしの間隔（m。0.05 以上）。狭いほど、曲げたときに直線の集まりがなめらかな曲線に見えるが、点の数と当たり判定の回数が増える\n" +
                 "曲げた所では、曲線に沿ってこの間隔で並べる。跳ね返った位置、支点（掴んだ点）、終わりの位置は、間隔に関係なく途中に入る")]
        [SerializeField, Min(MinPointSpacing)] private float _pointSpacing = 0.5f;  ///< 点どうしの間隔（m）

        [InspectorLabel("支点での曲げ方")]
        [Tooltip("掴んだ点（支点）で、ビームをどう曲げるか。実行中に変えてもよい\n" +
                 "向きを変える: 支点の手前「曲がる範囲（m）」から円の一部のように曲がって支点を通り、その先は曲がった向きのまま真っすぐ進む（当たる先が変わる）\n" +
                 "　離した支点の曲がりは「進んできた向きから見て、どちらへ何度曲がるか」として残る。手前の支点を動かしたり、手前の支点が真っすぐに戻ったりしても、" +
                 "後ろの曲がりはその向きから見て同じだけ曲がる（鎖のように一緒に動く）\n" +
                 "　届く距離は、曲げた後の道のりで数える\n" +
                 "山のように曲げる: 支点の前後「曲がる範囲（m）」だけが山のように曲がり、その外は元の線に戻る（途中の物を避ける。支点が終わりの近くなら、当たる先も変わる）\n" +
                 "　届く距離は、曲げる前の元の線の長さで数える")]
        [SerializeField] private BendMode _bendMode = BendMode.Redirect;  ///< 支点での曲げ方

        [InspectorLabel("曲がる範囲（m）")]
        [Tooltip("曲がる範囲（m。0.1 以上）\n" +
                 "向きを変える: 支点の手前この長さから曲がり始める。短いほど急に、長いほどゆるやかに曲がる（支点を横へ動かしたとき、短いと大きく向きが変わる）\n" +
                 "山のように曲げる: 支点の手前この長さから曲がり始め、支点の先この長さで元の線に戻る\n" +
                 "「山のように曲げる」では、支点どうしはこの半分より近づけられない（近すぎるときは片方だけを使う）")]
        [SerializeField, Min(MinBendRange)] private float _bendRange = 3.0f;  ///< 曲がる範囲（m）

        [InspectorLabel("離した後の曲がり")]
        [Tooltip("掴んだ点を離した後の、ビームの曲がりの扱い。実行中に変えてもよい\n" +
                 "曲がったまま: 離した位置に点が残り、ビームを止めるまで曲がったまま\n" +
                 "しばらくして戻る: 離した後「曲がったままの時間（秒）」の間は曲がったままにし、その後「戻るのにかける時間（秒）」をかけて真っすぐに戻る\n" +
                 "　どこかの点を掴んでいる間は、どの支点も時間を数えない（離すと、すべて同時に続きから数える）。なので、支点は離した順に戻る\n" +
                 "すぐ戻る: 離した瞬間に真っすぐに戻る\n" +
                 "どれでも、ビームを止めると曲がりは消える（掴まれている点は残り、次に出したビームはその点を通る）")]
        [SerializeField] private BendReleaseMode _bendReleaseMode = BendReleaseMode.Keep;  ///< 離した後の曲がりの扱い

        [InspectorLabel("曲がったままの時間（秒）")]
        [Tooltip("離した後、曲がったままにする時間（秒）。「離した後の曲がり」が「しばらくして戻る」のときだけ使う")]
        [SerializeField, Min(0.0f)] private float _bendHoldSeconds = 2.0f;  ///< 離した後、曲がったままにする時間（秒）

        [InspectorLabel("戻るのにかける時間（秒）")]
        [Tooltip("曲がったままの時間の後、真っすぐに戻るまでの時間（秒）。0 なら一度に戻る。「離した後の曲がり」が「しばらくして戻る」のときだけ使う")]
        [SerializeField, Min(0.0f)] private float _bendReturnSeconds = 1.0f;  ///< 真っすぐに戻るまでの時間（秒）

        [InspectorLabel("曲げる間は出す向きを固定")]
        [Tooltip("オンにすると、区間 0（出る位置から最初に跳ね返るまで）の点で曲げている間、ビームを出す向きを、掴んだときの向きのまま止める\n" +
                 "曲げている間とは、掴んでいる間と、離した後も曲がりが残っている間（「曲がったまま」ならビームを止めるまで）。真っすぐに戻っている間は、今の狙いへ少しずつ戻す\n" +
                 "カメラについて掴んだ物を動かす念力（Phononkinesis）で自分のビームを掴むと、カメラと一緒に元の線も回るので曲がらない。止めると、カメラを回した分だけ曲がる\n" +
                 "「狙いを決める視点の候補」が空のギミックのビームは、もともと向きが動かないので、どちらでも同じ")]
        [SerializeField] private bool _isAimLockedWhileHeld = true;  ///< 区間 0 の点で曲げている間、ビームを出す向きを止めるか

        [InspectorLabel("点を掴める半径（m）")]
        [Tooltip("点を掴める大きさ（球のコライダーの半径、m）。大きいほど、狙いが少しずれても掴める\n" +
                 "「点のプレハブ」にコライダーがあるときは、そのコライダーの大きさになり、これは使わない（コライダーが無ければ、この半径の球のトリガーを足す）")]
        [SerializeField, Min(0.01f)] private float _pointRadius = 0.3f;  ///< 点を掴める大きさ（球のコライダーの半径、m）

        [InspectorLabel("プレハブなしの点のレイヤー")]
        [Tooltip("「点のプレハブ」が空のときに作る点のレイヤー（プレハブを使うときは、プレハブのレイヤーになる）\n" +
                 "このプロジェクトは、トリガーのコライダーにもレイが当たる設定。念力の側が点を掴まない間は、そのレイを点がさえぎるので、" +
                 "念力の側の当たるレイヤーから、このレイヤーを外しておく（プレハブなしの点は PhononkinesisTarget を持たないので、Phononkinesis では掴めない）")]
        [SerializeField] private int _pointLayer;  ///< プレハブなしで作る点のレイヤー

        [InspectorLabel("点のプレハブ")]
        [Tooltip("点を作るときのプレハブ（任意）。念力の側で必要な部品（Phononkinesis の PhononkinesisTarget など）を付けておく\n" +
                 "足りない部品は作るときに足す: Rigidbody（Is Kinematic オン、Use Gravity オフにする）、コライダー（無ければ「点を掴める半径（m）」の球のトリガー）、BeamPoint\n" +
                 "空なら、球のトリガーのコライダー、Rigidbody、BeamPoint だけを持つ見えない Object を作る（念力の側の印が無いので、Phononkinesis では掴めない）。実行中に変えると、新しく作る点から効く")]
        [SerializeField] private GameObject _pointPrefab;  ///< 点を作るときのプレハブ。空なら見えない球を作る

        [InspectorLabel("点をつなぐ線を見せる")]
        [Tooltip("オンにすると、ビームの実体の線（点どうしをつなぐ直線）を、見た目の波とは別に細い線で見せる（確かめる用）\n" +
                 "点そのものは、Scene ビューに印（水色: 掴める点、灰: 掴めない点、橙: 支点、赤: 掴まれている支点）で出る")]
        [SerializeField] private bool _isBodyLineVisible;  ///< ビームの実体の線を見せるか（確かめる用）

        private readonly List<InfoBeamHit> _lastHits = new List<InfoBeamHit>();                ///< 最後のフレームでビームが当たった物（当たった順）
        private readonly HashSet<ReactionTrigger> _deliveredTriggers = new HashSet<ReactionTrigger>();  ///< このフレームで、もうビームを渡した受け手
        private InfoBeamLine _beamLine;
        private InfoBeamLine _bodyLine;      ///< ビームの実体の線（確かめる用）
        private FanWaveLine _fanLine;        ///< 扇の見た目（Wi-Fi 風の弧）
        private BeamPointChain _chain;       ///< 点と線のビームの点と曲げ方。Chain から使う
        private Vector3 _lockedAimDirection; ///< 止めているビームを出す向き（Is Aim Locked While Held）
        private bool _hasLockedAim;          ///< ビームを出す向きを止めているか
        private int _appliedSourceIndex;     ///< 最後に反映したビームの番号。Inspector での変更に気付くために持つ
        private WaveFireShape _appliedFireShape;  ///< 最後に反映した撃ち方。Inspector での変更に気付くために持つ
        private BeamSourceInfo _foundSource; ///< 「ビームの情報」が空のときに、自分か親から見つけた BeamSourceInfo
        private bool _isTriggerHeld;         ///< 引き金を引いているか

        /// @brief （古い形）出せるビームを選び直した後に起きる
        public event Action<BeamSourceInfo> SourceChanged;

        /// @brief 波の種類を入れ替えた後に起きる
        public event Action<WaveKind> WaveKindChanged;

        /// @brief 撃ち方を切り替えた後に起きる
        public event Action<WaveFireShape> FireShapeChanged;

        /// @brief 今のビームの情報（BeamSourceInfo）。無ければ null
        /// 古い形の「出せるビーム」に入っていればその今の番号のもの、空なら「ビームの情報」（それも空なら自分か親から見つけたもの）
        public BeamSourceInfo CurrentSource
        {
            get
            {
                if (HasSources)
                {
                    return _sources[_sourceIndex];
                }
                return _source != null ? _source : _foundSource;
            }
        }

        /// @brief （古い形）今のビームの番号
        public int SourceIndex => _sourceIndex;

        /// @brief （古い形）出せるビームの数
        public int SourceCount => _sources != null ? _sources.Length : 0;

        /// @brief 入力（Select Next Source / Select Previous Source）で波の種類を入れ替えるか
        public bool CanSwitchWaveKindByInput
        {
            get => _canSwitchWaveKindByInput;
            set => _canSwitchWaveKindByInput = value;
        }

        /// @brief 撃ち方（ビーム/扇）。変えると今の波を片付け、引き金を引いたままなら次のフレームから新しい撃ち方で出る
        public WaveFireShape FireShape
        {
            get => _fireShape;
            set
            {
                _fireShape = value;
                ApplyFireShape();
            }
        }

        /// @brief ビームを出しているか
        public bool IsFiring => _isTriggerHeld && CurrentType != null && isActiveAndEnabled;

        /// @brief 最後のフレームでビームが当たった物（当たった順）。出していないときは空
        public IReadOnlyList<InfoBeamHit> LastHits => _lastHits;

        /// @brief この下のコライダーには、ビームが当たらない
        public Transform IgnoreRoot => _ignoreRoot != null ? _ignoreRoot : transform;

        /// @brief 点と線のビームの点。出る位置から順に並び、最後が終わりの位置。跳ね返った位置と支点（掴んだ点）は、順番どおりの位置に入る
        /// ビームを出していないときは空
        public IReadOnlyList<BeamPoint> Points => _chain != null ? _chain.Points : System.Array.Empty<BeamPoint>();

        /// @brief 点と線のビームの、今ある支点（掴まれている点と、離された後も曲げている点）の数
        public int AnchorCount => _chain != null ? _chain.AnchorCount : 0;

        /// @brief 点どうしの間隔（m）。点と線のビームで使う
        public float PointSpacing
        {
            get => _pointSpacing;
            set => _pointSpacing = Mathf.Max(MinPointSpacing, value);
        }

        /// @brief 支点での曲げ方。点と線のビームで使う
        public BendMode BendMode
        {
            get => _bendMode;
            set => _bendMode = value;
        }

        /// @brief 曲がる範囲（m）。点と線のビームで使う
        public float BendRange
        {
            get => _bendRange;
            set => _bendRange = Mathf.Max(MinBendRange, value);
        }

        /// @brief 掴んだ点を離した後の、曲がりの扱い。点と線のビームで使う
        public BendReleaseMode BendReleaseMode
        {
            get => _bendReleaseMode;
            set => _bendReleaseMode = value;
        }

        /// @brief 古い形の「出せるビーム」に入っているか（入っていれば、古い形で動く）
        private bool HasSources => _sources != null && _sources.Length > 0;

        private BeamPointChain Chain => _chain ??= new BeamPointChain(this);

        private WaveType CurrentType
        {
            get
            {
                BeamSourceInfo source = CurrentSource;
                return source != null ? source.WaveType : null;
            }
        }

        //============================================================
        // 公開の操作
        //============================================================

        /// @brief 引き金の状態を伝える。入力の部品から毎フレーム呼んでよい。押している間ビームを出す
        /// ギミックのビームなら、UnityEvent（スイッチの On Activated など）から呼んで、出す・止めるを切り替える
        /// @param isHeld 引き金を引いているか
        [ShowInEventMenu]
        public void SetTriggerHeld(bool isHeld)
        {
            if (_isTriggerHeld == isHeld)
            {
                return;
            }
            _isTriggerHeld = isHeld;
            if (isHeld)
            {
                MarkSourceKind();
            }
            else
            {
                HideBeam();
            }
        }

        /// @brief 入力からの波の切り替え（チームの PlayerController では Q キー）
        /// 「入力で波の種類を切り替える」がオンなら、「ビームの情報」の波の種類を電磁波⇔超音波に入れ替える
        /// 古い形の「出せるビーム」に入っていれば、次のビームに選び直す（最後の次は最初に戻る）
        public void SelectNextSource()
        {
            if (HasSources)
            {
                SelectSource((_sourceIndex + 1) % _sources.Length);
            }
            else if (_canSwitchWaveKindByInput)
            {
                SwitchWaveKind();
            }
        }

        /// @brief 入力からの波の切り替え（逆回り）。波の種類は 2 つなので、新しい形では Select Next Source と同じ
        /// 古い形の「出せるビーム」に入っていれば、前のビームに選び直す（最初の前は最後に戻る）
        public void SelectPreviousSource()
        {
            if (HasSources)
            {
                SelectSource((_sourceIndex - 1 + _sources.Length) % _sources.Length);
            }
            else if (_canSwitchWaveKindByInput)
            {
                SwitchWaveKind();
            }
        }

        /// @brief 「ビームの情報」の波の種類を、電磁波⇔超音波で入れ替える。UnityEvent からも呼べる（戻り値が無いので、メニューに出る）
        /// 「入力で波の種類を切り替える」がオフでも効く。古い形の「出せるビーム」で動いているときは何もしない
        [ShowInEventMenu]
        public void SwitchWaveKind()
        {
            BeamSourceInfo source = CurrentSource;
            if (HasSources || source == null)
            {
                return;
            }
            SelectWaveKind(source.WaveKind == WaveKind.Electromagnetic ? WaveKind.Ultrasound : WaveKind.Electromagnetic);
        }

        /// @brief 「ビームの情報」の波の種類を、指定した種類にする。コードから呼ぶ（引数が選択肢なので、UnityEvent のメニューには出ない）
        /// 「入力で波の種類を切り替える」がオフでも効く。古い形の「出せるビーム」で動いているときは何もしない
        /// @param kind 波の種類
        /// @return 種類が変わったら true（もともとその種類なら false）
        public bool SelectWaveKind(WaveKind kind)
        {
            BeamSourceInfo source = CurrentSource;
            if (HasSources || source == null || source.WaveKind == kind)
            {
                return false;
            }
            source.WaveKind = kind;
            WaveKindChanged?.Invoke(kind);
            return true;
        }

        /// @brief （古い形）番号を指定して、「出せるビーム」を選び直す
        /// @param index ビームの番号
        /// @return 切り替えたら true。範囲の外か、新しい形で動いているときは false
        public bool SelectSource(int index)
        {
            if (!HasSources || index < 0 || index >= _sources.Length)
            {
                return false;
            }
            _sourceIndex = index;
            ApplySourceIndex();
            return true;
        }

        /// @brief 撃ち方をビーム⇔扇で入れ替える。入力の部品や UnityEvent から呼ぶ
        [ShowInEventMenu]
        public void ToggleFireShape()
        {
            FireShape = _fireShape == WaveFireShape.Beam ? WaveFireShape.Fan : WaveFireShape.Beam;
        }

        //============================================================
        // Unity のメッセージ
        //============================================================

        private void Awake()
        {
            _beamLine = InfoBeamLine.Create("InfoBeam", transform, _lineMaterial);
            _beamLine.Hide();
            _bodyLine = InfoBeamLine.Create("InfoBeamBody", transform, _lineMaterial);
            _bodyLine.Hide();
            _fanLine = FanWaveLine.Create("FanWave", transform, _lineMaterial);
            _fanLine.Hide();
            _sourceIndex = HasSources ? Mathf.Clamp(_sourceIndex, 0, _sources.Length - 1) : 0;
            _appliedSourceIndex = _sourceIndex;
            _appliedFireShape = _fireShape;
            // 「ビームの情報」が空なら、自分か親に付いた BeamSourceInfo を使う（子は探さない）
            _foundSource = GetComponentInParent<BeamSourceInfo>();
        }

        private void OnDestroy()
        {
            _chain?.Dispose();
        }

        private void Start()
        {
            if (_isFiringOnStart)
            {
                SetTriggerHeld(true);
            }
        }

        private void OnDisable()
        {
            HideBeam();
        }

        private void Update()
        {
            // Inspector で実行中に変えた値を反映する
            if (_sourceIndex != _appliedSourceIndex)
            {
                _sourceIndex = HasSources ? Mathf.Clamp(_sourceIndex, 0, _sources.Length - 1) : 0;
                ApplySourceIndex();
            }
            if (_fireShape != _appliedFireShape)
            {
                ApplyFireShape();
            }
        }

        private void LateUpdate()
        {
            // カメラと手が動いた後に、波の判定と見た目を合わせる
            if (IsFiring)
            {
                if (_fireShape == WaveFireShape.Fan)
                {
                    UpdateFan(Time.deltaTime);
                }
                else
                {
                    UpdateBeam(Time.deltaTime);
                }
            }
            else
            {
                HideBeam();
            }
        }

        //============================================================
        // 内部の処理
        //============================================================

        private void UpdateBeam(float deltaTime)
        {
            BeamSourceInfo source = CurrentSource;
            Transform muzzle = Muzzle;
            float range = source.Range;
            Vector3 direction = GetAimDirection(muzzle, range);
            float energy = source.EnergyPerSecond * deltaTime;
            TraceChainBeam(source, muzzle.position, direction, range, energy);

            // 受け手の処理でビームが止められた（この部品が無効にされたなど）ときは、線を出し直さない
            // 止められた後に並べ直した点も、片付け直す
            if (!IsFiring)
            {
                _chain?.Stop();
                return;
            }
            WaveType type = source.WaveType;
            float phase = Time.time * type.ScrollSpeed;

            // 見た目の波は、点どうしをつなぐ直線に沿って流す。跳ね返った位置でだけ揺れを 0 にする
            BeamPointChain chain = Chain;
            _beamLine.Draw(chain.Positions, chain.FadingCorners, muzzle.up, type, phase);
            if (_isBodyLineVisible)
            {
                _bodyLine.DrawStraight(chain.Positions, Color.Lerp(type.Color, Color.white, BodyLineWhiteBlend), BodyLineWidth);
            }
            else
            {
                _bodyLine.Hide();
            }
        }

        private void UpdateFan(float deltaTime)
        {
            BeamSourceInfo source = CurrentSource;
            Transform muzzle = Muzzle;
            Vector3 direction = GetAimDirection(muzzle, _fanRange);
            float energy = source.EnergyPerSecond * deltaTime;
            TraceFanWave(source.WaveType, muzzle.position, direction, energy);

            // 受け手の処理で波が止められた（この部品が無効にされたなど）ときは、弧を出し直さない
            if (!IsFiring)
            {
                _fanLine.Hide();
                return;
            }
            WaveType type = source.WaveType;
            float phase = Time.time * type.ScrollSpeed;
            _fanLine.Draw(muzzle.position, direction, muzzle.up, _fanHalfAngle, _fanRange, type, phase);
        }

        /// @brief 扇の中の物を調べて、ビームと同じ決まりで波を渡す。曲げ・跳ね返りはしない
        /// 近くの候補を球で集め、正面から「扇の半角（度）」以内に絞り、最後に出る位置から遮蔽のレイを 1 本飛ばして、壁の裏の物を除く
        private void TraceFanWave(WaveType type, Vector3 origin, Vector3 direction, float energy)
        {
            _lastHits.Clear();
            _deliveredTriggers.Clear();
            int count = BeamPhysics.OverlapSphere(origin, _fanRange, _hitMask, _triggerInteraction, IgnoreRoot,
                out Collider[] candidates);
            for (int i = 0; i < count; ++i)
            {
                Collider candidate = candidates[i];
                // 出る位置から見た「候補のいちばん近い点」の向きで、扇の中かを見る
                // 大きい物は、端が扇に入っていても、いちばん近い点が半角の外なら当たらない（判定はこの 1 点の近似）
                Vector3 target = GetClosestPoint(candidate, origin);
                Vector3 toTarget = target - origin;
                if (toTarget.sqrMagnitude < MinTargetSqrDistance)
                {
                    // 出る位置が候補の中にある物には渡さない（内側からのレイは本人に当たらず、遮蔽を確かめられない）
                    continue;
                }
                if (Vector3.Angle(direction, toTarget) > _fanHalfAngle)
                {
                    continue;
                }

                // 遮蔽のレイ。最初に当たったのが本人でなければ、間に物がある（壁の裏）ので渡さない
                float targetDistance = toTarget.magnitude;
                if (!BeamPhysics.TryCast(origin, toTarget / targetDistance, 0.0f, targetDistance + SurfaceOffset,
                        _hitMask, _triggerInteraction, IgnoreRoot, out RaycastHit hit)
                    || hit.collider != candidate)
                {
                    continue;
                }
                Deliver(type, energy, hit, 0, false);
            }
        }

        /// @brief 出る位置から見た、候補のいちばん近い点を返す
        /// Collider.ClosestPoint は Box・Sphere・Capsule・凸の Mesh 専用（それ以外はエラーを出して使えない）なので、
        /// それ以外（凹のメッシュ、Terrain など）は囲いの箱（bounds）のいちばん近い点で近似する
        private static Vector3 GetClosestPoint(Collider candidate, Vector3 origin)
        {
            bool isSupported = candidate is BoxCollider || candidate is SphereCollider || candidate is CapsuleCollider
                || (candidate is MeshCollider meshCollider && meshCollider.convex);
            return isSupported ? candidate.ClosestPoint(origin) : candidate.ClosestPointOnBounds(origin);
        }

        /// @brief 点と線のビームの通り道をたどり、当たった物にビームを渡す。跳ね返るかは素材で決める
        /// 1 つの区間（出る位置から最初に跳ね返るまで、跳ね返ってから次に跳ね返るまで）ごとに、支点で曲げた点の候補を作り、
        /// 前の点から次の点まで直線で当たり判定をする。跳ね返ったら、跳ね返った位置から新しい区間を始める
        /// 届く距離は、Bump なら曲げる前の元の線の長さで、Redirect なら曲げた後の道のりで数える
        private void TraceChainBeam(BeamSourceInfo source, Vector3 origin, Vector3 direction, float range, float energy)
        {
            _lastHits.Clear();
            _deliveredTriggers.Clear();
            bool isBendable = source.IsBendable;
            BeamPointChain chain = Chain;
            chain.UpdateAnchors(Time.time, isBendable, _bendReleaseMode, _bendHoldSeconds, _bendReturnSeconds, _bendMode);
            // 区間 0 を支点が曲げている間は、ビームを出す向きを、掴んだときの向きのまま止める（カメラと一緒に元の線が回らないようにする）
            // 離した後も曲げている間（Keep、戻り始めるまでの Hold Then Return）は止めたまま。戻っている間は、山の高さに合わせて今の狙いへ少しずつ戻す
            float lockWeight = _isAimLockedWhileHeld && isBendable ? chain.GetFirstLegAnchorWeight() : 0.0f;
            if (lockWeight > 0.0f)
            {
                if (!_hasLockedAim)
                {
                    _lockedAimDirection = direction;
                    _hasLockedAim = true;
                }
                direction = Vector3.Slerp(direction, _lockedAimDirection, lockWeight).normalized;
            }
            else
            {
                _hasLockedAim = false;
            }

            // 受け手の処理が例外を出しても、組み立てを必ず終える（終えないと、その後にビームを止めても点が片付かない）
            chain.BeginPath();
            try
            {
                chain.AddVertex(origin, BeamPointKind.Start, 0, -1, 0.0f);
                TraceChainLegs(chain, source.WaveType, origin, direction, range, energy, isBendable);
            }
            finally
            {
                chain.EndPath(isBendable, _pointPrefab, _pointRadius, _pointLayer);
            }
        }

        private void TraceChainLegs(BeamPointChain chain, WaveType type, Vector3 origin, Vector3 direction, float range,
            float energy, bool isBendable)
        {
            Vector3 legOrigin = origin;
            Vector3 legDirection = direction;
            float legLength = range;
            int reflectionCount = 0;
            // 1 回まわるごとに、止まるか、跳ね返る（Max Reflections 回まで）か、区間の終わりまで進みきって抜けるので、必ず終わる
            while (true)
            {
                chain.BuildLeg(reflectionCount, legOrigin, legDirection, legLength, _pointSpacing, _bendRange, _bendMode, isBendable);
                IReadOnlyList<PathSample> samples = chain.Samples;
                Vector3 from = legOrigin;
                float fromU = 0.0f;
                bool hasReflected = false;
                for (int i = 0; i < samples.Count; ++i)
                {
                    PathSample sample = samples[i];
                    BeamPointKind kind = sample.IsEnd ? BeamPointKind.End : BeamPointKind.Middle;
                    Vector3 delta = sample.Position - from;
                    float length = delta.magnitude;
                    // NaN の長さも飛ばすよう、比べた結果を否定して判定する
                    if (!(length >= MinSegmentLength))
                    {
                        // 短すぎて向きが決まらない線は調べない。支点と終わりの位置は、点として残す
                        if (sample.AnchorIndex >= 0 || sample.IsEnd)
                        {
                            chain.AddVertex(sample.Position, kind, reflectionCount, sample.AnchorIndex, sample.U);
                        }
                        continue;
                    }

                    Vector3 segmentDirection = delta / length;
                    // 跳ね返った後は、出した本人（Ignore Root の下）にも当たるようにして、当たったらそこで止める
                    Transform ignoreRoot = reflectionCount == 0 ? IgnoreRoot : null;
                    if (!BeamPhysics.TryCast(from, segmentDirection, _radius, length, _hitMask, _triggerInteraction,
                            ignoreRoot, out RaycastHit hit))
                    {
                        chain.AddVertex(sample.Position, kind, reflectionCount, sample.AnchorIndex, sample.U);
                        from = sample.Position;
                        fromU = sample.U;
                        continue;
                    }

                    // 当たった位置の、区間の始めからの長さ（PathSample.U と同じ数え方）。届く距離の残りは、ここから数える
                    float hitU = fromU + (sample.U - fromU) * (hit.distance / length);
                    if (reflectionCount > 0 && hit.collider.transform.IsChildOf(IgnoreRoot))
                    {
                        chain.AddVertex(hit.point, BeamPointKind.End, reflectionCount, -1, hitU);
                        return;
                    }

                    bool isReflected = CanReflect(hit, segmentDirection, reflectionCount, out Vector3 normal);
                    Deliver(type, energy, hit, reflectionCount, isReflected);
                    float remaining = legLength - hitU;
                    if (!isReflected || remaining <= 0.0f)
                    {
                        chain.AddVertex(hit.point, BeamPointKind.End, reflectionCount, -1, hitU);
                        return;
                    }

                    chain.AddVertex(hit.point, BeamPointKind.Reflection, reflectionCount, -1, hitU);
                    // 当たったときの当たり判定の球の中心から、面の外へ少し離して続ける
                    legOrigin = from + segmentDirection * hit.distance + normal * SurfaceOffset;
                    legDirection = Vector3.Reflect(segmentDirection, normal).normalized;
                    legLength = remaining;
                    ++reflectionCount;
                    hasReflected = true;
                    break;
                }
                if (!hasReflected)
                {
                    // 区間の終わり（届く距離の端）まで、何にも止められずに進んだ
                    return;
                }
            }
        }

        /// @brief 当たった物で跳ね返るかを決める。素材（SubstanceType）が跳ね返すものなら跳ね返り、素材が無い物では止まる
        /// @param outNormal 跳ね返る向きを決める面の法線。進む向きの手前側を向くようにそろえる
        private bool CanReflect(in RaycastHit hit, Vector3 direction, int reflectionCount, out Vector3 outNormal)
        {
            outNormal = hit.normal;
            if (reflectionCount >= _maxReflections)
            {
                return false;
            }
            // 始めから重なっていた物（距離 0）と、面の向きが取れない物では跳ね返らない
            // NaN の法線でも止まるよう、比べた結果を否定して判定する
            if (hit.distance <= 0.0f || !(outNormal.sqrMagnitude >= MinNormalSqrMagnitude))
            {
                return false;
            }
            // 面の裏から当たったときも、来た側へ跳ね返す
            if (Vector3.Dot(outNormal, direction) > 0.0f)
            {
                outNormal = -outNormal;
            }
            return ObjectInfoLookup.IsWaveReflective(hit.collider, false);
        }

        /// @brief 当たった物にビームを渡す。弱点なら倍率を掛け、WaveReaction の受け手があれば渡す
        private void Deliver(WaveType type, float energy, in RaycastHit hit, int reflectionCount, bool isReflected)
        {
            float multiplier = ObjectInfoLookup.TryGet(hit.collider, out WeakPoint weakPoint)
                ? weakPoint.GetMultiplier(type)
                : WeaknessType.NormalMultiplier;
            var beamHit = new InfoBeamHit(type, energy * multiplier, multiplier, hit.point, hit.collider,
                reflectionCount, isReflected);
            _lastHits.Add(beamHit);

            // 同じ受け手には、1 本のビーム（1 フレーム）で 1 回だけ渡す（跳ね返って、また同じ受け手に当たっても渡さない）
            if (ObjectInfoLookup.TryGet(hit.collider, out WaveReaction reaction)
                && ReactionTrigger.TryGetTrigger(reaction, out ReactionTrigger trigger)
                && _deliveredTriggers.Add(trigger))
            {
                trigger.ReceiveBeam(beamHit);
            }
        }

        /// @brief ビームを出す向きを決める。視点があれば、視点の正面で当たる位置へ Muzzle から向ける
        private Vector3 GetAimDirection(Transform muzzle, float range)
        {
            if (_aimOrigin == null) {
                return muzzle.forward;
            }

            Vector3 aimPosition = _aimOrigin.position;
            Vector3 aimForward = _aimOrigin.forward;
            float aimRange = range + Vector3.Distance(aimPosition, muzzle.position);
            Vector3 target = BeamPhysics.TryCast(aimPosition, aimForward, 0.0f, aimRange, _hitMask,
                _triggerInteraction, IgnoreRoot, out RaycastHit hit)
                ? hit.point
                : aimPosition + aimForward * aimRange;

            Vector3 toTarget = target - muzzle.position;
            // 狙う先が手より後ろ（壁に張り付いているときなど）なら、視点の向きにそのまま撃つ
            if (toTarget.sqrMagnitude < MinTargetSqrDistance || Vector3.Dot(toTarget, aimForward) <= 0.0f) {
                return aimForward;
            }
            return toTarget.normalized;
        }

        private void ApplySourceIndex()
        {
            if (_sourceIndex == _appliedSourceIndex)
            {
                return;
            }
            _appliedSourceIndex = _sourceIndex;
            if (_isTriggerHeld)
            {
                MarkSourceKind();
            }
            SourceChanged?.Invoke(CurrentSource);
        }

        private void ApplyFireShape()
        {
            if (_fireShape == _appliedFireShape)
            {
                return;
            }
            _appliedFireShape = _fireShape;
            // 今の波を片付ける。引き金を引いたままなら、次のフレームの LateUpdate から新しい撃ち方で出る
            HideBeam();
            FireShapeChanged?.Invoke(_fireShape);
        }

        /// @brief 今のビームの BeamSourceInfo に、出したもの（B5）を入れる
        private void MarkSourceKind()
        {
            BeamSourceInfo source = CurrentSource;
            if (source != null)
            {
                source.SourceKind = _sourceKind;
            }
        }

        private void HideBeam()
        {
            _lastHits.Clear();
            if (_beamLine != null)
            {
                _beamLine.Hide();
            }
            if (_bodyLine != null)
            {
                _bodyLine.Hide();
            }
            if (_fanLine != null)
            {
                _fanLine.Hide();
            }
            _hasLockedAim = false;
            _chain?.Stop();
        }
    }
}
