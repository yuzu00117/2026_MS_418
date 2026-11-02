using System.Collections.Generic;
using Aura.Inspector;
using UnityEngine;
using UnityEngine.Events;

namespace Aura.Gimmick
{
    /// @brief 上に載った物の重さ（Rigidbody の Mass の合計）が決めた重さに届くと起動する床のスイッチ
    /// 念力で箱を運んで載せる、などに使う。「起動したときの処理」に GimmickMover の Activate などをつなぐ
    /// 載った物は、この GameObject の「トリガー」にしたコライダーで数える。床の上に少しはみ出すように置く
    /// （乗るための床のコライダーは別に要る。子の GameObject に付けてよい）
    /// 数えるのは Rigidbody のある物だけ。プレイヤーも Rigidbody があるので数える。数えたくなければ「数えるレイヤー」から外す
    public sealed class PressurePlate : MonoBehaviour
    {
        [InspectorLabel("起動に必要な重さ")]
        [Tooltip("載った物の Rigidbody の Mass の合計が、これ以上になると起動する。下回ると止まる")]
        [SerializeField, Min(0.01f)] private float _requiredMass = 5.0f;  ///< 起動に必要な Mass の合計

        [InspectorLabel("数えるレイヤー")]
        [Tooltip("載った物のうち、数えるレイヤー。外したレイヤーの物は載っても数えない")]
        [SerializeField] private LayerMask _countedLayers = ~0;           ///< 数えるレイヤー

        [InspectorLabel("起動したときの処理")]
        [Tooltip("載った物の重さが「起動に必要な重さ」に届いたときに呼ぶ処理。扉を開けるなど")]
        [SerializeField, FilteredEventMenu] private UnityEvent _onActivated = new UnityEvent();    ///< 起動したときに呼ぶ

        [InspectorLabel("止まったときの処理")]
        [Tooltip("物が降りて、重さが「起動に必要な重さ」を下回ったときに呼ぶ処理。扉を閉めるなど")]
        [SerializeField, FilteredEventMenu] private UnityEvent _onDeactivated = new UnityEvent();  ///< 止まったときに呼ぶ

        private readonly Dictionary<Collider, Rigidbody> _bodiesByCollider = new Dictionary<Collider, Rigidbody>();  ///< トリガーに入っているコライダーと、その物の Rigidbody
        private readonly HashSet<Rigidbody> _countedBodies = new HashSet<Rigidbody>();  ///< 数え直すときに、同じ物を 2 回数えないために使う
        private readonly List<Collider> _removedColliders = new List<Collider>();       ///< 消えたコライダーを外すときに使う

        /// @brief 今、載っている物の Mass の合計
        public float TotalMass { get; private set; }

        /// @brief 起動しているか
        public bool IsActive { get; private set; }

        private void OnTriggerEnter(Collider other)
        {
            if (!TryGetCountedBody(other, out Rigidbody body))
            {
                return;
            }
            _bodiesByCollider[other] = body;
            UpdateState();
        }

        private void OnTriggerExit(Collider other)
        {
            if (_bodiesByCollider.Remove(other))
            {
                UpdateState();
            }
        }

        private void FixedUpdate()
        {
            // 載ったまま消えたり、コライダーが無効になったりした物は OnTriggerExit が呼ばれないことがあるので、ここで外す
            // Mass が途中で変わることもあるので、毎回数え直す
            UpdateState();
        }

        private void OnDisable()
        {
            _bodiesByCollider.Clear();
            UpdateState();
        }

        /// @brief 数える物か調べ、その物の Rigidbody を返す
        /// @return 数える物なら true
        private bool TryGetCountedBody(Collider other, out Rigidbody body)
        {
            body = other.attachedRigidbody;
            if (other.isTrigger || body == null)
            {
                return false;
            }
            return (_countedLayers.value & (1 << other.gameObject.layer)) != 0;
        }

        /// @brief 重さを数え直し、起動したか止まったかを決める
        private void UpdateState()
        {
            float totalMass = 0.0f;
            _removedColliders.Clear();
            _countedBodies.Clear();
            foreach (KeyValuePair<Collider, Rigidbody> each in _bodiesByCollider)
            {
                Collider collider = each.Key;
                Rigidbody body = each.Value;
                // 1 つの物が複数のコライダーを持つことがあるので、物ごとに 1 回だけ数える
                if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy || body == null)
                {
                    _removedColliders.Add(collider);
                }
                else if (_countedBodies.Add(body))
                {
                    totalMass += body.mass;
                }
            }
            foreach (Collider each in _removedColliders)
            {
                _bodiesByCollider.Remove(each);
            }
            TotalMass = totalMass;

            bool isActive = TotalMass >= _requiredMass;
            if (isActive == IsActive)
            {
                return;
            }
            IsActive = isActive;
            if (IsActive)
            {
                _onActivated.Invoke();
            }
            else
            {
                _onDeactivated.Invoke();
            }
        }
    }
}
