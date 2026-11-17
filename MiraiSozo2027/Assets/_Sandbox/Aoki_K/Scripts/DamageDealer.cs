using UnityEngine;

public class DamageDealer : MonoBehaviour
{
    [SerializeField, Min(1)] private int _damage = 10;

    [Tooltip("trueの場合、ダメージを与えた相手を即死させる")]
    [SerializeField] private bool _instantKill = false;

    [Tooltip("ダメージを与える対象のレイヤー")]
    [SerializeField] private LayerMask _targetLayers = ~0;
        private void OnTriggerEnter(Collider other)
    {
        TryDealDamage(other);
    }

    private void OnTriggerStay(Collider other)
    {
        TryDealDamage(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        TryDealDamage(collision.collider);
    }

    private void OnCollisionStay(Collision collision)
    {
        TryDealDamage(collision.collider);
    }

    private void TryDealDamage(Collider other)
    {
        if (other == null) return;
        if ((_targetLayers.value & (1 << other.gameObject.layer)) == 0) return;

        // 当たったコライダー自身、Rigidbody、または親オブジェクトから IDamageable を探す
        if (!other.TryGetComponent<IDamageable>(out var damageable))
        {
            damageable = other.GetComponentInParent<IDamageable>();
        }

        if (damageable == null || damageable.IsDead) return;

        if (_instantKill)
        {
            damageable.Kill();
        }
        else
        {
            damageable.TakeDamage(_damage);
        }
    }
}