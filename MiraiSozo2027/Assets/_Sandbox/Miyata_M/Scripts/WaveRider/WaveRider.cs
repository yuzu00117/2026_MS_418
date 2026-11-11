using System.Collections.Generic;
using Aura.Inspector;
using Aura.ObjectInfo;
using Aura.Wave;
using UnityEngine;

namespace Aura.Gimmick
{
    /// @brief タレットが出す波（ビームの線）に運ばれるオブジェクト。上に乗った物（プレイヤーを含む）も一緒に運ぶ
    /// ・反応する波の種類は WaveReaction（電磁波/超音波のチェック）で決める。反応しない波では動かず、波が無いときと同じ扱いになる
    /// ・波が出ている間は常に線の上にいて、線の上の道のり s を一定の速さで進める。波が曲がっても跳ね返っても、s を保ったまま線に沿う
    /// ・波が無くなったら、ゲーム開始時の位置へ「戻る速さ」で直線に戻る。戻る途中で波が来たら、今の位置に一番近い線の上の点から乗り直す
    /// ・Kinematic な Rigidbody として物理ステップ（FixedUpdate）で動く。上に乗った Rigidbody には、同じ移動量を足して運ぶ
    ///   （プレイヤーは毎ステップ水平の速度を入力で上書きするため、摩擦では運ばれない）
    /// ・波の線は WaveEmitter.BeamCorners から読む。WaveEmitter が LateUpdate で更新した、直前のフレームの線を使う
    /// ・当たり判定は、波を止めないレイヤーの子に付ける（同じ階層に付けると、ビームが自分の面で止まって線が縮む）
    [DisallowMultipleComponent]
    [RequireComponent(typeof(WaveReaction))]
    [RequireComponent(typeof(Rigidbody))]
    public sealed class WaveRider : MonoBehaviour
    {
        private const float MinDirectionSqrMagnitude = 0.25f; ///< 進む向きの長さの 2 乗がこれより小さければ、向きが取れなかったとみなす
        private const float TurnDegreesPerSecond = 720.0f;    ///< 進む向きに回転を合わせるときの、1 秒あたりの最大の回転（度）
        private const float TopContactTolerance = 0.1f;       ///< 接触点がこの高さ以内に相手の足元があれば、上に乗っているとみなす（m）

        [InspectorLabel("乗る波を出すもの")]
        [Tooltip("乗る波を出す WaveEmitter（タレットなど）。空なら、反応する波を出している中で、今の位置から一番近い線に乗る")]
        [SerializeField] private WaveEmitter _emitter;        ///< 乗る波を出すもの。空なら近い線を探す

        [InspectorLabel("運ばれる速さ（m/s）")]
        [Tooltip("波の線に沿って進む速さ（m/s）。曲がった所の角も、道のりにそのまま数える。0 なら乗っても進まない")]
        [SerializeField, Min(0.0f)] private float _carrySpeed = 2.0f;  ///< 波に運ばれる速さ（m/s）

        [InspectorLabel("戻る速さ（m/s）")]
        [Tooltip("波が無くなったとき、ゲーム開始時の位置へ戻る速さ（m/s）。0 なら戻らず、その場で止まる")]
        [SerializeField, Min(0.0f)] private float _returnSpeed = 2.0f;  ///< 初期位置へ戻る速さ（m/s）

        [InspectorLabel("線からの高さのずれ（m）")]
        [Tooltip("波の線から、ワールドの真上へずらす量（m）。線の上に乗せるときの、足元の高さ合わせに使う")]
        [SerializeField] private float _heightOffset;           ///< 線から真上へずらす量（m）

        [InspectorLabel("進む向きに回転を合わせる")]
        [Tooltip("オンにすると、乗っている間、線の進む向き（Y 軸まわり）を正面に向ける。曲がった所では少しずつ向きを変える\n" +
                 "上に乗った物は回さない（位置だけ運ぶ）")]
        [SerializeField] private bool _isFacingTravelDirection;  ///< 進む向きに回転を合わせるか

        [InspectorLabel("終点に着いたとき")]
        [Tooltip("線の終点に着いたときの動き。折り返すと、先頭まで戻ってまた終点へ向かう（波が出ている間、往復する）")]
        [SerializeField] private WaveRiderEndMode _endMode = WaveRiderEndMode.Stop;  ///< 終点に着いたときの動き

        [InspectorLabel("上の物を運ぶ")]
        [Tooltip("オンにすると、上に乗っている Rigidbody（プレイヤーなど）を、自分と同じ移動量だけ一緒に運ぶ\n" +
                 "プレイヤーを乗せるなら、オンのままにする")]
        [SerializeField] private bool _isCarryingPassengers = true;  ///< 上に乗った物を運ぶか

        private WaveReaction _reaction;       ///< 反応する波の種類
        private Rigidbody _body;              ///< 自分の Rigidbody（Kinematic）
        private Vector3 _initialPosition;     ///< ゲーム開始時の位置（ワールド座標）
        private WaveEmitter _currentEmitter;  ///< 今乗っている波を出しているもの。乗っていなければ null
        private float _distance;              ///< 今の線の上の道のり s（m）
        private float _direction = 1.0f;      ///< 線の上を進む向き（1: 終点へ、-1: 先頭へ。折り返しで使う）

        private readonly HashSet<Rigidbody> _passengers = new HashSet<Rigidbody>();  ///< 直前の物理ステップで上に乗っていた物

        /// @brief 波に乗っているか
        public bool IsRiding => _currentEmitter != null;

        private void Awake()
        {
            _reaction = GetComponent<WaveReaction>();
            _body = GetComponent<Rigidbody>();
            _body.isKinematic = true;
            _body.interpolation = RigidbodyInterpolation.Interpolate;
            _initialPosition = _body.position;
        }

        private void OnDisable()
        {
            _passengers.Clear();
        }

        private void FixedUpdate()
        {
            float deltaTime = Time.fixedDeltaTime;
            Vector3 current = _body.position;
            WaveEmitter emitter = FindEmitter();

            Vector3 next;
            if (emitter == null)
            {
                _currentEmitter = null;
                _direction = 1.0f;
                next = Vector3.MoveTowards(current, _initialPosition, _returnSpeed * deltaTime);
            }
            else
            {
                // 乗り直すとき（別の線に変わったときも）は、今の位置に一番近い線の上の点から始める
                if (emitter != _currentEmitter)
                {
                    _currentEmitter = emitter;
                    _distance = WaveLinePath.Project(emitter.BeamCorners, GetLinePosition(current), out _);
                    _direction = 1.0f;
                }
                next = Ride(emitter, deltaTime);
            }

            CarryPassengers(next - current);
            _body.MovePosition(next);
        }

        // 上に乗った Rigidbody を覚える。物理の計算の後に呼ばれるので、運ぶのは次の FixedUpdate
        // （子の Collider の接触も、この Rigidbody のオブジェクトに届く）
        private void OnCollisionStay(Collision collision)
        {
            Rigidbody other = collision.rigidbody;
            if (!_isCarryingPassengers || other == null || other.isKinematic)
            {
                return;
            }

            float footHeight = collision.collider.bounds.min.y;
            for (int i = 0; i < collision.contactCount; i++)
            {
                if (footHeight >= collision.GetContact(i).point.y - TopContactTolerance)
                {
                    _passengers.Add(other);
                    return;
                }
            }
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
            return emitter.isActiveAndEnabled
                && emitter.IsFiring
                && emitter.FireShape == WaveFireShape.Beam
                && source != null
                && emitter.BeamCorners.Count >= 2
                && _reaction.CanReactTo(source.WaveKind);
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
            float length = WaveLinePath.GetLength(corners);

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
            return point + Vector3.up * _heightOffset;
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
            _body.MoveRotation(Quaternion.RotateTowards(_body.rotation, target, TurnDegreesPerSecond * deltaTime));
        }

        /// @brief 上に乗っていた物を、自分の移動量と同じだけ動かす。動かしたら覚えた分は捨てる
        /// 位置を直接足す（MovePosition は、プレイヤー自身の歩きと食い合うため使わない）
        /// 計算量: O(P)（P は乗っている物の数）
        private void CarryPassengers(Vector3 movement)
        {
            if (_passengers.Count == 0)
            {
                return;
            }
            if (movement.sqrMagnitude > 0.0f)
            {
                foreach (Rigidbody passenger in _passengers)
                {
                    if (passenger != null)
                    {
                        passenger.position += movement;
                    }
                }
            }
            _passengers.Clear();
        }
    }
}
