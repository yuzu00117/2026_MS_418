using UnityEngine;

public class DamageDealer : DamageDealerBase<CachedTarget>
{
    [Tooltip("trueの場合、ダメージを与えた相手を即死させる")]
    [SerializeField] private bool _isInstantKill = false;

    private void OnTriggerEnter(Collider other) => TryDealDamage(other);
    private void OnTriggerStay(Collider other) => TryDealDamage(other);
    private void OnCollisionEnter(Collision collision) => TryDealDamage(collision.collider);
    private void OnCollisionStay(Collision collision) => TryDealDamage(collision.collider);

    protected override CachedTarget CreateCachedTarget(Collider hitCollider)
    {
        IDamageable damageable = hitCollider.GetComponentInParent<IDamageable>();
        return new CachedTarget(damageable);
    }

    private void TryDealDamage(Collider other)
    {
        if (!IsTargetLayer(other)) return;

        CachedTarget target = GetOrFindTarget(other);
        if (!target.HasValidDamageable) return;

        if (_isInstantKill)
        {
            target.Damageable.Kill();
        }
        else
        {
            target.Damageable.TakeDamage(_damage);
        }
    }
}