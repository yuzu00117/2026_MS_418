using Aura.Inspector;
using UnityEngine;

namespace Aura.Gimmick
{
    /// @brief 海などに落ちた物を、いちばん近い「戻す場所」へ戻す範囲
    /// 海の下に、横に広いトリガーの当たり判定（BoxCollider の Is Trigger を ON）と一緒に置く
    /// 入ってきた Rigidbody（プレイヤーや箱など）を、「戻す場所」のうち落ちた位置にいちばん近い所の少し上へ移し、速さを 0 にする
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public sealed class FallRecoveryZone : MonoBehaviour
    {
        [InspectorLabel("戻す場所")]
        [Tooltip("落ちた物を戻す場所。落ちた位置（横の位置）にいちばん近いものへ戻す。空なら何もしない")]
        [SerializeField] private Transform[] _recoveryPoints = new Transform[0];  ///< 落ちた物を戻す場所

        [InspectorLabel("戻す高さ（m）")]
        [Tooltip("戻す場所から、この分だけ上に出す。床に埋まらないようにする")]
        [SerializeField, Min(0.0f)] private float _heightOffset = 1.0f;  ///< 戻す場所から上に出す高さ（m）

        private void Reset()
        {
            GetComponent<Collider>().isTrigger = true;
        }

        private void OnTriggerEnter(Collider other)
        {
            Rigidbody body = other.attachedRigidbody;
            if (body == null || body.isKinematic)
            {
                return;
            }
            if (!TryGetNearestPoint(body.position, out Vector3 point))
            {
                return;
            }
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
            body.position = point + Vector3.up * _heightOffset;
            body.transform.position = body.position;
        }

        /// @brief 横の位置（XZ）がいちばん近い「戻す場所」を探す
        private bool TryGetNearestPoint(Vector3 position, out Vector3 outPoint)
        {
            outPoint = Vector3.zero;
            float bestDistance = float.PositiveInfinity;
            for (int i = 0; i < _recoveryPoints.Length; ++i)
            {
                Transform point = _recoveryPoints[i];
                if (point == null)
                {
                    continue;
                }
                Vector3 offset = point.position - position;
                offset.y = 0.0f;
                float distance = offset.sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    outPoint = point.position;
                }
            }
            return !float.IsPositiveInfinity(bestDistance);
        }
    }
}
