using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Aura.ObjectInfoDemo
{
    /// @brief 点と線のビームの、点の管理と曲げ方。InfoBeamEmitter が 1 つ持つ
    /// ・点（BeamPoint）は、出る位置から順に List に並べる。跳ね返った位置や支点は、順番どおりの位置に入る
    /// ・念力で掴まれた点が「支点」になる。支点の前後 Bend Range の範囲の点が、支点を通るようになめらかに曲がり、その外は元の線のまま
    /// ・曲げる基準の線（元の線）は、出る位置（跳ね返った後は跳ね返った位置）から真っすぐ伸ばした線で、壁などの向こうまで伸ばして考える
    ///   なので、支点を横へ動かすと、壁を避けて通したり、当たる先を変えたりできる（物に当たるかは、曲げた後の線で調べる）
    /// ・曲げ方は Bend Mode で選ぶ
    ///   Bump: 支点ごとの「山」（支点で高さが最大、前後 Bend Range で 0 になる、なめらかな形）を足し合わせて作る
    ///         支点の高さは、曲線がちょうど支点を通るように決める。支点を足したり消したりしても、その瞬間に形が跳ばない
    ///   Redirect: 支点の手前 Bend Range から円の一部のように曲がって支点を通り、その先は曲がった向きのまま真っすぐ進む
    ///         支点が複数あれば、ビームに沿って手前から順に、同じことをくり返す
    ///         離した支点の曲がりは「手前から進んできた向きに対して、どちらへ何度曲がるか」として覚える。手前の支点が動いたり
    ///         真っすぐに戻ったりしても、後ろの曲がりはその向きから見て同じだけ曲がる（鎖のように一緒に動く）
    /// 当たり判定と受け手への受け渡しは InfoBeamEmitter が行い、ここは点と線の形だけを扱う
    internal sealed class BeamPointChain
    {
        private const int MaxPathPoints = 1024;           ///< 1 本のビームの点の数の上限。これを超える分は作らず、ビームはそこで終わる
        private const float DenseStepRatio = 0.25f;       ///< 曲線の長さを測るとき、点の間隔の何倍ごとに曲線の上の位置を調べるか
        private const float MinAnchorGapRatio = 0.5f;     ///< 支点どうしを、Bend Range のこの倍より離す（近すぎる支点は、片方だけを使う）
        private const float MinAnchorGapSpacingRatio = 0.5f;  ///< 支点どうしと、出る位置から支点までを、少なくとも点の間隔のこの倍は離す
        private const float NearPointRatio = 0.25f;       ///< 支点や終わりの直前の点が、点の間隔のこの倍より近ければ、直前の点を除く（短すぎる線を作らない）
        private const float MinPivot = 0.0001f;           ///< 支点の高さを解くとき、これより小さい値で割らない（解けないときは、ほかの支点の影響を無視する）
        private const float MinBendLength = 0.0001f;      ///< Redirect で、これより短い曲がりや直線は作らない（m）
        private const float MinBendAngle = 0.0001f;       ///< Redirect で、支点への向きのずれがこれより小さければ（ラジアン）、曲げずに真っすぐ進む
        private const float BendFadeStartDegrees = 55.0f; ///< Redirect で、曲がり始めから見た支点への向きのずれが、これを超えると曲がりを弱め始める（度）
        private const float MaxBendAngleDegrees = 90.0f;  ///< Redirect で、向きのずれがこれ以上の支点では曲げない（度。真横の支点で、U ターンのように曲げない）
        private const float MinAxisSqrMagnitude = 0.000001f;  ///< Redirect で、曲がる軸の長さの 2 乗がこれより小さければ、軸が決まらないとみなして曲げない
        private const float VerticalDot = 0.99f;          ///< 区間の向きを初めて決めるとき、真上との内積の大きさがこれを超えれば、上の代わりに奥を基準にする
        private const string ContainerNameFormat = "BeamPoints ({0})";
        private const string StagingName = "Staging";
        private const string PointNameFormat = "BeamPoint {0}";
        private const int KnotPriorityReturning = 0;  ///< 近すぎる支点のどちらを残すかの順位: 戻っている途中の支点
        private const int KnotPriorityReleased = 1;   ///< 近すぎる支点のどちらを残すかの順位: 離された支点
        private const int KnotPriorityHeld = 2;       ///< 近すぎる支点のどちらを残すかの順位: 掴まれている支点

        private readonly InfoBeamEmitter _emitter;
        private readonly List<BeamPoint> _pool = new List<BeamPoint>();         ///< 支点でない点（今のビームに使っていない作り置きも含む）
        private readonly List<Anchor> _anchors = new List<Anchor>();            ///< 支点
        private readonly List<Anchor> _suppressedAnchors = new List<Anchor>();  ///< 掴まれた支点のすぐ近くにあるため、組み立ての後に消す、離された支点
        private readonly List<BeamPoint> _points = new List<BeamPoint>();       ///< 今のビームの点。出る位置から順に並ぶ
        private readonly List<Vector3> _positions = new List<Vector3>();        ///< 今のビームの点の位置（_points と同じ順）。線を描くのに使う
        private readonly List<float> _pathUs = new List<float>();               ///< 今のビームの点の、区間の始めからの長さ（_points と同じ順。PathSample.U）
        private readonly List<bool> _fadingCorners = new List<bool>();          ///< 今のビームの点のうち、見た目の波の揺れを 0 にする点（跳ね返った位置）
        private readonly List<Vertex> _vertices = new List<Vertex>();           ///< 組み立て中のビームの点
        private readonly List<Knot> _knots = new List<Knot>();                  ///< 組み立て中の区間の支点（元の線での位置が近い順）
        private readonly List<PathSample> _samples = new List<PathSample>();    ///< 組み立て中の区間の、曲げた後の点の候補
        private readonly List<PathPiece> _pieces = new List<PathPiece>();       ///< Redirect で組み立て中の区間の道（直線と円の一部を順につないだもの）
        private readonly List<int> _legAnchors = new List<int>();              ///< Redirect で組み立て中の区間の支点の番号（ビームに沿って手前から順）
        private readonly List<Quaternion> _legFrames = new List<Quaternion>(); ///< Redirect で、区間ごとの始めの向きの基準（何回跳ね返った後か、の順）。曲がりの向きは、これから見た向きで覚える
        private float[,] _matrix = new float[0, 0];   ///< 支点の高さを解くときの係数。使い回す
        private Vector3[] _rightSides = new Vector3[0];  ///< 支点の高さを解くときの右辺。使い回す
        private int[] _constraintKnots = new int[0];  ///< 高さを解く支点の、_knots での番号。使い回す
        private GameObject _container;  ///< 点をまとめる親。シーンの直下に置く（プレイヤーの子にすると、念力で動かしたときに親の動きが混ざるため）
        private Transform _staging;     ///< 点を作るときに一時的に入れる、非アクティブな親。プレハブの部品の Awake を、設定を済ませてから動かすために使う
        private int _createdCount;      ///< これまでに作った点の数。名前の番号に使う
        private bool _isStopped = true; ///< Stop の後、まだビームを組み立てていないか（止めている間、点を毎フレーム片付け直さないようにする）
        private bool _isBuilding;       ///< BeginPath から EndPath までの、組み立ての途中か
        private bool _isStopRequested;  ///< 組み立ての途中（受け手の処理の中など）で Stop が呼ばれたか。EndPath で止める

        private Vector3 _legOrigin;     ///< 組み立て中の区間の始めの位置
        private Vector3 _legDirection;  ///< 組み立て中の区間の、元の線の向き
        private float _bendRange;       ///< 支点の前後で曲げる範囲（m）

        /// @brief 点を管理する部品を作る
        /// @param emitter 持ち主の InfoBeamEmitter
        public BeamPointChain(InfoBeamEmitter emitter)
        {
            _emitter = emitter;
        }

        /// @brief 今のビームの点。出る位置から順に並ぶ（最後が終わりの位置）。ビームを出していなければ空
        public IReadOnlyList<BeamPoint> Points => _points;

        /// @brief 今のビームの点の位置（Points と同じ順）
        public IReadOnlyList<Vector3> Positions => _positions;

        /// @brief 今のビームの点のうち、見た目の波の揺れを 0 にする点（跳ね返った位置）。Positions と同じ順
        public IReadOnlyList<bool> FadingCorners => _fadingCorners;

        /// @brief 組み立て中の区間の、曲げた後の点の候補。BuildLeg の後に使う
        public IReadOnlyList<PathSample> Samples => _samples;

        /// @brief 今ある支点の数（掴まれているものと、離された後も曲げているもの。ビームが届いていないものも含む）
        public int AnchorCount => _anchors.Count;

        //============================================================
        // 支点
        //============================================================

        /// @brief 毎フレーム、ビームを組み立てる前に呼ぶ。新しく掴まれた点を支点にし、離された支点を、離した後の決まりに従って戻す
        /// 離してからの時間は、どこかの点が掴まれている間は、どの支点も数えない（離すと、すべて同時に続きから数える）
        /// なので、支点は離した順に戻る
        /// @param time 今の時刻（秒）
        /// @param isBendable 今のビームが念力で曲げられるか。false なら、離された支点はすぐに消す
        /// @param releaseMode 離した後の扱い
        /// @param holdSeconds 離した後、曲がったままにする時間（秒）。HoldThenReturn のときだけ使う
        /// @param returnSeconds 曲がったままの時間の後、真っすぐに戻るまでの時間（秒）。HoldThenReturn のときだけ使う
        /// @param bendMode 曲げ方。Redirect 以外では、離した支点の、覚えた曲がりを使わない（Redirect に戻したときに、位置から覚え直す）
        public void UpdateAnchors(float time, bool isBendable, BendReleaseMode releaseMode, float holdSeconds, float returnSeconds,
            BendMode bendMode)
        {
            // 止めた後の最初の組み立てでは、掴まれていない支点は、離されたものとして残さずに消す（ビームを止めると曲がりは消える）
            bool isFirstBuildAfterStop = _isStopped;
            bool isRedirect = bendMode == BendMode.Redirect;

            // 前のフレームのビームの点のうち、掴まれた点を支点にする（曲げられないビームの点は、コライダーが無効なので掴まれない）
            foreach (BeamPoint point in _points)
            {
                if (!point.IsAnchor && point.IsGrabbable && point.IsGrabbed)
                {
                    AddAnchor(point, isRedirect);
                }
            }

            // どこかの点が掴まれている（ビームを操作している）間は、どの支点も、離してからの時間を数えない
            bool isAnyGrabbed = false;
            foreach (Anchor anchor in _anchors)
            {
                if (!isRedirect)
                {
                    anchor.HasShape = false;
                }
                if (anchor.Point != null && anchor.Point.IsGrabbed)
                {
                    isAnyGrabbed = true;
                }
            }

            for (int i = _anchors.Count - 1; i >= 0; --i)
            {
                Anchor anchor = _anchors[i];
                BeamPoint point = anchor.Point;
                if (point == null)
                {
                    _anchors.RemoveAt(i);
                    continue;
                }
                // 手放すと重力で落とす念力で離されたときは、ビームが動かす状態に戻してから、離されたものとして扱う
                if (point.IsDropped)
                {
                    point.ResetPhysics();
                }

                if (point.IsGrabbed)
                {
                    if (!anchor.IsHeld)
                    {
                        // 離した支点を掴み直した瞬間。今の曲がりと同じ所から曲がり始めるようにする（掴んだ瞬間に形が跳ばない）
                        anchor.HeldBendAlong = GetBendAlong(anchor);
                    }
                    anchor.IsHeld = true;
                    anchor.IsReturning = false;
                    anchor.Strength = 1.0f;
                    continue;
                }
                if (isFirstBuildAfterStop || !isBendable || releaseMode == BendReleaseMode.ReturnImmediately)
                {
                    RemoveAnchorAt(i);
                    continue;
                }
                if (anchor.IsHeld)
                {
                    // 離した瞬間。Bump は離した位置を覚えて、そこを通るように曲げ続ける。Redirect は、掴んでいる間に覚えた曲がりのまま曲げ続ける
                    anchor.IsHeld = false;
                    anchor.Position = point.transform.position;
                    anchor.ReleasedSeconds = 0.0f;
                    anchor.LastUpdateTime = time;
                }

                // 離してからの時間を数える（どこかの点が掴まれている間は止める）
                float deltaTime = Mathf.Max(0.0f, time - anchor.LastUpdateTime);
                anchor.LastUpdateTime = time;
                if (!isAnyGrabbed)
                {
                    anchor.ReleasedSeconds += deltaTime;
                }
                if (releaseMode != BendReleaseMode.HoldThenReturn || anchor.ReleasedSeconds < holdSeconds)
                {
                    continue;
                }

                // 戻り始め。Bump は今の山の形をそのまま覚え、高さを 0 へ減らしていく。Redirect は、覚えた曲がりの角度を 0 へ減らしていく
                // （曲がる長さは変えないので、後ろの支点までの道のりは変わらない）。どちらも、戻り始めと消える瞬間に形が跳ばない
                if (!anchor.IsReturning)
                {
                    if (isRedirect ? !anchor.HasShape : !anchor.HasSolved)
                    {
                        RemoveAnchorAt(i);
                        continue;
                    }
                    anchor.IsReturning = true;
                    anchor.ReturningU = anchor.SolvedU;
                    anchor.ReturningHeight = anchor.SolvedHeight;
                    anchor.ReturningDirection = anchor.SolvedDirection;
                }
                float returningTime = anchor.ReleasedSeconds - holdSeconds;
                float progress = returnSeconds > 0.0f ? returningTime / returnSeconds : 1.0f;
                if (progress >= 1.0f)
                {
                    RemoveAnchorAt(i);
                    continue;
                }
                anchor.Strength = Mathf.SmoothStep(1.0f, 0.0f, progress);
            }
        }

        /// @brief Redirect で、覚えた曲がりの、曲がり始めから支点までの、曲がり始めの向きに沿った長さ（m）
        /// 掴み直したとき、ここから曲がり始めるようにすると、掴んだ瞬間の形が今の形と同じになる。曲がっていなければ -1（Bend Range を使う）
        private static float GetBendAlong(Anchor anchor)
        {
            float angle = anchor.BendAngle * (anchor.IsReturning ? anchor.Strength : 1.0f);
            if (!anchor.HasShape || angle < MinBendAngle)
            {
                return -1.0f;
            }
            return anchor.BendLength / angle * Mathf.Sin(angle);
        }

        /// @brief 区間 0（出る位置から最初に跳ね返るまで）を曲げている支点の重み
        /// 掴まれている支点と離された支点は 1、真っすぐに戻っている途中の支点は、その山の高さの割合。支点が無ければ 0
        public float GetFirstLegAnchorWeight()
        {
            float weight = 0.0f;
            foreach (Anchor anchor in _anchors)
            {
                if (anchor.LegIndex == 0 && anchor.Point != null)
                {
                    weight = Mathf.Max(weight, anchor.IsReturning ? anchor.Strength : 1.0f);
                }
            }
            return weight;
        }

        /// @brief ビームを止めている間、毎フレーム呼ぶ
        /// 掴まれていない支点を消し、点をすべて「使っていない」にする。掴まれている支点は、念力が持ったままなので残す
        /// （掴まれている点は掴めないようにしておき、次にビームを出したとき、その点を通って曲がる。止めている間に離されたら消す）
        /// 組み立ての途中（受け手の処理の中）で呼ばれたときは、組み立てが終わってから止める
        public void Stop()
        {
            if (_isBuilding)
            {
                _isStopRequested = true;
                return;
            }

            for (int i = _anchors.Count - 1; i >= 0; --i)
            {
                Anchor anchor = _anchors[i];
                BeamPoint point = anchor.Point;
                if (point == null)
                {
                    _anchors.RemoveAt(i);
                    continue;
                }
                if (point.IsDropped)
                {
                    point.ResetPhysics();
                }
                if (!point.IsGrabbed)
                {
                    RemoveAnchorAt(i);
                    continue;
                }
                anchor.IsHeld = true;
                anchor.IsReturning = false;
                anchor.Strength = 1.0f;
                point.Index = -1;
                point.SetGrabbable(false);
            }

            if (_isStopped)
            {
                return;
            }
            _isStopped = true;
            for (int i = _pool.Count - 1; i >= 0; --i)
            {
                BeamPoint point = _pool[i];
                if (point == null)
                {
                    _pool.RemoveAt(i);
                    continue;
                }
                // 止めるのと同じフレームに掴まれた点は、支点にして残す（作り置きとして使い回さない）
                if (point.IsGrabbable && point.IsGrabbed)
                {
                    AddAnchor(point, false);
                    point.Index = -1;
                    point.SetGrabbable(false);
                    continue;
                }
                MarkUnused(point);
            }
            _points.Clear();
            _positions.Clear();
            _pathUs.Clear();
            _fadingCorners.Clear();
            // 残るのは掴まれている支点だけ（掴まれている間は、覚えた曲がりを使わない）なので、向きの基準も作り直してよい
            _legFrames.Clear();
        }

        /// @brief 点をすべて消す（持ち主が消えるとき）
        public void Dispose()
        {
            _anchors.Clear();
            _suppressedAnchors.Clear();
            _pool.Clear();
            _points.Clear();
            _pathUs.Clear();
            _legFrames.Clear();
            if (_container != null)
            {
                Object.Destroy(_container);
                _container = null;
            }
        }

        //============================================================
        // 形を組み立てる
        //============================================================

        /// @brief ビームの点を組み立て始める。この後 AddVertex で出る位置から順に点を足し、最後に EndPath を呼ぶ
        public void BeginPath()
        {
            _isBuilding = true;
            _isStopRequested = false;
            _vertices.Clear();
            _suppressedAnchors.Clear();
            foreach (Anchor anchor in _anchors)
            {
                anchor.IsReached = false;
                anchor.HasSolved = false;
            }
        }

        /// @brief 1 つの区間（出る位置から最初に跳ね返るまで、跳ね返ってから次に跳ね返るまで）の、曲げた後の点の候補を作る
        /// 候補は Samples に入る。支点を通るように曲げた線の上に、spacing ごとに置く
        /// Bump: 元の線を、支点の前後 bendRange の範囲だけ曲げる。届く距離は、元の線の長さで数える
        /// Redirect: 支点の手前 bendRange から曲がって支点を通り、その先は曲がった向きのまま進む。届く距離は、曲げた後の道のりで数える
        /// @param legIndex 何回跳ね返った後の区間か（0 なら跳ね返る前）
        /// @param origin 区間の始めの位置
        /// @param direction 区間の元の線の向き（正規化したもの）
        /// @param length 区間の長さ（m）。届く距離の残り
        /// @param spacing 点の間隔（m）
        /// @param bendRange 曲げる範囲（m）。Bump は支点の前後、Redirect は支点の手前
        /// @param bendMode 曲げ方
        /// @param isBendable 念力で曲げられるか。false なら支点を使わず、真っすぐな線の上に置く
        public void BuildLeg(int legIndex, Vector3 origin, Vector3 direction, float length, float spacing, float bendRange,
            BendMode bendMode, bool isBendable)
        {
            _samples.Clear();
            _legOrigin = origin;
            _legDirection = direction;
            _bendRange = bendRange;
            _knots.Clear();
            if (length <= 0.0f)
            {
                return;
            }
            if (bendMode == BendMode.Redirect)
            {
                BuildRedirectLeg(legIndex, length, spacing, isBendable);
                return;
            }

            CollectKnots(legIndex, length, spacing, isBendable);
            int budget = MaxPathPoints - _vertices.Count;
            float step = spacing * DenseStepRatio;
            float currentU = 0.0f;
            Vector3 current = origin;
            float travelled = 0.0f;  // 最後に置いた点から、曲線に沿って進んだ長さ
            int knotIndex = 0;
            while (true)
            {
                bool isAtAnchor = knotIndex < _knots.Count;
                float stopU = isAtAnchor ? _knots[knotIndex].U : length;

                // 次の支点（無ければ区間の終わり）まで、曲線に沿って spacing ごとに点を置く
                while (currentU < stopU)
                {
                    if (_samples.Count >= budget - 1)
                    {
                        AddBudgetEnd(currentU, current, travelled);
                        return;
                    }
                    float nextU = Mathf.Min(currentU + step, stopU);
                    Vector3 next = Evaluate(nextU);
                    float segmentLength = (next - current).magnitude;
                    if (travelled + segmentLength >= spacing)
                    {
                        // この間で spacing に届く。届く位置を曲線の上に取って点を置く（travelled < spacing なので必ず先へ進む）
                        float u = Mathf.Lerp(currentU, nextU, (spacing - travelled) / segmentLength);
                        current = Evaluate(u);
                        currentU = u;
                        travelled = 0.0f;
                        _samples.Add(new PathSample(u, current, -1, false));
                        continue;
                    }
                    travelled += segmentLength;
                    current = next;
                    currentU = nextU;
                }

                // 支点や終わりのすぐ手前に点があると短すぎる線になるので、その点を除く
                int lastIndex = _samples.Count - 1;
                bool isNearPointRemoved = false;
                if (travelled < spacing * NearPointRatio && lastIndex >= 0 && _samples[lastIndex].AnchorIndex < 0)
                {
                    _samples.RemoveAt(lastIndex);
                    isNearPointRemoved = true;
                }
                if (!isAtAnchor)
                {
                    lastIndex = _samples.Count - 1;
                    // 直前の点を除いたときは、支点から終わりまで spacing 以上あるので、終わりの点を別に置く
                    if (!isNearPointRemoved && travelled < spacing * NearPointRatio && lastIndex >= 0 && _samples[lastIndex].AnchorIndex >= 0)
                    {
                        // 支点が区間の終わりにある（終わりの点を掴んだ）ときは、支点を終わりの点にする（同じ位置に 2 つ置かない）
                        PathSample anchorSample = _samples[lastIndex];
                        _samples[lastIndex] = new PathSample(anchorSample.U, anchorSample.Position, anchorSample.AnchorIndex, true);
                        return;
                    }
                    _samples.Add(new PathSample(length, Evaluate(length), -1, true));
                    return;
                }
                if (_samples.Count >= budget - 1)
                {
                    AddBudgetEnd(currentU, current, travelled);
                    return;
                }
                Knot knot = _knots[knotIndex];
                _samples.Add(new PathSample(knot.U, knot.Position, knot.AnchorIndex, false));
                current = knot.Position;
                currentU = knot.U;
                travelled = 0.0f;
                ++knotIndex;
            }
        }

        /// @brief ビームの点を 1 つ足す（出る位置から順に）
        /// @param position 位置
        /// @param kind ビームのどこにある点か
        /// @param legIndex 何回跳ね返った後の区間にあるか
        /// @param anchorIndex 支点なら、その番号（PathSample.AnchorIndex）。支点でなければ -1
        /// @param pathU 区間の始めからの長さ（m。PathSample.U と同じ数え方）。その点が掴まれたとき、支点がビームに沿ってどこにあるかに使う
        public void AddVertex(Vector3 position, BeamPointKind kind, int legIndex, int anchorIndex, float pathU)
        {
            _vertices.Add(new Vertex(position, kind, legIndex, anchorIndex, pathU));
            if (anchorIndex >= 0)
            {
                _anchors[anchorIndex].IsReached = true;
            }
        }

        /// @brief 足した点に、点の Object を割り当てて並べる。足りなければ作る
        /// 組み立ての途中で Stop が呼ばれていたら、点は並べずに止める
        /// @param isBendable 念力で曲げられるか。false なら、どの点も掴めない（コライダーを無効にする）
        /// @param pointPrefab 点を作るときのプレハブ。null なら球のコライダーだけの見えない Object を作る
        /// @param pointRadius プレハブを使わずに作った点の、球のコライダーの半径（m）
        /// @param pointLayer プレハブを使わずに作る点のレイヤー
        public void EndPath(bool isBendable, GameObject pointPrefab, float pointRadius, int pointLayer)
        {
            _isBuilding = false;
            if (_isStopRequested)
            {
                // 受け手の処理の中などで止められた。組み立てた点は使わずに、普段どおり片付ける
                _isStopRequested = false;
                _vertices.Clear();
                _suppressedAnchors.Clear();
                _isStopped = false;
                Stop();
                return;
            }

            _isStopped = false;
            _points.Clear();
            _positions.Clear();
            _pathUs.Clear();
            _fadingCorners.Clear();
            int poolIndex = 0;
            for (int i = 0; i < _vertices.Count; ++i)
            {
                Vertex vertex = _vertices[i];
                BeamPoint point;
                if (vertex.AnchorIndex >= 0)
                {
                    Anchor anchor = _anchors[vertex.AnchorIndex];
                    point = anchor.Point;
                    // 掴まれている間は念力が動かす。離された後は、曲げた線の上（戻っている途中なら戻った位置）へ動かす
                    if (!anchor.IsHeld)
                    {
                        point.Place(vertex.Position);
                    }
                }
                else
                {
                    point = GetPoolPoint(poolIndex, pointPrefab, pointRadius, pointLayer);
                    ++poolIndex;
                    point.Place(vertex.Position);
                }

                point.Index = i;
                point.Kind = vertex.Kind;
                point.LegIndex = vertex.LegIndex;
                bool isGrabbableKind = vertex.Kind == BeamPointKind.Middle || vertex.Kind == BeamPointKind.End;
                point.SetGrabbable(isBendable && (isGrabbableKind || point.IsAnchor));
                _points.Add(point);
                _positions.Add(vertex.Position);
                _pathUs.Add(vertex.PathU);
                _fadingCorners.Add(vertex.Kind == BeamPointKind.Reflection);
            }

            for (int i = poolIndex; i < _pool.Count; ++i)
            {
                MarkUnused(_pool[i]);
            }
            foreach (Anchor anchor in _anchors)
            {
                if (!anchor.IsReached)
                {
                    // ビームが届いていない支点（手前で物に当たった、届く距離の外にある）は、List に入れない
                    // 掴まれていなければ、見えないまま掴めてしまわないよう、掴めなくする
                    anchor.Point.Index = -1;
                    anchor.Point.SetGrabbable(isBendable && anchor.IsHeld);
                }
            }
            foreach (Anchor suppressed in _suppressedAnchors)
            {
                int index = _anchors.IndexOf(suppressed);
                if (index >= 0)
                {
                    RemoveAnchorAt(index);
                }
            }
            _suppressedAnchors.Clear();
        }

        //============================================================
        // 内部の処理
        //============================================================

        //============================================================
        // 曲がった先へ進む形（Redirect）
        //============================================================

        /// @brief 曲がった先へ進む形の区間を組み立て、点の候補を Samples に入れる
        /// 出る位置から元の線の向きに進み、支点ごとに、手前から円の一部のように曲がって支点を通る。支点の先は、曲がった向きのまま進む
        /// 支点は、ビームに沿って手前から順（区間の始めからの道のり PathS の順）に通る。それぞれの支点の曲がりは、次のどちらかで決める
        /// ・掴まれている支点（と、まだ曲がりを覚えていない支点）: 支点の位置を通るように決め（SolveTargetBend）、曲がりとして覚える
        /// ・離された支点: 覚えた曲がりをそのまま使う。曲がりは「直前の支点からの道のり」「曲がる長さ」「手前から進んできた向きに対する回転」
        ///   なので、手前の支点が動いたり真っすぐに戻ったりしても、後ろの曲がりはその向きから見て同じだけ曲がる（鎖のように一緒に動く）
        /// 真っすぐに戻る支点は、曲がる長さを変えずに角度だけを 0 へ減らす。後ろの支点までの道のりが変わらないので、戻る途中も消える瞬間も形が跳ばない
        /// 終わりの点を掴んだ支点で終えるかは、支点までの道のりで決める
        private void BuildRedirectLeg(int legIndex, float length, float spacing, bool isBendable)
        {
            _pieces.Clear();
            Vector3 position = _legOrigin;
            Vector3 direction = _legDirection;
            Quaternion frame = TransportLegFrame(legIndex, _legDirection);
            float total = 0.0f;          // ここまでの道のりの長さ
            float previousPathS = 0.0f;  // 直前に通った支点の、この組み立ての前の道のり（区間の始めなら 0）
            bool isEndedAtAnchor = false;
            float tolerance = spacing * NearPointRatio;
            float nearGap = spacing * MinAnchorGapSpacingRatio;
            CollectLegAnchors(legIndex, isBendable);
            int next = 0;
            for (; next < _legAnchors.Count; ++next)
            {
                int anchorIndex = _legAnchors[next];
                Anchor anchor = _anchors[anchorIndex];
                float oldPathS = anchor.PathS;
                float lead;  // 直前の支点（無ければ区間の始め）から、この支点までの道のり
                if (anchor.IsHeld || !anchor.HasShape)
                {
                    if (!TrySolveTargetBend(anchor, position, direction, frame, length - total, nearGap, out lead))
                    {
                        // 今の位置より後ろか、すぐ近く。このフレームは通らない（直前の支点との道のりは保つ）
                        anchor.PathS = total + Mathf.Max(0.0f, oldPathS - previousPathS);
                        continue;
                    }
                }
                else
                {
                    // 直前の支点からの道のりは、前の組み立てのときのまま（手前の支点が動いても、一緒に動く）
                    lead = Mathf.Max(0.0f, oldPathS - previousPathS);
                }

                float bendLength = Mathf.Min(anchor.BendLength, lead);
                float straightLength = lead - bendLength;
                float angle = anchor.BendAngle * (anchor.IsReturning ? anchor.Strength : 1.0f);
                float pathS = total + lead;
                isEndedAtAnchor = anchor.IsEndAnchor && pathS >= length - tolerance;
                AddStraightPiece(position, direction, straightLength);
                Vector3 bendStart = position + direction * straightLength;
                Vector3 end = AddArcPiece(bendStart, direction, frame * anchor.BendAxis, angle, bendLength, anchorIndex, isEndedAtAnchor,
                    out Vector3 exitDirection, out Quaternion turn);
                frame = turn * frame;
                position = end;
                direction = exitDirection;
                total = pathS;
                previousPathS = oldPathS;
                anchor.PathS = pathS;
                if (!anchor.IsHeld)
                {
                    // 実行中に Bump へ切り替えたときは、ここを通るように曲げる
                    anchor.Position = end;
                }
                RecordRedirectSolution(anchor, end);
                if (isEndedAtAnchor)
                {
                    ++next;
                    break;
                }
            }
            // 終わりの点で終えた後ろの支点は通らない。直前の支点との道のりだけ保つ
            for (; next < _legAnchors.Count; ++next)
            {
                Anchor anchor = _anchors[_legAnchors[next]];
                anchor.PathS = total + Mathf.Max(0.0f, anchor.PathS - previousPathS);
            }

            if (!isEndedAtAnchor && total < length)
            {
                AddStraightPiece(position, direction, length - total);
            }
            // 終わりの点で終えるときは、曲げて延びた分も含めて支点まで置く。そうでなければ届く距離まで置く
            EmitRedirectSamples(isEndedAtAnchor ? total : length, spacing);
        }

        /// @brief 支点の位置を通る曲がりを決め、支点の曲がりとして覚える（掴まれている支点と、まだ曲がりを覚えていない支点に使う）
        /// 曲がり始めは、支点の手前 Bend Range（掴み直したときなどは HeldBendAlong）。支点が横に大きく離れているときは、
        /// 向きのずれが BendFadeStartDegrees に収まるまで手前へ延ばす（横へ大きく引くほど、長くゆるやかな円で曲がり、支点を通り続ける）。直前の支点より手前へは延ばせない
        /// 円は、曲がり始めで進む向きに接し、支点を通るものにする（曲がる角度は、曲がり始めから見た支点への向きのずれの 2 倍）
        /// @param position 直前の支点の位置（無ければ区間の始め）
        /// @param direction 直前の支点を通った後の向き
        /// @param frame 直前の支点を通った後の、向きの基準。曲がる軸は、これから見た向きで覚える
        /// @param remaining 届く距離の残り（m）
        /// @param outLead 直前の支点から、この支点までの道のり（m）
        /// @return 通れたか。支点が今の位置より後ろか、すぐ近くなら false（このフレームは通らない）
        private bool TrySolveTargetBend(Anchor anchor, Vector3 position, Vector3 direction, Quaternion frame, float remaining, float nearGap,
            out float outLead)
        {
            outLead = 0.0f;
            Vector3 target = GetAnchorTarget(anchor);
            float u = Vector3.Dot(target - position, direction);
            if (u < nearGap)
            {
                return false;
            }
            if (anchor.IsEndAnchor && u > remaining && remaining >= nearGap)
            {
                // 終わりの点を、届く距離より前へ押し出した。端まで戻して使う（届く距離を超えて伸ばさない）
                target -= direction * (u - remaining);
                u = remaining;
            }

            float lateral = (target - position - direction * u).magnitude;
            float minAlong = anchor.HeldBendAlong >= 0.0f ? anchor.HeldBendAlong : _bendRange;
            float wantedAlong = Mathf.Max(minAlong, lateral / Mathf.Tan(BendFadeStartDegrees * Mathf.Deg2Rad));
            float bendAlong = Mathf.Min(u, wantedAlong);
            float straightLength = Mathf.Max(0.0f, u - bendAlong);
            Vector3 bendStart = position + direction * straightLength;

            // 次の支点は、曲がる角度を縮めて曲がりを弱めることがある（0 まで縮めると曲がらない）。どちらも、形が一度に跳ばないようにするため
            // ・直前の支点のすぐ近く（点の間隔の半分から 1 つ分まで）の支点
            // ・手前へ延ばしきっても向きのずれが大きい（真横に近い）支点。ずれが BendFadeStartDegrees から 90 度に近づくほど弱める
            //   （U ターンのような急な曲がりを作らない。縮め方をゆるやかにして、少し動いただけで先が大きく振れないようにする）
            float weight = Mathf.InverseLerp(nearGap, nearGap * 2.0f, u)
                * Mathf.InverseLerp(MaxBendAngleDegrees, BendFadeStartDegrees, GetBendAngleDegrees(bendStart, direction, target));
            if (weight < 1.0f)
            {
                target = ScaleBendAngle(bendStart, direction, target, weight);
            }

            float bendLength = ComputeBend(bendStart, direction, target, out Vector3 normal, out float radius, out _);
            anchor.BendLength = bendLength;
            if (radius > 0.0f)
            {
                anchor.BendAngle = bendLength / radius;
                anchor.BendAxis = Quaternion.Inverse(frame) * Vector3.Cross(direction, normal);
            }
            else
            {
                anchor.BendAngle = 0.0f;
                anchor.BendAxis = Vector3.zero;
            }
            anchor.HasShape = true;
            outLead = straightLength + bendLength;
            return true;
        }

        /// @brief この区間の支点を、ビームに沿って手前から順（PathS の順）に _legAnchors へ集める
        private void CollectLegAnchors(int legIndex, bool isBendable)
        {
            _legAnchors.Clear();
            if (!isBendable)
            {
                return;
            }
            for (int i = 0; i < _anchors.Count; ++i)
            {
                Anchor anchor = _anchors[i];
                if (anchor.LegIndex != legIndex || anchor.Point == null)
                {
                    continue;
                }
                int index = _legAnchors.Count;
                while (index > 0 && _anchors[_legAnchors[index - 1]].PathS > anchor.PathS)
                {
                    --index;
                }
                _legAnchors.Insert(index, i);
            }
        }

        /// @brief 区間の始めの向きの基準を、前のフレームの基準から、今の区間の向きへ一番小さく回して求める（覚えた曲がりの向きが、急に回らないようにする）
        /// 初めての区間は、上（真上に近い向きなら奥）を基準にする
        private Quaternion TransportLegFrame(int legIndex, Vector3 direction)
        {
            while (_legFrames.Count <= legIndex)
            {
                Vector3 up = Mathf.Abs(Vector3.Dot(direction, Vector3.up)) < VerticalDot ? Vector3.up : Vector3.forward;
                _legFrames.Add(Quaternion.LookRotation(direction, up));
            }
            Quaternion frame = _legFrames[legIndex];
            frame = (Quaternion.FromToRotation(frame * Vector3.forward, direction) * frame).normalized;
            _legFrames[legIndex] = frame;
            return frame;
        }

        /// @brief 曲がり始めから見た、支点への向きのずれ（度）。曲がる角度は、この 2 倍になる
        private static float GetBendAngleDegrees(Vector3 bendStart, Vector3 direction, Vector3 target)
        {
            Vector3 chord = target - bendStart;
            float along = Vector3.Dot(chord, direction);
            float lateral = (chord - direction * along).magnitude;
            return Mathf.Atan2(lateral, along) * Mathf.Rad2Deg;
        }

        /// @brief 曲がり始めから見た支点への向きのずれを scale 倍にした位置（曲がり始めからの距離は変えない）。0 倍なら真っすぐ先
        private static Vector3 ScaleBendAngle(Vector3 bendStart, Vector3 direction, Vector3 target, float scale)
        {
            Vector3 chord = target - bendStart;
            float chordLength = chord.magnitude;
            Vector3 lateral = chord - direction * Vector3.Dot(chord, direction);
            float lateralLength = lateral.magnitude;
            if (chordLength < MinBendLength || lateralLength < MinBendLength)
            {
                return target;
            }
            float angle = Mathf.Atan2(lateralLength, Vector3.Dot(chord, direction)) * Mathf.Clamp01(scale);
            return bendStart + (direction * Mathf.Cos(angle) + lateral / lateralLength * Mathf.Sin(angle)) * chordLength;
        }

        /// @brief Redirect で通った支点の位置を、Bump の形でも覚えておく（実行中に Bump へ切り替えて戻るときに、古い値を使わないようにする）
        private void RecordRedirectSolution(Anchor anchor, Vector3 target)
        {
            Vector3 toTarget = target - _legOrigin;
            float solvedU = Vector3.Dot(toTarget, _legDirection);
            anchor.SolvedU = solvedU;
            anchor.SolvedHeight = toTarget - _legDirection * solvedU;
            anchor.SolvedDirection = _legDirection;
            anchor.HasSolved = true;
        }

        /// @brief 支点が通る位置。掴まれている間は点の今の位置、離された後は離した位置
        private static Vector3 GetAnchorTarget(Anchor anchor)
        {
            return anchor.IsHeld ? anchor.Point.transform.position : anchor.Position;
        }

        /// @brief 真っすぐな道を足す。短すぎれば足さない
        private void AddStraightPiece(Vector3 start, Vector3 direction, float length)
        {
            if (length < MinBendLength)
            {
                return;
            }
            _pieces.Add(new PathPiece(start, direction, Vector3.zero, 0.0f, length, start + direction * length, -1, false));
        }

        /// @brief start で direction に接し、target を通る円の一部を求める
        /// @param outNormal 円の中心へ向かう向き。真っすぐな道なら 0
        /// @param outRadius 円の半径（m）。真っすぐな道なら 0
        /// @param outExitDirection target を通るときの向き（この先へ真っすぐ進む向き）
        /// @return 道のりの長さ（m）
        private static float ComputeBend(Vector3 start, Vector3 direction, Vector3 target, out Vector3 outNormal, out float outRadius,
            out Vector3 outExitDirection)
        {
            outNormal = Vector3.zero;
            outRadius = 0.0f;
            outExitDirection = direction;
            Vector3 chord = target - start;
            float chordLength = chord.magnitude;
            if (chordLength < MinBendLength)
            {
                return 0.0f;
            }

            Vector3 lateral = chord - direction * Vector3.Dot(chord, direction);
            float lateralLength = lateral.magnitude;
            float theta = Mathf.Acos(Mathf.Clamp(Vector3.Dot(direction, chord / chordLength), -1.0f, 1.0f));
            if (!(theta >= MinBendAngle) || lateralLength < MinBendLength)
            {
                // ほぼ真っすぐ先にある。曲げずに、支点へ真っすぐ進む
                outExitDirection = chord / chordLength;
                return chordLength;
            }

            // 円の半径は、弦の長さ ÷ (2 sin θ)。曲がる角度は 2θ（接線と弦のなす角の 2 倍）
            outNormal = lateral / lateralLength;
            outRadius = chordLength / (2.0f * Mathf.Sin(theta));
            float turn = 2.0f * theta;
            outExitDirection = (direction * Mathf.Cos(turn) + outNormal * Mathf.Sin(turn)).normalized;
            return outRadius * turn;
        }

        /// @brief start で direction に接し、axis のまわりに angle（ラジアン）だけ曲がる、長さ length の円の一部を足す
        /// 曲がらない（角度が小さい、軸が決まらない）ときは、真っすぐな道にする
        /// @param axis 曲がる軸。進む向きに垂直な成分だけを使う（誤差で少し傾いていても、曲がる面がずれないようにする）
        /// @param exitDirection 終わりの向き（この先へ真っすぐ進む向き）
        /// @param turn 始めの向きから終わりの向きへの回転
        /// @return 終わりの位置
        private Vector3 AddArcPiece(Vector3 start, Vector3 direction, Vector3 axis, float angle, float length, int anchorIndex,
            bool isEndAtAnchor, out Vector3 exitDirection, out Quaternion turn)
        {
            exitDirection = direction;
            turn = Quaternion.identity;
            Vector3 normal = Vector3.zero;
            float radius = 0.0f;
            Vector3 end = start + direction * length;
            axis -= direction * Vector3.Dot(axis, direction);
            if (angle >= MinBendAngle && length >= MinBendLength && axis.sqrMagnitude >= MinAxisSqrMagnitude)
            {
                axis.Normalize();
                normal = Vector3.Cross(axis, direction);
                radius = length / angle;
                end = start + direction * (radius * Mathf.Sin(angle)) + normal * (radius * (1.0f - Mathf.Cos(angle)));
                exitDirection = (direction * Mathf.Cos(angle) + normal * Mathf.Sin(angle)).normalized;
                turn = Quaternion.AngleAxis(angle * Mathf.Rad2Deg, axis);
            }
            _pieces.Add(new PathPiece(start, direction, normal, radius, length, end, anchorIndex, isEndAtAnchor));
            return end;
        }

        /// @brief 組み立てた道に沿って、spacing ごとに点の候補を置く。支点では間隔に関係なく置き、そこから数え直す
        /// @param limit 置く長さ（m）。この長さで終わりの点を置く
        private void EmitRedirectSamples(float limit, float spacing)
        {
            int budget = MaxPathPoints - _vertices.Count;
            float pathU = 0.0f;      // 区間の始めから、道に沿って進んだ長さ
            float travelled = 0.0f;  // 最後に置いた点から、道に沿って進んだ長さ
            Vector3 current = _legOrigin;
            foreach (PathPiece piece in _pieces)
            {
                float pieceLimit = Mathf.Min(piece.Length, limit - pathU);
                float s = 0.0f;
                while (s + (spacing - travelled) <= pieceLimit)
                {
                    if (_samples.Count >= budget - 1)
                    {
                        AddBudgetEnd(pathU + s, piece.GetPosition(s), travelled);
                        return;
                    }
                    s += spacing - travelled;
                    travelled = 0.0f;
                    _samples.Add(new PathSample(pathU + s, piece.GetPosition(s), -1, false));
                }
                travelled += pieceLimit - s;
                bool isTruncated = pieceLimit < piece.Length - MinBendLength;
                pathU += pieceLimit;
                current = isTruncated ? piece.GetPosition(pieceLimit) : piece.End;
                if (_samples.Count >= budget - 1)
                {
                    AddBudgetEnd(pathU, current, travelled);
                    return;
                }
                if (isTruncated)
                {
                    // 届く距離が、この道の途中で尽きた（この先の支点には届かない）
                    AddRedirectEnd(pathU, current, -1, travelled, spacing);
                    return;
                }
                if (piece.AnchorIndex >= 0)
                {
                    // 支点の先の残りが短ければ、支点を終わりの点にする（短すぎる線と、重なりそうな点を作らない）
                    if (piece.IsEndAtAnchor || pathU >= limit - spacing * NearPointRatio)
                    {
                        AddRedirectEnd(pathU, piece.End, piece.AnchorIndex, travelled, spacing);
                        return;
                    }
                    RemoveNearPoint(travelled, spacing);
                    _samples.Add(new PathSample(pathU, piece.End, piece.AnchorIndex, false));
                    travelled = 0.0f;
                }
            }
            AddRedirectEnd(pathU, current, -1, travelled, spacing);
        }

        /// @brief 終わりの点の候補を足す。支点で終わるときは、支点を終わりの点にする
        private void AddRedirectEnd(float pathU, Vector3 position, int anchorIndex, float travelled, float spacing)
        {
            RemoveNearPoint(travelled, spacing);
            _samples.Add(new PathSample(pathU, position, anchorIndex, true));
        }

        /// @brief 支点や終わりのすぐ手前に点があると短すぎる線になるので、その点を除く
        private void RemoveNearPoint(float travelled, float spacing)
        {
            int lastIndex = _samples.Count - 1;
            if (travelled < spacing * NearPointRatio && lastIndex >= 0 && _samples[lastIndex].AnchorIndex < 0)
            {
                _samples.RemoveAt(lastIndex);
            }
        }

        /// @brief 点の数の上限に達したときの、終わりの候補を足す。直前の点が今の位置にあれば、その点を終わりにする（重なった点を作らない）
        private void AddBudgetEnd(float currentU, Vector3 current, float travelled)
        {
            int lastIndex = _samples.Count - 1;
            if (travelled <= 0.0f && lastIndex >= 0)
            {
                PathSample last = _samples[lastIndex];
                _samples[lastIndex] = new PathSample(last.U, last.Position, last.AnchorIndex, true);
                return;
            }
            _samples.Add(new PathSample(currentU, current, -1, true));
        }

        /// @brief この区間の支点を、始めから近い順に集め、曲線がちょうど支点を通るように山の高さを決める
        private void CollectKnots(int legIndex, float length, float spacing, bool isBendable)
        {
            if (!isBendable)
            {
                return;
            }

            float startGap = spacing * MinAnchorGapSpacingRatio;
            float minGap = Mathf.Max(startGap, _bendRange * MinAnchorGapRatio);
            for (int i = 0; i < _anchors.Count; ++i)
            {
                Anchor anchor = _anchors[i];
                if (anchor.LegIndex != legIndex || anchor.Point == null)
                {
                    continue;
                }
                if (anchor.IsReturning)
                {
                    // 戻っている途中の支点は、戻り始めたときの山を、高さを減らして使う（通る位置は決めない）
                    // 元の線が回っていれば、山の向きも同じだけ回す（元の線に沿った向きに山ができないようにする）
                    float returningU = anchor.ReturningU;
                    if (returningU > length && (anchor.IsEndAnchor || returningU <= length + spacing * NearPointRatio))
                    {
                        returningU = length;
                    }
                    // 実行中に Redirect へ切り替えたとき、支点を通る順に使う
                    anchor.PathS = returningU;
                    if (returningU >= startGap && returningU <= length)
                    {
                        Vector3 height = Quaternion.FromToRotation(anchor.ReturningDirection, _legDirection) * anchor.ReturningHeight;
                        InsertKnot(new Knot(returningU, height * anchor.Strength, i, false, false));
                    }
                    continue;
                }

                // 支点の位置を元の線に垂直に下ろし、元の線のどこを、どれだけずらすかを決める
                Vector3 target = anchor.IsHeld ? anchor.Point.transform.position : anchor.Position;
                Vector3 toTarget = target - _legOrigin;
                float projectedU = Vector3.Dot(toTarget, _legDirection);
                float u = projectedU;
                // 終わりの点を掴んだ支点と、届く距離の端のすれすれにある支点は、端に合わせる（計算の誤差や、動かしたときのはみ出しで外れないように）
                if (u > length && (anchor.IsEndAnchor || u <= length + spacing * NearPointRatio))
                {
                    u = length;
                }
                anchor.PathS = u;
                if (u < startGap || u > length)
                {
                    // 出る位置より後ろ（すぐ近く）か、届く距離の外にある支点は使わない
                    continue;
                }
                // ずらす量は、元の線に垂直な向きだけ（端に合わせた支点も、届く距離を超えて前へ伸ばさない）
                InsertKnot(new Knot(u, toTarget - _legDirection * projectedU, i, true, anchor.IsHeld));
            }

            RemoveCrowdedKnots(minGap);
            SolveHeights();

            // 支点の位置を決める。通る位置を決めた支点は、そこをそのまま通る
            for (int k = 0; k < _knots.Count; ++k)
            {
                Knot knot = _knots[k];
                Anchor anchor = _anchors[knot.AnchorIndex];
                if (knot.IsConstraint)
                {
                    knot.Position = _legOrigin + _legDirection * knot.U + knot.Offset;
                    anchor.SolvedU = knot.U;
                    anchor.SolvedHeight = knot.Height;
                    anchor.SolvedDirection = _legDirection;
                    anchor.HasSolved = true;
                }
                else
                {
                    knot.Position = Evaluate(knot.U);
                }
                _knots[k] = knot;
            }
        }

        private void InsertKnot(in Knot knot)
        {
            int index = _knots.Count;
            while (index > 0 && _knots[index - 1].U > knot.U)
            {
                --index;
            }
            _knots.Insert(index, knot);
        }

        /// @brief 近すぎる支点は片方だけを使う。掴まれている支点 → 離された支点 → 戻っている途中の支点、の順に残す
        /// 掴まれている支点のすぐ近くの離された支点は、掴み直したものとみなして、組み立ての後に消す
        private void RemoveCrowdedKnots(float minGap)
        {
            int keptCount = 0;
            for (int i = 0; i < _knots.Count; ++i)
            {
                Knot knot = _knots[i];
                if (keptCount > 0 && knot.U - _knots[keptCount - 1].U < minGap)
                {
                    Knot kept = _knots[keptCount - 1];
                    Knot loser = knot;
                    if (GetPriority(knot) > GetPriority(kept))
                    {
                        _knots[keptCount - 1] = knot;
                        loser = kept;
                    }
                    if (!_anchors[loser.AnchorIndex].IsHeld && GetPriority(_knots[keptCount - 1]) == KnotPriorityHeld)
                    {
                        _suppressedAnchors.Add(_anchors[loser.AnchorIndex]);
                    }
                    continue;
                }
                _knots[keptCount] = knot;
                ++keptCount;
            }
            _knots.RemoveRange(keptCount, _knots.Count - keptCount);
        }

        private static int GetPriority(in Knot knot)
        {
            if (!knot.IsConstraint)
            {
                return KnotPriorityReturning;
            }
            return knot.IsHeld ? KnotPriorityHeld : KnotPriorityReleased;
        }

        /// @brief 通る位置を決めた支点（掴まれているか、離された支点）の山の高さを、曲線がちょうどそこを通るように解く
        /// 支点 i で、すべての山を足した高さ = 支点 i のずらす量、になるように、連立方程式をガウスの消去法で解く
        private void SolveHeights()
        {
            int count = 0;
            for (int k = 0; k < _knots.Count; ++k)
            {
                if (_knots[k].IsConstraint)
                {
                    ++count;
                }
            }
            if (count == 0)
            {
                return;
            }
            if (_constraintKnots.Length < count)
            {
                _constraintKnots = new int[count];
                _rightSides = new Vector3[count];
                _matrix = new float[count, count];
            }

            int row = 0;
            for (int k = 0; k < _knots.Count; ++k)
            {
                if (!_knots[k].IsConstraint)
                {
                    continue;
                }
                _constraintKnots[row] = k;
                // 戻っている途中の山は高さが決まっているので、右辺から引いておく
                Vector3 rightSide = _knots[k].Offset;
                for (int j = 0; j < _knots.Count; ++j)
                {
                    if (!_knots[j].IsConstraint)
                    {
                        rightSide -= _knots[j].Height * Bump(_knots[k].U, _knots[j].U);
                    }
                }
                _rightSides[row] = rightSide;
                ++row;
            }
            for (int r = 0; r < count; ++r)
            {
                for (int c = 0; c < count; ++c)
                {
                    _matrix[r, c] = Bump(_knots[_constraintKnots[r]].U, _knots[_constraintKnots[c]].U);
                }
            }

            if (!TrySolve(count))
            {
                // 解けないときは、ほかの支点の山を無視して、それぞれの支点のずらす量をそのまま高さにする
                for (int r = 0; r < count; ++r)
                {
                    _rightSides[r] = _knots[_constraintKnots[r]].Offset;
                }
            }
            for (int r = 0; r < count; ++r)
            {
                int k = _constraintKnots[r];
                Knot knot = _knots[k];
                knot.Height = _rightSides[r];
                _knots[k] = knot;
            }
        }

        /// @brief _matrix × 高さ = _rightSides を解き、高さを _rightSides に入れる（部分ピボット選択つきのガウスの消去法）
        private bool TrySolve(int count)
        {
            for (int column = 0; column < count; ++column)
            {
                int pivot = column;
                for (int r = column + 1; r < count; ++r)
                {
                    if (Mathf.Abs(_matrix[r, column]) > Mathf.Abs(_matrix[pivot, column]))
                    {
                        pivot = r;
                    }
                }
                if (Mathf.Abs(_matrix[pivot, column]) < MinPivot)
                {
                    return false;
                }
                if (pivot != column)
                {
                    for (int c = 0; c < count; ++c)
                    {
                        (_matrix[pivot, c], _matrix[column, c]) = (_matrix[column, c], _matrix[pivot, c]);
                    }
                    (_rightSides[pivot], _rightSides[column]) = (_rightSides[column], _rightSides[pivot]);
                }
                for (int r = column + 1; r < count; ++r)
                {
                    float factor = _matrix[r, column] / _matrix[column, column];
                    if (factor == 0.0f)
                    {
                        continue;
                    }
                    for (int c = column; c < count; ++c)
                    {
                        _matrix[r, c] -= factor * _matrix[column, c];
                    }
                    _rightSides[r] -= factor * _rightSides[column];
                }
            }
            for (int r = count - 1; r >= 0; --r)
            {
                Vector3 value = _rightSides[r];
                for (int c = r + 1; c < count; ++c)
                {
                    value -= _matrix[r, c] * _rightSides[c];
                }
                _rightSides[r] = value / _matrix[r, r];
            }
            return true;
        }

        /// @brief 元の線の、始めから u だけ進んだ位置を、支点に合わせてずらした位置
        private Vector3 Evaluate(float u)
        {
            return _legOrigin + _legDirection * u + EvaluateOffset(u);
        }

        /// @brief 元の線の、始めから u だけ進んだ位置をずらす量。支点ごとの山を足し合わせる
        private Vector3 EvaluateOffset(float u)
        {
            Vector3 offset = Vector3.zero;
            for (int k = 0; k < _knots.Count; ++k)
            {
                offset += _knots[k].Height * Bump(u, _knots[k].U);
            }
            return offset;
        }

        /// @brief 元の線の位置 anchorU にある支点の山の、位置 u での高さの割合（支点で 1、前後 Bend Range で 0）
        /// 形は余弦の形で、支点と範囲の端で傾きが 0。区間の始め（出る位置、跳ね返った位置）はずらさないよう、
        /// 支点の手前の幅は、Bend Range と区間の始めから支点までの長さの、短い方にする
        private float Bump(float u, float anchorU)
        {
            if (u >= anchorU)
            {
                return Fall((u - anchorU) / _bendRange);
            }
            return Fall((anchorU - u) / Mathf.Min(_bendRange, anchorU));
        }

        /// @brief 0 で 1、1 以上で 0 になる、両端で傾きが 0 のなめらかな形
        private static float Fall(float t)
        {
            return t >= 1.0f ? 0.0f : 0.5f + 0.5f * Mathf.Cos(Mathf.PI * Mathf.Max(0.0f, t));
        }

        /// @brief 掴まれた点を支点にする
        /// @param isSplitting Redirect か。掴んだ点が、後ろの支点の曲がりの途中にあるときは、その曲がりを掴んだ点で 2 つに分ける
        private void AddAnchor(BeamPoint point, bool isSplitting)
        {
            _pool.Remove(point);
            point.IsAnchor = true;
            float pathS = point.Index >= 0 && point.Index < _pathUs.Count ? _pathUs[point.Index] : 0.0f;
            var anchor = new Anchor(point, point.LegIndex, point.Kind == BeamPointKind.End, pathS);
            if (isSplitting)
            {
                SplitBendAt(anchor);
            }
            _anchors.Add(anchor);
        }

        /// @brief 掴んだ点（新しい支点）が、ビームに沿ってすぐ後ろの支点の曲がりの途中にあるとき、その曲がりを 2 つに分ける
        /// 掴んだ点の手前の部分は、新しい支点の曲がりになる（同じ所から曲がり始め、同じ円を通る）。後ろの支点には、残りの部分だけを残す
        /// こうすると、掴んだ瞬間の形は今の形と同じになり（跳ばない）、後ろの支点は、新しい支点を通った後の向きから見て、残りの分だけ曲がる
        private void SplitBendAt(Anchor anchor)
        {
            Anchor next = null;
            foreach (Anchor other in _anchors)
            {
                if (other.LegIndex == anchor.LegIndex && other.Point != null && other.PathS > anchor.PathS
                    && (next == null || other.PathS < next.PathS))
                {
                    next = other;
                }
            }
            if (next == null || next.IsHeld || !next.HasShape || next.BendLength < MinBendLength)
            {
                return;
            }
            float covered = anchor.PathS - (next.PathS - next.BendLength);  // 後ろの支点の曲がり始めから、掴んだ点までの長さ
            if (covered <= MinBendLength)
            {
                return;
            }
            float fraction = Mathf.Clamp01(covered / next.BendLength);
            float coveredAngle = next.BendAngle * (next.IsReturning ? next.Strength : 1.0f) * fraction;
            anchor.HeldBendAlong = coveredAngle >= MinBendAngle ? covered / coveredAngle * Mathf.Sin(coveredAngle) : -1.0f;
            next.BendAngle *= 1.0f - fraction;
            next.BendLength -= covered;
        }

        private void RemoveAnchorAt(int index)
        {
            BeamPoint point = _anchors[index].Point;
            _anchors.RemoveAt(index);
            if (point == null)
            {
                return;
            }
            point.IsAnchor = false;
            point.ResetPhysics();
            MarkUnused(point);
            _pool.Add(point);
        }

        private static void MarkUnused(BeamPoint point)
        {
            if (point == null)
            {
                return;
            }
            point.Index = -1;
            point.Kind = BeamPointKind.Unused;
            point.SetGrabbable(false);
        }

        /// @brief 作り置きの点の index 番目を返す。足りなければ作る
        private BeamPoint GetPoolPoint(int index, GameObject pointPrefab, float pointRadius, int pointLayer)
        {
            // 消された点（シーンの切り替えなど）は除く
            while (index < _pool.Count && _pool[index] == null)
            {
                _pool.RemoveAt(index);
            }
            if (index >= _pool.Count)
            {
                _pool.Add(CreatePoint(pointPrefab, pointRadius, pointLayer));
            }
            BeamPoint point = _pool[index];
            // 重力で落ちる状態のまま作り置きに残った点は、ビームが動かす状態に戻す
            if (point.IsDropped)
            {
                point.ResetPhysics();
            }
            if (point.OwnSphere != null && !Mathf.Approximately(point.OwnSphere.radius, pointRadius))
            {
                point.OwnSphere.radius = pointRadius;
            }
            return point;
        }

        /// @brief 点の Object を作る。コライダー、Rigidbody（Kinematic、重力なし）、BeamPoint がそろうようにする
        private BeamPoint CreatePoint(GameObject pointPrefab, float pointRadius, int pointLayer)
        {
            EnsureContainer();
            GameObject pointObject;
            if (pointPrefab != null)
            {
                pointObject = Object.Instantiate(pointPrefab, _staging, false);
                // ひな形が非アクティブでも、点はアクティブにする（親が非アクティブなので、部品の Awake はまだ動かない）
                pointObject.SetActive(true);
            }
            else
            {
                pointObject = new GameObject();
                pointObject.layer = pointLayer;
                pointObject.transform.SetParent(_staging, false);
            }
            pointObject.name = string.Format(PointNameFormat, _createdCount);
            ++_createdCount;

            // 非アクティブな親の下にいる間に整えるので、プレハブの部品（念力の対象の印など）の Awake は、整えた後の状態を見る
            if (!pointObject.TryGetComponent(out Rigidbody pointRigidbody))
            {
                pointRigidbody = pointObject.AddComponent<Rigidbody>();
            }
            pointRigidbody.isKinematic = true;
            pointRigidbody.useGravity = false;

            SphereCollider ownSphere = null;
            if (pointObject.GetComponentInChildren<Collider>(true) == null)
            {
                ownSphere = pointObject.AddComponent<SphereCollider>();
                ownSphere.isTrigger = true;
                ownSphere.radius = pointRadius;
                // 念力のレイで選ばれるためだけの球なので、ほかのコライダー（隣の点やプレイヤーのトリガー）とは重なりの組を作らない
                // （レイの判定には効かないので、選ぶのには影響しない）
                ownSphere.excludeLayers = ~0;
            }
            if (!pointObject.TryGetComponent(out BeamPoint point))
            {
                point = pointObject.AddComponent<BeamPoint>();
            }
            point.OwnSphere = ownSphere;
            point.Initialize(_emitter);

            // ここで初めてアクティブになる
            pointObject.transform.SetParent(_container.transform, false);
            return point;
        }

        private void EnsureContainer()
        {
            if (_container != null)
            {
                return;
            }
            _container = new GameObject(string.Format(ContainerNameFormat, _emitter.name));
            Scene scene = _emitter.gameObject.scene;
            if (scene.IsValid() && scene != _container.scene)
            {
                SceneManager.MoveGameObjectToScene(_container, scene);
            }
            var staging = new GameObject(StagingName);
            staging.SetActive(false);
            staging.transform.SetParent(_container.transform, false);
            _staging = staging.transform;
        }

        //============================================================
        // 中で使う型
        //============================================================

        /// @brief 支点
        private sealed class Anchor
        {
            public Anchor(BeamPoint point, int legIndex, bool isEndAnchor, float pathS)
            {
                Point = point;
                LegIndex = legIndex;
                IsEndAnchor = isEndAnchor;
                PathS = pathS;
                IsHeld = true;
                Strength = 1.0f;
                HeldBendAlong = -1.0f;
            }

            public BeamPoint Point { get; }
            public int LegIndex { get; }               ///< 何回跳ね返った後の区間を曲げるか（掴んだときの点の区間）
            public bool IsEndAnchor { get; }           ///< 終わりの点を掴んでできた支点か
            public bool IsHeld { get; set; }           ///< 掴まれているか
            public Vector3 Position { get; set; }      ///< 離された位置（Redirect では、最後に組み立てたときの位置）。Bump は、離された後ここを通るように曲げる
            public float ReleasedSeconds { get; set; } ///< 離されてから数えた時間（秒）。どこかの点が掴まれている間は増えない
            public float LastUpdateTime { get; set; }  ///< 最後に ReleasedSeconds を数えた時刻（秒）
            public bool IsReturning { get; set; }      ///< 真っすぐに戻っている途中か
            public float PathS { get; set; }           ///< 区間の始めから、ビームに沿って支点までの道のり（m。最後に組み立てたとき）。支点を通る順と、直前の支点からの道のりに使う
            public bool HasShape { get; set; }         ///< Redirect で、曲がり（BendLength、BendAngle、BendAxis）を覚えているか。覚えていなければ、位置を通るように決める
            public float BendLength { get; set; }      ///< Redirect で、曲がり始めから支点までの、曲がる部分の道のり（m）。戻る間も変えない
            public float BendAngle { get; set; }       ///< Redirect で、曲がる角度（ラジアン、0 以上）。戻る間は、これに Strength を掛ける
            public Vector3 BendAxis { get; set; }      ///< Redirect で、曲がる軸。直前の支点を通った後の向きの基準から見た向き（右へ曲がる、上へ曲がる、など）
            public float HeldBendAlong { get; set; }   ///< Redirect で、掴まれている間の曲がり始めの、支点の手前の長さ（m）の下限。負なら Bend Range を使う
            public float ReturningU { get; set; }      ///< 戻り始めたときの、元の線での位置（m）
            public Vector3 ReturningHeight { get; set; }  ///< 戻り始めたときの山の高さ
            public Vector3 ReturningDirection { get; set; }  ///< 戻り始めたときの、その区間の元の線の向き
            public float Strength { get; set; }        ///< 戻っている途中の、山の高さ（Redirect では曲がる角度）に掛ける割合（1 から 0 へ減る）
            public bool IsReached { get; set; }        ///< 今のビームが、この支点まで届いたか
            public bool HasSolved { get; set; }        ///< 前の組み立てで、山の高さを解いたか（解いていなければ、その山は効いていない）
            public float SolvedU { get; set; }         ///< 最後に解いたときの、元の線での位置（m）
            public Vector3 SolvedHeight { get; set; }  ///< 最後に解いたときの山の高さ
            public Vector3 SolvedDirection { get; set; }  ///< 最後に解いたときの、その区間の元の線の向き
        }

        /// @brief 組み立て中の区間の支点
        private struct Knot
        {
            public Knot(float u, Vector3 offsetOrHeight, int anchorIndex, bool isConstraint, bool isHeld)
            {
                U = u;
                Offset = isConstraint ? offsetOrHeight : Vector3.zero;
                Height = isConstraint ? Vector3.zero : offsetOrHeight;
                AnchorIndex = anchorIndex;
                IsConstraint = isConstraint;
                IsHeld = isHeld;
                Position = Vector3.zero;
            }

            public float U;              ///< 元の線の、区間の始めからの長さ（m）
            public Vector3 Offset;       ///< 通る位置を決めた支点の、元の線からずらす量
            public Vector3 Height;       ///< 山の高さ
            public Vector3 Position;     ///< 支点の位置（曲げた後）
            public int AnchorIndex;      ///< 支点の番号
            public bool IsConstraint;    ///< 通る位置を決めた支点か（掴まれているか、離された支点）。false なら戻っている途中の支点
            public bool IsHeld;          ///< 掴まれている支点か
        }

        /// @brief Redirect の道のひとつ（真っすぐな線か、円の一部）
        private readonly struct PathPiece
        {
            public PathPiece(Vector3 start, Vector3 direction, Vector3 normal, float radius, float length, Vector3 end,
                int anchorIndex, bool isEndAtAnchor)
            {
                Start = start;
                Direction = direction;
                Normal = normal;
                Radius = radius;
                Length = length;
                End = end;
                AnchorIndex = anchorIndex;
                IsEndAtAnchor = isEndAtAnchor;
            }

            public Vector3 Start { get; }      ///< 始めの位置
            public Vector3 Direction { get; }  ///< 始めの向き
            public Vector3 Normal { get; }     ///< 円の中心へ向かう向き（真っすぐな線では 0）
            public float Radius { get; }       ///< 円の半径（m）。0 なら真っすぐな線
            public float Length { get; }       ///< 道のりの長さ（m）
            public Vector3 End { get; }        ///< 終わりの位置（支点で終わるなら、支点の位置そのもの）
            public int AnchorIndex { get; }    ///< 終わりが支点なら、その番号。そうでなければ -1
            public bool IsEndAtAnchor { get; } ///< この支点でビームを終えるか

            /// @brief 始めから道に沿って s だけ進んだ位置
            public Vector3 GetPosition(float s)
            {
                if (Radius <= 0.0f)
                {
                    return Start + Direction * s;
                }
                float angle = s / Radius;
                return Start + Normal * (Radius * (1.0f - Mathf.Cos(angle))) + Direction * (Radius * Mathf.Sin(angle));
            }
        }

        /// @brief 組み立て中のビームの点
        private readonly struct Vertex
        {
            public Vertex(Vector3 position, BeamPointKind kind, int legIndex, int anchorIndex, float pathU)
            {
                Position = position;
                Kind = kind;
                LegIndex = legIndex;
                AnchorIndex = anchorIndex;
                PathU = pathU;
            }

            public Vector3 Position { get; }
            public BeamPointKind Kind { get; }
            public int LegIndex { get; }
            public int AnchorIndex { get; }
            public float PathU { get; }  ///< 区間の始めからの長さ（m。PathSample.U）
        }
    }

    /// @brief 点と線のビームの、曲げた後の点の候補（BeamPointChain.BuildLeg が作る）
    internal readonly struct PathSample
    {
        public PathSample(float u, Vector3 position, int anchorIndex, bool isEnd)
        {
            U = u;
            Position = position;
            AnchorIndex = anchorIndex;
            IsEnd = isEnd;
        }

        public float U { get; }           ///< 元の線の、区間の始めからの長さ（m）。届く距離はこの長さで数える
        public Vector3 Position { get; }  ///< 曲げた後の位置
        public int AnchorIndex { get; }   ///< 支点なら、その番号。支点でなければ -1
        public bool IsEnd { get; }        ///< 区間の終わり（届く距離の端か、点の数の上限）か
    }
}
