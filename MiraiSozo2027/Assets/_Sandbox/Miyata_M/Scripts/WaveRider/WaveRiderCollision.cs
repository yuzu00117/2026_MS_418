using System.Collections.Generic;
using UnityEngine;

namespace Aura.Gimmick
{
    /// @brief WaveRider の当たり判定まわりの計算を受け持つ（自分の Collider の外形、壁からの押し戻し、回転の可否、上に乗った物の検出、壁の手前で止める移動）
    /// ・自分の Collider は、自分と子のものを使う。トリガーと無効なものは除く
    /// ・押し戻しと回転の可否は、同じ「横方向の食い込み」の計算を使う。縦に重ならない物（床）と、動く Rigidbody と、ほかの WaveRider は相手にしない
    internal sealed class WaveRiderCollision
    {
        private const float MinPushDistance = 0.001f;              ///< 壁から押し戻す重なりの最小（m）。これ以下は接しているだけとみなす
        private const float MinPushHorizontalSqrMagnitude = 0.25f; ///< 押し出しの向きの横成分の長さの 2 乗がこれ以下なら、ほぼ縦向きなので押し戻さない
        private const float ContactDistance = 0.001f;              ///< 当たった距離がこれ以下なら、すでに面に接しているとみなす（m）
        private const float MinSlideSqrMagnitude = 1.0e-8f;        ///< 面に沿って滑らせる量の長さの 2 乗がこれ以下なら、滑らせない
        private const float PassengerSlabHeight = 0.3f;            ///< 上面の上で乗っている物を探す、薄い箱の高さ（m）
        private const float TopContactTolerance = 0.1f;            ///< 相手の足元が自分の上面からこの高さ以内に潜っていても、上に乗っているとみなす（m）
        private const int CornerCount = 8;                         ///< 箱の角の数
        private const float MinIntoOverlapDot = 0.01f;             ///< 進む向きと押し出しの向きの内積がこれより負なら、重なっている壁へ入り込むとみなす（面に沿う動きは止めない）

        public const int OverlapBufferSize = 32;                   ///< 周りで同時に調べる Collider の最大数。超えた分は数えない

        private readonly Rigidbody _body;    ///< 自分の Rigidbody（Kinematic）
        private readonly Transform _owner;   ///< 自分の Transform（自分の子を相手にしないため）
        private readonly Collider[] _overlapBuffer = new Collider[OverlapBufferSize];  ///< 重なりを受ける入れ物（確保を避ける）
        private Collider[] _ownColliders;    ///< 自分と子の Collider

        public WaveRiderCollision(Rigidbody body, Transform owner)
        {
            _body = body;
            _owner = owner;
            RefreshColliders();
        }

        /// @brief 自分と子の Collider を取り直す。子の Collider が増えたり減ったりしたときに呼ぶ
        public void RefreshColliders()
        {
            _ownColliders = _owner.GetComponentsInChildren<Collider>();
        }

        /// @brief 自分の（トリガーでない）当たり判定をまとめた、軸平行の箱を求める
        /// @return 使える当たり判定があれば true
        /// 計算量: O(C)（C は自分の当たり判定の数）
        public bool TryGetOwnBounds(out Bounds bounds)
        {
            bounds = default;
            bool hasBounds = false;
            foreach (Collider own in _ownColliders)
            {
                if (!IsSolid(own))
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
        public float GetFrontExtent(Vector3 direction)
        {
            if (!TryGetOwnBounds(out Bounds bounds))
            {
                return 0.0f;
            }

            Vector3 absDirection = new Vector3(Mathf.Abs(direction.x), Mathf.Abs(direction.y), Mathf.Abs(direction.z));
            float extent = Vector3.Dot(bounds.center - _body.position, direction) + Vector3.Dot(bounds.extents, absDirection);
            return Mathf.Max(0.0f, extent);
        }

        /// @brief next へ動き、中心まわりに turnDelta だけ回ったとき、壁などに食い込んでいたら、横方向だけ押し戻した位置を返す
        /// 波の線は壁の面（反射する所）を通るため、中心が線の上だと壁に半分食い込む。その分を壁から離す
        /// 波を出しているもの（砲台など）は、線の始まりで必ず重なるので押し戻さない
        /// 自分の Collider が複数あっても同じ壁を二重に押さないよう、Collider ごとの押し戻しのうち一番大きいものだけを使う
        /// 計算量: O(C × K)（C は自分の Collider の数、K は重なった Collider の数）
        public Vector3 PushOutOfWalls(Vector3 next, Quaternion turnDelta, Transform emitter)
        {
            Vector3 push = Vector3.zero;
            foreach (Collider own in _ownColliders)
            {
                if (!IsSolid(own))
                {
                    continue;
                }
                GetPenetration(own, next, turnDelta, emitter, null, out Vector3 ownPush);
                if (ownPush.sqrMagnitude > push.sqrMagnitude)
                {
                    push = ownPush;
                }
            }
            return next + push;
        }

        /// @brief 今の位置で中心まわりに delta だけ回ると、壁などに横から食い込むか
        /// 位置の移動は壁の手前で止めるが、回転は別なので、壁際で回って角が食い込まないよう調べる。上に乗っている物は相手にしない
        /// 計算量: O(C × K)（C は自分の Collider の数、K は重なった Collider の数）
        public bool IsRotationBlocked(Quaternion delta, HashSet<Rigidbody> passengers)
        {
            foreach (Collider own in _ownColliders)
            {
                if (IsSolid(own) && GetPenetration(own, _body.position, delta, null, passengers, out Vector3 _))
                {
                    return true;
                }
            }
            return false;
        }

        /// @brief 自分の上面のすぐ上にある Rigidbody を、乗っている物として passengers に集める（passengers は先に空にする）
        /// 接触の通知（OnCollisionStay）は、相手が眠ると届かず、運び損ねて滑るので、毎ステップ上面を直接調べる
        /// @return 上面の Collider が入れ物の数に達した（取りこぼした恐れがある）なら true
        /// 計算量: O(C + K)（C は自分の Collider の数、K は上面で重なった Collider の数）
        public bool DetectPassengers(HashSet<Rigidbody> passengers)
        {
            passengers.Clear();
            if (!TryGetOwnBounds(out Bounds bounds))
            {
                return false;
            }

            // 回っているときに軸平行の箱で探すと、角の外（上面の外）の物まで拾うので、自分の向き（Y 軸まわり）に合わせた箱で探す
            Quaternion yaw = Quaternion.Euler(0.0f, _body.rotation.eulerAngles.y, 0.0f);
            Bounds local = GetOwnLocalBounds(yaw);
            float slabCenterY = bounds.max.y + PassengerSlabHeight * 0.5f - TopContactTolerance;
            Vector3 localCenter = new Vector3(local.center.x, 0.0f, local.center.z);
            Vector3 center = _body.position + yaw * localCenter;
            center.y = slabCenterY;
            Vector3 halfExtents = new Vector3(local.extents.x, PassengerSlabHeight * 0.5f, local.extents.z);
            int count = Physics.OverlapBoxNonAlloc(center, halfExtents, _overlapBuffer, yaw, ~0, QueryTriggerInteraction.Ignore);
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
                    passengers.Add(other);
                }
            }
            return count >= OverlapBufferSize;
        }

        /// @brief step だけ進めた位置を返す。壁などに当たるなら、その手前までにする。上に乗っている物と、動く Rigidbody は当たり判定から外す
        /// すでに面に接している所（床の上など）で止まってしまわないよう、接している面に沿って 1 回だけ滑らせる
        /// 計算量: O(H)（H は進む先の当たり判定の数）
        public Vector3 SweepMove(Vector3 current, Vector3 step, HashSet<Rigidbody> passengers, out bool isBlocked)
        {
            Vector3 next = SweepOnce(current, step, passengers, out isBlocked, out Vector3 contactNormal, out float hitDistance);
            if (isBlocked && hitDistance < ContactDistance)
            {
                Vector3 slide = Vector3.ProjectOnPlane(step, contactNormal);
                if (slide.sqrMagnitude > MinSlideSqrMagnitude)
                {
                    next = SweepOnce(current, slide, passengers, out isBlocked, out _, out _);
                }
            }
            return next;
        }

        /// @brief 自分の（トリガーでない）当たり判定をまとめた箱を、自分の位置と yaw の向きから見た座標で求める（呼ぶ前に、使える当たり判定があることを確かめておく）
        /// BoxCollider は 8 つの角を使うので、回っていても上面の形どおりになる。ほかの形は、ワールドの軸平行の箱の 8 つの角で代える（少し大きめになる）
        /// 計算量: O(C)（C は自分の当たり判定の数。1 つにつき角 8 つ）
        private Bounds GetOwnLocalBounds(Quaternion yaw)
        {
            Quaternion toLocal = Quaternion.Inverse(yaw);
            Vector3 origin = _body.position;
            Bounds local = default;
            bool hasBounds = false;
            foreach (Collider own in _ownColliders)
            {
                if (!IsSolid(own))
                {
                    continue;
                }
                BoxCollider box = own as BoxCollider;
                Vector3 center = box != null ? box.center : own.bounds.center;
                Vector3 extents = box != null ? box.size * 0.5f : own.bounds.extents;
                for (int corner = 0; corner < CornerCount; corner++)
                {
                    Vector3 offset = new Vector3(
                        (corner & 1) == 0 ? -extents.x : extents.x,
                        (corner & 2) == 0 ? -extents.y : extents.y,
                        (corner & 4) == 0 ? -extents.z : extents.z);
                    Vector3 world = box != null ? box.transform.TransformPoint(center + offset) : center + offset;
                    Vector3 point = toLocal * (world - origin);
                    if (hasBounds)
                    {
                        local.Encapsulate(point);
                    }
                    else
                    {
                        local = new Bounds(point, Vector3.zero);
                        hasBounds = true;
                    }
                }
            }
            return local;
        }

        /// @brief 今の位置で自分がもう重なっている壁などへ、direction の向きでさらに入り込むか
        /// 相手にする物は SweepOnce と同じ（動く Rigidbody、ほかの WaveRider、上に乗っている物、自分の子は除く）
        /// @param normal 入り込む相手から押し出される向き（入り込むときだけ意味がある）
        /// 計算量: O(C × K)（C は自分の Collider の数、K は重なった Collider の数）
        private bool IsMovingIntoOverlap(Vector3 direction, HashSet<Rigidbody> passengers, out Vector3 normal)
        {
            normal = Vector3.zero;
            foreach (Collider own in _ownColliders)
            {
                if (!IsSolid(own))
                {
                    continue;
                }
                Bounds ownBounds = own.bounds;
                int count = Physics.OverlapBoxNonAlloc(ownBounds.center, ownBounds.extents, _overlapBuffer, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    Collider other = _overlapBuffer[i];
                    Rigidbody otherBody = other.attachedRigidbody;
                    if (other.transform.IsChildOf(_owner)
                        || (otherBody != null && (!otherBody.isKinematic || IsRider(otherBody) || passengers.Contains(otherBody))))
                    {
                        continue;
                    }
                    if (Physics.ComputePenetration(own, own.transform.position, own.transform.rotation,
                            other, other.transform.position, other.transform.rotation, out Vector3 pushDirection, out float distance)
                        && distance > MinPushDistance
                        && Vector3.Dot(direction, pushDirection) < -MinIntoOverlapDot)
                    {
                        normal = pushDirection;
                        return true;
                    }
                }
            }
            return false;
        }

        /// ほかの WaveRider か。WaveRider どうしは押し戻さず、止めもしない（互いに全量押し戻すと、離れすぎては戻るのを繰り返して震えるため）
        /// 計算量: O(1)（その Rigidbody の GameObject のコンポーネント数に比例）
        private static bool IsRider(Rigidbody body)
        {
            return body.TryGetComponent<WaveRider>(out _);
        }

        /// 押し戻しや外形に使える Collider か（消えていない、有効、トリガーでない）
        private static bool IsSolid(Collider collider)
        {
            return collider != null && collider.enabled && !collider.isTrigger;
        }

        /// @brief 自分の Collider 1 つを、basePosition へ動かして中心まわりに delta だけ回したとき、周りから受ける横方向の押し戻しを求める
        /// @param ignoredRoot この子の Collider は相手にしない（null なら無し）
        /// @param ignoredBodies この Rigidbody の Collider は相手にしない（null なら無し）
        /// @return 横方向に食い込んでいる相手が 1 つでもあれば true
        /// 計算量: O(K)（K は重なった Collider の数）
        private bool GetPenetration(Collider own, Vector3 basePosition, Quaternion delta, Transform ignoredRoot, HashSet<Rigidbody> ignoredBodies, out Vector3 push)
        {
            push = Vector3.zero;
            bool isPenetrating = false;
            Vector3 bodyPosition = _body.position;
            // 回りながら進むときは、回ったあとの向きで調べる（回る途中の角が壁に食い込むため）
            Vector3 ownPosition = basePosition + delta * (own.transform.position - bodyPosition);
            Quaternion ownRotation = delta * own.transform.rotation;
            Vector3 ownCenter = basePosition + delta * (own.bounds.center - bodyPosition);
            float reach = own.bounds.extents.magnitude;   // 回っても収まるよう、どの向きにも同じ広さで探す
            int count = Physics.OverlapBoxNonAlloc(ownCenter, Vector3.one * reach, _overlapBuffer, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider other = _overlapBuffer[i];
                Rigidbody otherBody = other.attachedRigidbody;
                if (other.transform.IsChildOf(_owner)
                    || (ignoredRoot != null && other.transform.IsChildOf(ignoredRoot))
                    || (otherBody != null && (!otherBody.isKinematic || IsRider(otherBody) || (ignoredBodies != null && ignoredBodies.Contains(otherBody)))))
                {
                    continue;
                }

                // 縦に重なっていない物（床など）は相手にしない
                float overlapY = Mathf.Min(ownCenter.y + own.bounds.extents.y, other.bounds.max.y) - Mathf.Max(ownCenter.y - own.bounds.extents.y, other.bounds.min.y);
                if (overlapY <= MinPushDistance)
                {
                    continue;
                }

                // 背の高い壁だと、一番浅い押し出しは「真上」になってしまう。高さを相手の中心に揃えて、横向きの押し出しを出させる
                Vector3 levelPosition = ownPosition + Vector3.up * (other.bounds.center.y - ownCenter.y);
                if (Physics.ComputePenetration(own, levelPosition, ownRotation,
                        other, other.transform.position, other.transform.rotation, out Vector3 direction, out float distance))
                {
                    direction.y = 0.0f;
                    if (distance > MinPushDistance && direction.sqrMagnitude > MinPushHorizontalSqrMagnitude)
                    {
                        push += direction / direction.sqrMagnitude * distance;   // 横向きの分だけで、押し出す長さを割り戻す
                        isPenetrating = true;
                    }
                }
            }
            return isPenetrating;
        }

        /// @brief step だけ進めた位置を返す。壁などに当たるなら、その手前までにして、当たった面の向きと距離も返す
        /// SweepTestAll は配列を作るが、波に乗っていない間だけなので許容する
        /// 計算量: O(H)（H は進む先の当たり判定の数）
        private Vector3 SweepOnce(Vector3 current, Vector3 step, HashSet<Rigidbody> passengers, out bool isBlocked, out Vector3 contactNormal, out float hitDistance)
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
            // SweepTestAll は、動き始めにもう重なっている物を返さない。重なっている壁へさらに入り込む向きなら、ここで止める
            if (IsMovingIntoOverlap(direction, passengers, out Vector3 overlapNormal))
            {
                isBlocked = true;
                contactNormal = overlapNormal;
                hitDistance = 0.0f;
                return current;
            }
            foreach (RaycastHit hit in _body.SweepTestAll(direction, stepLength, QueryTriggerInteraction.Ignore))
            {
                // 動く Rigidbody（箱、プレイヤーなど）では止まらず、物理で押しのける（乗っている間と同じ）。止まると、どかない限りずっと戻れない
                Rigidbody hitBody = hit.rigidbody;
                // ほかの WaveRider とは、乗っている間と同じく、ぶつからずにすり抜ける
                if (hitBody != null && (!hitBody.isKinematic || IsRider(hitBody) || passengers.Contains(hitBody)))
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
    }
}
