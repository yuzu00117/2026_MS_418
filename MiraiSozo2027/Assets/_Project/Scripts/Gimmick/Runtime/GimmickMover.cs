using System.Collections;
using System.Collections.Generic;
using Aura.Inspector;
using UnityEngine;

namespace Aura.Gimmick
{
    /// @brief 起動で決めた位置まで動き、止めると元の位置に戻るギミック（扉、リフトなど）
    /// 受け手（ReactionTrigger、古い WaveReceiver）の On Activated に Activate、On Deactivated に Deactivate をつなぐ
    /// 「動き方」を「起動中は往復する」にすると、起動している間ずっと往復する足場になる
    /// 「乗った物を運ぶ」を ON にすると、上に乗ったプレイヤーや箱も、動いた分だけ一緒に動く（横に動く足場に使う）
    /// 起動・停止・往復の切り替えは親の GimmickMotion にある
    public sealed class GimmickMover : GimmickMotion
    {
        private const int RiderBufferSize = 32;  ///< 乗った物を探すときに、一度に調べられる物の数
        private const float RiderGap = 0.1f;     ///< 物の下端と上面のすき間がこれ以下なら、乗っているとみなす（m）

        [InspectorLabel("起動で動く量（m）")]
        [Tooltip("起動したときに動く量（m）。ゲーム開始時の位置から、この分だけずらした位置まで動く\n" +
                 "向きは親の GameObject から見た向き（親がなければワールドの向き）。例: (0, 3, 0) なら 3 m 上がる")]
        [SerializeField] private Vector3 _activeOffset = new Vector3(0.0f, 3.0f, 0.0f);  ///< 起動したときに動く量（ローカル座標、m）

        [InspectorLabel("動く速さ（m/s）")]
        [Tooltip("動く速さ（m/s）。起動したときも、元の位置に戻るときも、この速さで動く。0.01 以上")]
        [SerializeField, Min(0.01f)] private float _speed = 2.0f;                        ///< 動く速さ（m/s）

        [InspectorLabel("乗った物を運ぶ")]
        [Tooltip("ON なら、上に乗った物（プレイヤーや箱など、Rigidbody のある物）も、動いた分だけ一緒に動かす。横に動く足場に使う\n" +
                 "OFF なら今までどおり、動くのはこの物だけ（上がるときは乗った物を押し上げるが、横には運ばない）\n" +
                 "跳んで離れている間は運ばない（動いている足場の上でその場で跳ぶと、足場だけが先へ進む）")]
        [SerializeField] private bool _canCarryRiders;                                   ///< 上に乗った物も一緒に動かすか

        private readonly List<Collider> _ownColliders = new List<Collider>();         ///< この物と子の当たり判定。乗った物を探すのに使う
        private readonly Collider[] _overlapBuffer = new Collider[RiderBufferSize];   ///< 上にある物を探すのに使う
        private readonly HashSet<Rigidbody> _riders = new HashSet<Rigidbody>();       ///< 上に乗っている物。当たり判定を 2 つ以上持つ物を 2 回動かさないよう、集めてから動かす
        private readonly WaitForFixedUpdate _waitForPhysics = new WaitForFixedUpdate();  ///< 物理の計算が終わるのを待つのに使う

        private Vector3 _inactivePosition;   ///< 止まっているときの位置（ローカル座標）
        private Vector3 _lastFixedPosition;  ///< 前の FixedUpdate のときの位置（ワールド座標）。乗った物を動かす量を求めるのに使う
        private Vector3 _pendingMovement;    ///< 物理の計算が終わったら、乗った物を横に動かす量（ワールド座標、m）
        private Coroutine _carryCoroutine;   ///< 物理の計算が終わるたびに乗った物を横に動かすコルーチン

        protected override void RememberInactivePose()
        {
            _inactivePosition = transform.localPosition;
        }

        protected override bool MoveStep(bool isHeadingToActive)
        {
            Vector3 target = isHeadingToActive ? _inactivePosition + _activeOffset : _inactivePosition;
            transform.localPosition = Vector3.MoveTowards(transform.localPosition, target, _speed * Time.deltaTime);
            return transform.localPosition == target;
        }

        private void OnEnable()
        {
            // 無効の間に動かされた分は運ばない
            _lastFixedPosition = transform.position;
            _pendingMovement = Vector3.zero;
            _riders.Clear();
            GetComponentsInChildren(true, _ownColliders);
            _carryCoroutine = StartCoroutine(CarryRidersAfterPhysics());
        }

        private void OnDisable()
        {
            // コンポーネントを無効にしただけではコルーチンが止まらないので止める（有効に戻したときに 2 つ動かないように）
            if (_carryCoroutine != null)
            {
                StopCoroutine(_carryCoroutine);
                _carryCoroutine = null;
            }
        }

        private void FixedUpdate()
        {
            // 動かすのは Update（GimmickMotion）なので、前の FixedUpdate から動いた分を、物理の 1 回分としてまとめて運ぶ
            // 「乗った物を運ぶ」が OFF の間も位置は覚えておく（途中で ON にしたときに、OFF の間に動いた分まで運ばないように）
            Vector3 position = transform.position;
            Vector3 movement = position - _lastFixedPosition;
            _lastFixedPosition = position;
            if (!_canCarryRiders || movement == Vector3.zero)
            {
                return;
            }
            // 乗った物は物理の計算の前に探す（この物も乗った物も、前の物理の計算のときの位置にそろっている）
            CollectRiders();
            // 下へ動いた分は、物理の計算の前に動かす（後で動かすと、計算の間にこの物だけが下がって乗った物が落ち始め、沈み込む）
            // 上へ動いた分は運ばない（物理の計算で、この物が乗った物を押し上げる。運ぶと 2 回分上がって浮く）
            if (movement.y < 0.0f)
            {
                MoveRiders(new Vector3(0.0f, movement.y, 0.0f));
            }
            // 横に動いた分は、物理の計算の後に動かす（CarryRidersAfterPhysics）
            _pendingMovement += new Vector3(movement.x, 0.0f, movement.z);
        }

        /// @brief 物理の計算が終わるたびに、FixedUpdate で見つけた乗った物を横に動かす
        /// 物理の計算の前に動かすと、その分が補間（Interpolate）に入らず、乗った物が物理の 1 回ごとにしか動かないように見える（フレームレートが高いとかくつく）
        /// 計算の後に MovePosition で動かすと、補間に入ってなめらかに見える
        private IEnumerator CarryRidersAfterPhysics()
        {
            while (true)
            {
                yield return _waitForPhysics;
                if (_pendingMovement != Vector3.zero)
                {
                    MoveRiders(_pendingMovement);
                }
                _pendingMovement = Vector3.zero;
                _riders.Clear();
            }
        }

        /// @brief 上に乗っている物を、決めた量だけ動かす
        /// プレイヤー（PlayerController）は FixedUpdate ごとに横の速さを入力で上書きするので、速さを足すのではなく位置をずらす
        /// （速さを足すやり方は、止まっている扱いのこの物との摩擦で遅れ、乗った物が少しずつずれる）
        /// @param movement 動かす量（ワールド座標、m）
        private void MoveRiders(Vector3 movement)
        {
            foreach (Rigidbody rider in _riders)
            {
                // 見つけてから動かすまでの間に消えたり、Kinematic になったりした物は動かさない
                if (rider != null && !rider.isKinematic)
                {
                    rider.MovePosition(rider.position + movement);
                }
            }
        }

        /// @brief 上に乗っている物（Kinematic でない Rigidbody のある物）を _riders に集める
        /// 物理の世界では、この物はまだ前の物理の計算のときの位置にある（Transform を変えた分は、次の物理の計算の前に写される）
        /// そのため、乗った物と同じ時点の位置どうしで調べられる
        private void CollectRiders()
        {
            foreach (Collider own in _ownColliders)
            {
                if (own == null || !own.enabled || own.isTrigger || !own.gameObject.activeInHierarchy)
                {
                    continue;
                }
                // 当たり判定を囲む箱を、上へ「乗っているとみなすすき間」の分だけ伸ばし、その中にある物を候補にする
                Bounds bounds = own.bounds;
                Vector3 halfRise = Vector3.up * (RiderGap * 0.5f);
                int count = Physics.OverlapBoxNonAlloc(bounds.center + halfRise, bounds.extents + halfRise, _overlapBuffer,
                                                       Quaternion.identity, Physics.AllLayers, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; ++i)
                {
                    Collider other = _overlapBuffer[i];
                    Rigidbody body = other.attachedRigidbody;
                    // Kinematic の物は、スクリプトやアニメーションが位置を決めているので動かさない
                    if (body == null || body.isKinematic || other.transform.IsChildOf(transform))
                    {
                        continue;
                    }
                    if (IsStandingOn(other, own))
                    {
                        _riders.Add(body);
                    }
                }
            }
        }

        /// @brief その当たり判定が、足場の当たり判定の上に立っているか
        /// 下端の真ん中のすぐ下に足場があれば立っているとみなす。横から触れているだけの物や、真ん中が縁の外へ出た物は運ばない
        private static bool IsStandingOn(Collider rider, Collider platform)
        {
            Bounds bounds = rider.bounds;
            Vector3 origin = new Vector3(bounds.center.x, bounds.min.y + RiderGap, bounds.center.z);
            return platform.Raycast(new Ray(origin, Vector3.down), out _, RiderGap * 2.0f);
        }
    }
}
