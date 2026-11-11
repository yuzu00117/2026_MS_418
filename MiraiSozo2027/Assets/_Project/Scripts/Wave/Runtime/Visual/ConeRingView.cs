using System;
using System.Collections.Generic;
using Aura.Inspector;
using UnityEngine;
using UnityEngine.Rendering;

namespace Aura.Wave
{
    /// @brief 扇（近距離）の 1 発の今の見た目（泡の輪）。撃った位置から細かい泡の輪が出て、当たり判定の円すいに沿って大きくなりながら進む
    /// 押し続けて連射すると、輪が連なって円すいを作る
    /// 1 つの輪は、細かい泡を輪の管の表面に密に集めて、管のまわりで回したもの（輪の穴の内側が前へ、外側が後ろへ流れる）。
    /// 管の中心には大きくぼやけたもやを置き、泡をひとまとまりの輪に見せる
    /// 重ね方は 2 つ（「重ね方」）。既定の「色を重ねる」は、泡が後ろを少し隠して色を乗せるので、明るい背景の上でも見える。
    /// 「光を足す」は、泡が長く重なる管の縁だけが明るい線になり、暗い背景で粒のままで輪の形が分かる（明るい背景の上では見えにくい）。
    /// 「光を足す」で「もや」を 0 にすると、2026-11-10 の最初の見た目になる（もやは光を足すときも、管の中心を淡く光らせる）
    /// WaveEmitter と同じ GameObject に付ける。扇の見た目の部品（IFanWaveView）が 1 つも付いていなければ、WaveEmitter が再生を始めるときにこれを足す
    /// （これまでの線の輪の見た目にしたいときは、FanRingView を付けておく）
    /// 撃った 1 発の情報（FanShotInfo）と、波の種類（WaveType）の色を読む
    /// ・輪の大きさ: 半径 = 0.1 m ＋ 進んだ距離 × tan(半角)（WaveEmitter の「扇の半角（度）」。輪の広がりは当たり判定の円すいの半角に合わせてある）
    /// ・進む速さ: WaveEmitter の「扇の波の速さ（m/s）」。先頭の輪の中心（軸の上）が、波面（当たり判定で波が渡る位置）と同じ距離を進む
    ///   当たりの波面は撃った位置からの距離で数えるので、輪の縁（撃った位置から √(進んだ距離² ＋ 半径²) の所）は波面より少し先を進む（半角が大きいほど差が大きい）
    /// ・届く距離: WaveEmitter の「扇の届く距離（m）」。輪の中心が届く距離の手前に来た所から輪がほどけ、泡は上へ浮かびながら消える
    /// 泡の位置は「輪が出てからの時間」と「泡ごとの決まった乱数」だけの式で、シェーダー（Aura/Wave/ConeRing）が毎フレーム計算する
    /// （泡の状態は持たない）。Graphics.RenderPrimitives で、全部の輪を 1 回で描く。GameObject は増えない
    /// 前の 1 発の輪が残っている間に次を撃つと、両方の輪が出る
    [DisallowMultipleComponent]
    public sealed class ConeRingView : MonoBehaviour, IFanWaveView
    {
        private const string DefaultShaderName = "Aura/Wave/ConeRing";  ///< 既定のシェーダーの名前（Scripts/Wave/Shaders/Resources/WaveConeRing.shader）
        private const int MaxRings = 64;               ///< 同時に描ける輪の数の上限（シェーダーの MAX_RINGS と同じ値）
        private const int VectorsPerRing = 5;          ///< 輪 1 つの値を詰める Vector4 の数（シェーダーの RING_VECTORS と同じ値）
        private const int VerticesPerBubble = 6;       ///< 泡 1 つの頂点の数（三角形 2 枚の四角）
        private const int DefaultRingsPerShot = 1;     ///< 「1 発の輪の数」の初期値（WaveEmitter の「押し続けると連射」で、輪が一定の間隔で並ぶ数）
        private const int MaxRingsPerShot = 8;         ///< 「1 発の輪の数」の上限
        private const float MinRingInterval = 0.02f;   ///< 「輪を出す間隔（秒）」の下限
        private const float MaxRingInterval = 0.5f;    ///< 「輪を出す間隔（秒）」の上限
        private const int MinBubbleCount = 100;        ///< 「輪 1 つの泡の数」の下限
        private const int MaxBubbleCount = 4000;       ///< 「輪 1 つの泡の数」の上限
        private const float MinBubbleSize = 0.005f;    ///< 「泡の直径（m）」の下限
        private const float MaxBubbleSize = 0.1f;      ///< 「泡の直径（m）」の上限
        private const float MinTubeRatio = 0.05f;      ///< 「管の太さ（割合）」の下限
        private const float MaxTubeRatio = 0.35f;      ///< 「管の太さ（割合）」の上限
        private const float MaxSwirlSpeed = 5.0f;      ///< 「回る速さ（周/秒）」の上限
        private const float MaxBackDim = 0.9f;         ///< 「奥の泡の暗さ」の上限
        private const float MinUnravelStart = 0.3f;    ///< 「ほどけ始める位置（割合）」の下限
        private const float MaxUnravelStart = 0.98f;   ///< 「ほどけ始める位置（割合）」の上限
        private const float MinFloatSeconds = 0.1f;    ///< 「浮かんで消える時間（秒）」の下限
        private const float MaxFloatSeconds = 2.0f;    ///< 「浮かんで消える時間（秒）」の上限
        private const float StartRadius = 0.1f;        ///< 出たときの輪の半径（m）。輪の半径 = これ ＋ 進んだ距離 × tan(半角)
        private const float MaxHalfAngle = 85.0f;      ///< 輪の大きさを決める半角の上限（度）。tan が大きくなりすぎないようにする
        private const float MinPixelDiameter = 1.5f;   ///< 泡の画面上の直径の下限（画素）。これより小さく写る泡は、この大きさにして暗くする（遠くのちらつき止め）
        private const float MaxPixelDiameter = 24.0f;  ///< 泡の画面上の直径の上限（画素）。カメラの近くの泡が大きく写りすぎないようにする
        private const float WaveMargin = 0.1f;         ///< 描く範囲の余白: 輪の波打ちで半径が増える割合（シェーダーの WAVE_AMPLITUDE より大きくする）
        private const float TubeMarginScale = 1.5f;    ///< 描く範囲の余白: 管の太さに掛ける倍率（泡を管の表面の外へ散らす分と、もやの半径（シェーダーの HAZE_SIZE の半分）を含める）
        private const float BubbleMarginScale = 2.0f;  ///< 描く範囲の余白: 泡の直径に掛ける倍率（泡ごとのばらつきと、離れた後にふくらむ分）
        private const float DetachMargin = 0.85f;      ///< 描く範囲の余白: 輪を離れた泡が、浮かんだり流れたりする距離（m）。上向きに撃つと浮きと前への流れが同じ向きに重なるので、
                                                       ///< シェーダーの FLOAT_RISE_MAX ＋ FORWARD_DRIFT_MAX ＋ √2 × WOBBLE（約 0.81 m）より大きくする
        private const int SeedMask = 0xFFFFFF;         ///< 輪の乱数の種を、float で正確に渡せる範囲（24 ビット）に収める
        private const float HazeSpacingMargin = 0.16f; ///< 描く範囲の余白: 細い管で、もやの半径が下限（隣のもやとの間隔、輪の半径の 2π/40 倍）になる分（シェーダーの HAZE_MIN_SPACING × π / HAZE_PER_RING ≒ 0.157 より大きくする）
        private const int HazePerRing = 40;            ///< 輪 1 つのもやの数（シェーダーの HAZE_PER_RING と同じ値）。輪ごとに泡の前に、この数だけ多く描く（もやの上に泡が乗る）

        private static readonly int RingsId = Shader.PropertyToID("_Rings");                ///< 輪ごとの値（Vector4 の配列）
        private static readonly int BubbleCountId = Shader.PropertyToID("_BubbleCount");    ///< 輪 1 つの泡の数
        private static readonly int BubbleSizeId = Shader.PropertyToID("_BubbleSize");      ///< 泡の直径（m）
        private static readonly int TubeRatioId = Shader.PropertyToID("_TubeRatio");        ///< 管の太さ（輪の半径に対する割合）
        private static readonly int ShellFocusId = Shader.PropertyToID("_ShellFocus");      ///< 泡を管の表面へ寄せる強さ
        private static readonly int SwirlSpeedId = Shader.PropertyToID("_SwirlSpeed");      ///< 管のまわりを回る速さ（周/秒）
        private static readonly int BackDimId = Shader.PropertyToID("_BackDim");            ///< 奥の泡の暗さ
        private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");      ///< 泡 1 つの明るさ
        private static readonly int UnravelStartId = Shader.PropertyToID("_UnravelStart");  ///< ほどけ始める位置（割合）
        private static readonly int FloatSecondsId = Shader.PropertyToID("_FloatSeconds");  ///< 浮かんで消える時間（秒）
        private static readonly int StartRadiusId = Shader.PropertyToID("_StartRadius");    ///< 出たときの輪の半径（m）
        private static readonly int MinPixelsId = Shader.PropertyToID("_MinPixels");        ///< 泡の画面上の直径の下限（画素）
        private static readonly int MaxPixelsId = Shader.PropertyToID("_MaxPixels");        ///< 泡の画面上の直径の上限（画素）
        private static readonly int CoverageId = Shader.PropertyToID("_Coverage");          ///< 泡の濃さ（色を重ねるとき）
        private static readonly int HazeAmountId = Shader.PropertyToID("_HazeAmount");      ///< もや
        private static readonly int BlendStyleId = Shader.PropertyToID("_BlendStyle");      ///< 重ね方（0 = 光を足す、1 = 色を重ねる）
        private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");          ///< 重ね方（描く色に掛ける物。マテリアルに入れる）
        private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");          ///< 重ね方（後ろの色に掛ける物。マテリアルに入れる）

        private static readonly Comparison<RingEntry> ByAge = (a, b) => a.Age.CompareTo(b.Age);  ///< 輪を、出てからの時間が短い（新しい）順に並べる
        private static readonly Comparison<RingEntry> FarFirst = (a, b) => b.ViewSqrDistance.CompareTo(a.ViewSqrDistance);  ///< 輪を、カメラから遠い順に並べる

        [Header("輪")]
        [InspectorLabel("1 発の輪の数")]
        [Tooltip("1 発で、撃った位置から続けて出す輪の数。先頭の輪の中心が波面（当たり判定で波が渡る位置）と同じ距離を進み、後ろの輪は「輪を出す間隔（秒）」ずつ遅れて同じ速さで進む\n" +
                 "当たりは変わらない（波が渡るのは、波面が届いたとき）。既定の 1 なら、WaveEmitter の「押し続けると連射」をオンにすると、輪がクールタイムごとに一定の間隔で並ぶ\n" +
                 "連射をオフにして 1 発で数本の輪を出すときは 3〜4 にする（2 以上で連射するときは、クールタイムを「輪を出す間隔（秒）」のその数倍にすると、間隔がそろう）")]
        [SerializeField, Range(1, MaxRingsPerShot)] private int _ringsPerShot = DefaultRingsPerShot;  ///< 1 発で続けて出す輪の数

        [InspectorLabel("輪を出す間隔（秒）")]
        [Tooltip("1 発の中で、輪を出す間隔（秒）。輪どうしの間は「扇の波の速さ（m/s）」× これになる")]
        [SerializeField, Range(MinRingInterval, MaxRingInterval)] private float _ringInterval = 0.08f;  ///< 1 発の中で輪を出す間隔（秒）

        [Header("泡")]
        [InspectorLabel("輪 1 つの泡の数")]
        [Tooltip("いちばん大きくなったときの、輪 1 つの泡の数。輪が小さい間（手元）は、大きさに合わせて少なく使う\n" +
                 "多いほど輪の形がはっきりし、重くなる（（泡の数 ＋ もや 40）× 6 の頂点を、輪の数だけ描く。「もや」が 0 でも、もやの頂点は数に入る）")]
        [SerializeField, Range(MinBubbleCount, MaxBubbleCount)] private int _bubbleCount = 1500;  ///< 輪 1 つの泡の数（いちばん大きいとき）

        [InspectorLabel("泡の直径（m）")]
        [Tooltip("泡 1 つの直径（m）の目安。泡ごとに 0.5〜2 倍にばらつく（小さい泡が多い）\n" +
                 "画面で 1.5 画素より小さく写る泡は、1.5 画素にして暗くする（遠くのちらつき止め）。24 画素より大きくはしない")]
        [SerializeField, Range(MinBubbleSize, MaxBubbleSize)] private float _bubbleSize = 0.025f;  ///< 泡の直径（m）の目安

        [InspectorLabel("管の太さ（割合）")]
        [Tooltip("輪の管の半径が、輪の半径の何倍か。大きいほどドーナツ・塊に見え、小さいほどイルカの泡の輪のように細い。進むほど少し細る")]
        [SerializeField, Range(MinTubeRatio, MaxTubeRatio)] private float _tubeRatio = 0.15f;  ///< 管の半径が輪の半径の何倍か

        [InspectorLabel("泡を管の表面へ寄せる")]
        [Tooltip("泡を、管の中ではなく表面の近くに集める強さ（0〜1）。1 に近いほど薄い殻になり、光を足して重ねたとき（「もや」が 0 のとき）に管の縁だけが明るい線になる（輪の形が分かる）\n" +
                 "0 なら管の中に一様に散り、真ん中がいちばん明るい「光る棒」に見える")]
        [SerializeField, Range(0.0f, 1.0f)] private float _shellFocus = 0.8f;  ///< 泡を管の表面へ寄せる強さ

        [InspectorLabel("回る速さ（周/秒）")]
        [Tooltip("泡が管のまわりを回る速さ（1 秒に何周か）。輪の穴の内側が前へ、外側が後ろへ流れる（本物の渦の輪と同じ向き）\n" +
                 "速すぎると雑音に、遅すぎると止まった点に見える。0 なら回らない")]
        [SerializeField, Range(0.0f, MaxSwirlSpeed)] private float _swirlSpeed = 1.5f;  ///< 管のまわりを回る速さ（周/秒）

        [InspectorLabel("奥の泡の暗さ")]
        [Tooltip("カメラから見て管の奥側にある泡を、どれだけ暗くするか（0〜0.9）。光を足して重ねると手前と奥が同じ明るさになり、回る向きが読めないため")]
        [SerializeField, Range(0.0f, MaxBackDim)] private float _backDim = 0.4f;  ///< 奥の泡を暗くする割合

        [Header("光り方")]
        [InspectorLabel("重ね方")]
        [Tooltip("泡を背景にどう重ねるか。実行中に変えてもよい\n" +
                 "色を重ねる（既定）: 泡が後ろを「泡の濃さ」だけ隠して色を乗せる。明るい背景（白い遺跡や空）の上でも見える\n" +
                 "光を足す: 泡の光を後ろに足す。暗い背景では、泡が重なる管の縁が光って輪の形が分かるが、明るい背景の上ではほとんど見えない（「もや」を 0 にすると、2026-11-10 の最初の見た目）")]
        [SerializeField] private ConeRingBlend _blendStyle = ConeRingBlend.Blend;  ///< 泡の重ね方

        [InspectorLabel("泡の濃さ")]
        [Tooltip("「重ね方」が「色を重ねる」のときに、泡 1 つが後ろを隠す割合（0〜1）。大きいほど、泡の色がはっきり乗る")]
        [SerializeField, Range(0.0f, 1.0f)] private float _coverage = 0.7f;  ///< 泡 1 つが後ろを隠す割合（色を重ねるとき）

        [InspectorLabel("もや")]
        [Tooltip("管の中心に置く、大きくぼやけたもやの濃さ（0〜1）。泡の間を埋めて、輪をひとまとまりの塊に見せる（「色を重ねる」では、泡より深い色にする）。0 なら泡だけ（2026-11-10 の最初の見た目）\n" +
                 "もやは輪がほどけるのに合わせて薄れ、届く距離で消える")]
        [SerializeField, Range(0.0f, 1.0f)] private float _hazeAmount = 1.0f;  ///< もやの濃さ

        [InspectorLabel("明るさ")]
        [Tooltip("泡 1 つの明るさ（WaveType の「波の色」に掛ける）\n" +
                 "光を足すとき: 泡が重なる管の縁ほど明るくなる。縁の重なりだけが光のにじみ（Bloom）で光るくらいにする（全部の泡が光ると、輪全体がぼやけた光の塊になる）\n" +
                 "色を重ねるとき: 隠した分より少し明るく光らせる強さ（2 なら 3 割ほど明るい。0 なら光らせず、色を乗せるだけ）")]
        [SerializeField, Min(0.0f)] private float _brightness = 2.0f;  ///< 泡 1 つの明るさ

        [Header("一生")]
        [InspectorLabel("ほどけ始める位置（割合）")]
        [Tooltip("WaveEmitter の「扇の届く距離（m）」のうち、どこから輪がほどけ始めるか（割合）。泡は輪を離れ、上へ浮かびながら消える\n" +
                 "届く距離に着くまでに、すべての泡が輪を離れる（ごく一部の泡は、途中でこぼれる）")]
        [SerializeField, Range(MinUnravelStart, MaxUnravelStart)] private float _unravelStart = 0.82f;  ///< 届く距離のうち、ほどけ始める位置（割合）

        [InspectorLabel("浮かんで消える時間（秒）")]
        [Tooltip("輪を離れた泡が、上へ浮かびながら消えるまでの時間（秒）")]
        [SerializeField, Range(MinFloatSeconds, MaxFloatSeconds)] private float _floatSeconds = 0.6f;  ///< 輪を離れた泡が消えるまでの時間（秒）

        [Header("シェーダー")]
        [InspectorLabel("泡のシェーダー")]
        [Tooltip("泡の輪を描くシェーダー。空なら既定のシェーダー（Aura/Wave/ConeRing）を使う\n" +
                 "再生中に変えたときは、次に再生したときから効く")]
        [SerializeField] private Shader _shader;  ///< 泡の輪を描くシェーダー。空なら既定のもの

        private readonly List<ConeShot> _shots = new List<ConeShot>();      ///< 1 発ごとの輪（進んでいるものと、使い回すために取っておくもの）
        private readonly List<RingEntry> _entries = new List<RingEntry>();  ///< このフレームで描く輪
        private readonly Vector4[] _ringData = new Vector4[MaxRings * VectorsPerRing];  ///< シェーダーに渡す輪ごとの値（長さはいつも同じ）
        private Material _material;                 ///< 泡を描くマテリアル（EnsureResources で作る）
        private MaterialPropertyBlock _properties;  ///< シェーダーに渡す値の入れ物（使い回す）
        private int _nextSerial;                    ///< 次に撃った 1 発に付ける通し番号（輪の乱数の種に使う）
        private ConeRingBlend? _appliedBlendStyle;  ///< マテリアルに入れた重ね方（変わったときだけ入れ直す）
        private Camera _viewCamera;                 ///< 輪を並べる基準のメインカメラ（無いか無効になったときだけ取り直す）
        private bool _hasWarnedMissingShader;       ///< シェーダーが見つからないことを、もう知らせたか

        public void ShowFanShot(in FanShotInfo shot)
        {
            if (shot.Type == null || !(shot.Range > 0.0f) || !(shot.Speed > 0.0f) || !EnsureResources())
            {
                return;
            }
            ConeShot coneShot = GetIdleShot();
            coneShot.Info = shot;
            coneShot.Serial = _nextSerial;
            coneShot.IsActive = true;
            _nextSerial = (_nextSerial + 1) & SeedMask;
        }

        public void ClearFanShots()
        {
            foreach (ConeShot coneShot in _shots)
            {
                coneShot.IsActive = false;
            }
        }

        private void LateUpdate()
        {
            if (_material == null)
            {
                return;
            }
            CollectRings(Time.time);
            if (_entries.Count == 0)
            {
                return;
            }
            // 色を重ねるときは、描く順で重なりが変わるので、手前の輪が奥の輪を隠すよう遠い順に描く（光を足すときは順に関わらず同じ）
            if (_blendStyle == ConeRingBlend.Blend)
            {
                SortRingsFarFirst();
            }
            Bounds bounds = PackRings();
            int bubbleCount = Mathf.Clamp(_bubbleCount, MinBubbleCount, MaxBubbleCount);
            ApplyBlendStyle();
            SetProperties(bubbleCount);
            // 泡の位置はシェーダーが式で決める。頂点の入れ物は無く、「6 頂点 ×（泡の数 ＋ もやの数）」を輪の数だけ描く（描く範囲は自分で渡す）
            var renderParams = new RenderParams(_material)
            {
                matProps = _properties,
                worldBounds = bounds,
                shadowCastingMode = ShadowCastingMode.Off,
                receiveShadows = false,
            };
            Graphics.RenderPrimitives(renderParams, MeshTopology.Triangles, VerticesPerBubble * (bubbleCount + HazePerRing), _entries.Count);
        }

        private void OnDisable()
        {
            ClearFanShots();
        }

        private void OnDestroy()
        {
            if (_material != null)
            {
                Destroy(_material);
            }
        }

        /// @brief このフレームで描く輪を集める（出た後で、まだ消えていない輪）。輪がすべて消えた 1 発は終える
        /// 上限（MaxRings）を越えるときは、新しい輪を残す
        /// @param now 今の時刻（Time.time）
        private void CollectRings(float now)
        {
            _entries.Clear();
            int ringsPerShot = Mathf.Clamp(_ringsPerShot, 1, MaxRingsPerShot);
            float interval = Mathf.Clamp(_ringInterval, MinRingInterval, MaxRingInterval);
            float floatSeconds = Mathf.Clamp(_floatSeconds, MinFloatSeconds, MaxFloatSeconds);
            for (int shotIndex = 0; shotIndex < _shots.Count; ++shotIndex)
            {
                ConeShot coneShot = _shots[shotIndex];
                if (!coneShot.IsActive)
                {
                    continue;
                }
                FanShotInfo info = coneShot.Info;
                // 輪は届く距離に着くまで進み、その後、離れた泡が浮かんで消える
                float lifetime = info.Range / info.Speed + floatSeconds;
                bool isAlive = false;
                for (int ring = 0; ring < ringsPerShot; ++ring)
                {
                    float age = now - (info.StartTime + ring * interval);
                    if (age >= lifetime)
                    {
                        continue;
                    }
                    isAlive = true;
                    if (age >= 0.0f)
                    {
                        _entries.Add(new RingEntry(shotIndex, ring, age));
                    }
                }
                coneShot.IsActive = isAlive;
            }
            if (_entries.Count > MaxRings)
            {
                _entries.Sort(ByAge);
                _entries.RemoveRange(MaxRings, _entries.Count - MaxRings);
            }
        }

        /// @brief 描く輪を、メインカメラから輪の中心（軸の上）までの距離が遠い順に並べる。メインカメラが無ければ並べ替えない
        private void SortRingsFarFirst()
        {
            if (_viewCamera == null || !_viewCamera.isActiveAndEnabled)
            {
                _viewCamera = Camera.main;
            }
            if (_viewCamera == null || _entries.Count < 2)
            {
                return;
            }
            Vector3 viewPosition = _viewCamera.transform.position;
            for (int i = 0; i < _entries.Count; ++i)
            {
                RingEntry entry = _entries[i];
                FanShotInfo info = _shots[entry.ShotIndex].Info;
                float distance = Mathf.Min(info.Speed * entry.Age, info.Range);
                Vector3 center = info.Origin + info.GetRotation() * Vector3.forward * distance;
                _entries[i] = entry.WithViewSqrDistance((center - viewPosition).sqrMagnitude);
            }
            _entries.Sort(FarFirst);
        }

        /// @brief 描く輪の値を、シェーダーに渡す配列に詰める
        /// 1 つの輪に Vector4 を 5 つ使う: (撃った位置, 出てからの時間)、(進む向き, 速さ)、(輪の面の右, 届く距離)、(輪の面の上, tan(半角))、(色（線形）, 乱数の種)
        /// @return 描く範囲（ワールド座標）。撃った位置から今の輪までを、輪の大きさと離れた泡の動きの分だけ広げた箱を、全部の輪で合わせたもの
        private Bounds PackRings()
        {
            float tubeRatio = Mathf.Clamp(_tubeRatio, MinTubeRatio, MaxTubeRatio);
            float bubbleSize = Mathf.Clamp(_bubbleSize, MinBubbleSize, MaxBubbleSize);
            var bounds = new Bounds();
            for (int i = 0; i < _entries.Count; ++i)
            {
                RingEntry entry = _entries[i];
                ConeShot coneShot = _shots[entry.ShotIndex];
                FanShotInfo info = coneShot.Info;
                Quaternion rotation = info.GetRotation();
                Vector3 origin = info.Origin;
                Vector3 axis = rotation * Vector3.forward;
                Vector3 right = rotation * Vector3.right;
                Vector3 up = rotation * Vector3.up;
                float tanHalf = Mathf.Tan(Mathf.Clamp(info.HalfAngle, 0.0f, MaxHalfAngle) * Mathf.Deg2Rad);
                // プロジェクトは線形の色空間なので、色は線形にして渡す（Vector4 の配列は、SetColor と違って変換されない）
                Color color = info.Type != null ? info.Type.Color.linear : Color.white;
                int seed = (coneShot.Serial * MaxRingsPerShot + entry.RingIndex) & SeedMask;
                int index = i * VectorsPerRing;
                _ringData[index] = new Vector4(origin.x, origin.y, origin.z, entry.Age);
                _ringData[index + 1] = new Vector4(axis.x, axis.y, axis.z, info.Speed);
                _ringData[index + 2] = new Vector4(right.x, right.y, right.z, info.Range);
                _ringData[index + 3] = new Vector4(up.x, up.y, up.z, tanHalf);
                _ringData[index + 4] = new Vector4(color.r, color.g, color.b, seed);

                float distance = Mathf.Min(info.Speed * entry.Age, info.Range);
                float ringRadius = (StartRadius + distance * tanHalf) * (1.0f + WaveMargin);
                float margin = ringRadius * (1.0f + Mathf.Max(TubeMarginScale * tubeRatio, HazeSpacingMargin)) + BubbleMarginScale * bubbleSize + DetachMargin;
                var ringBounds = new Bounds(origin, Vector3.zero);
                ringBounds.Encapsulate(origin + axis * distance);
                ringBounds.Expand(2.0f * margin);
                if (i == 0)
                {
                    bounds = ringBounds;
                }
                else
                {
                    bounds.Encapsulate(ringBounds);
                }
            }
            return bounds;
        }

        /// @brief シェーダーに渡す値を入れ物に入れる
        /// @param bubbleCount 輪 1 つの泡の数
        private void SetProperties(int bubbleCount)
        {
            _properties.SetVectorArray(RingsId, _ringData);
            _properties.SetFloat(BubbleCountId, bubbleCount);
            _properties.SetFloat(BubbleSizeId, Mathf.Clamp(_bubbleSize, MinBubbleSize, MaxBubbleSize));
            _properties.SetFloat(TubeRatioId, Mathf.Clamp(_tubeRatio, MinTubeRatio, MaxTubeRatio));
            _properties.SetFloat(ShellFocusId, Mathf.Clamp01(_shellFocus));
            _properties.SetFloat(SwirlSpeedId, Mathf.Clamp(_swirlSpeed, 0.0f, MaxSwirlSpeed));
            _properties.SetFloat(BackDimId, Mathf.Clamp(_backDim, 0.0f, MaxBackDim));
            _properties.SetFloat(BrightnessId, Mathf.Max(0.0f, _brightness));
            _properties.SetFloat(UnravelStartId, Mathf.Clamp(_unravelStart, MinUnravelStart, MaxUnravelStart));
            _properties.SetFloat(FloatSecondsId, Mathf.Clamp(_floatSeconds, MinFloatSeconds, MaxFloatSeconds));
            _properties.SetFloat(StartRadiusId, StartRadius);
            _properties.SetFloat(MinPixelsId, MinPixelDiameter);
            _properties.SetFloat(MaxPixelsId, MaxPixelDiameter);
            _properties.SetFloat(CoverageId, Mathf.Clamp01(_coverage));
            _properties.SetFloat(HazeAmountId, Mathf.Clamp01(_hazeAmount));
            _properties.SetFloat(BlendStyleId, _blendStyle == ConeRingBlend.Blend ? 1.0f : 0.0f);
        }

        /// @brief 重ね方を、マテリアルの重ねる設定に入れる（変わったときだけ）
        /// 重ねる設定は描くときの状態なので、MaterialPropertyBlock ではなくマテリアルに入れる
        private void ApplyBlendStyle()
        {
            if (_appliedBlendStyle == _blendStyle)
            {
                return;
            }
            _appliedBlendStyle = _blendStyle;
            _material.SetFloat(SrcBlendId, (float)BlendMode.One);
            _material.SetFloat(DstBlendId, (float)(_blendStyle == ConeRingBlend.Blend ? BlendMode.OneMinusSrcAlpha : BlendMode.One));
        }

        /// @brief 輪が進んでいない 1 発を返す。すべて進んでいれば、新しく作る
        /// @return 使える 1 発（無ければ新しく作った物）
        private ConeShot GetIdleShot()
        {
            foreach (ConeShot coneShot in _shots)
            {
                if (!coneShot.IsActive)
                {
                    return coneShot;
                }
            }
            var created = new ConeShot();
            _shots.Add(created);
            return created;
        }

        /// @brief 描くのに使うマテリアルを用意する
        /// @return 用意できたら true（シェーダーが見つからないときは false）
        private bool EnsureResources()
        {
            if (_material != null)
            {
                return true;
            }
            Shader shader = _shader != null ? _shader : Shader.Find(DefaultShaderName);
            if (shader == null || !shader.isSupported)
            {
                if (!_hasWarnedMissingShader)
                {
                    _hasWarnedMissingShader = true;
                    string reason = shader == null ? "が見つからない" : "がこの環境では使えない（URP と、シェーダーの 4.5 以上が要る）";
                    Debug.LogWarning($"{nameof(ConeRingView)}: シェーダー（{DefaultShaderName}）{reason}ので、円すいの輪を描けない（当たり判定は働く）", this);
                }
                return false;
            }
            _material = new Material(shader) { name = "ConeRing", hideFlags = HideFlags.HideAndDontSave };
            _properties = new MaterialPropertyBlock();
            return true;
        }

        /// @brief 1 発分の輪
        private sealed class ConeShot
        {
            public FanShotInfo Info { get; set; }  ///< 撃った 1 発の情報
            public int Serial { get; set; }        ///< 撃った順の通し番号（輪の乱数の種に使う）
            public bool IsActive { get; set; }     ///< 輪が残っているか（まだ出ていない輪も含む）
        }

        /// @brief このフレームで描く輪 1 つ
        private readonly struct RingEntry
        {
            /// @param shotIndex 1 発の番号（_shots の中の位置）
            /// @param ringIndex 1 発の中の輪の番号（0 が先頭の輪）
            /// @param age 輪が出てからの時間（秒）
            /// @param viewSqrDistance メインカメラから輪の中心までの距離の 2 乗（並べ替えに使う。並べないときは 0）
            public RingEntry(int shotIndex, int ringIndex, float age, float viewSqrDistance = 0.0f)
            {
                ShotIndex = shotIndex;
                RingIndex = ringIndex;
                Age = age;
                ViewSqrDistance = viewSqrDistance;
            }

            public int ShotIndex { get; }          ///< 1 発の番号（_shots の中の位置）
            public int RingIndex { get; }          ///< 1 発の中の輪の番号（0 が先頭の輪）
            public float Age { get; }              ///< 輪が出てからの時間（秒）
            public float ViewSqrDistance { get; }  ///< メインカメラから輪の中心までの距離の 2 乗

            /// @brief カメラからの距離だけを入れ替えた写しを返す
            /// @param viewSqrDistance メインカメラから輪の中心までの距離の 2 乗
            /// @return 距離を入れ替えた写し
            public RingEntry WithViewSqrDistance(float viewSqrDistance)
            {
                return new RingEntry(ShotIndex, RingIndex, Age, viewSqrDistance);
            }
        }
    }
}
