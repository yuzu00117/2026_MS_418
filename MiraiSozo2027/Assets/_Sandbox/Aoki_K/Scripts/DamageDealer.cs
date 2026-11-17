using UnityEngine;

public class DamageDealer : MonoBehaviour
{
    [SerializeField, Min(1)] private int _damage = 10;
    [SerializeField] private LayerMask _targetLayers = ~0;
    private void OnTriggerEnter(Collider other)
    {
        if ((_targetLayers.value & (1 << other.gameObject.layer)) == 0) return;
        // 相手のコライダー（または親オブジェクト）が IDamageable を持っていればダメージを与える
        if (other.TryGetComponent<IDamageable>(out var damageable) ||
            (other.attachedRigidbody != null && other.attachedRigidbody.TryGetComponent<IDamageable>(out damageable)))
        {
            damageable.TakeDamage(_damage);
        }
    }
}