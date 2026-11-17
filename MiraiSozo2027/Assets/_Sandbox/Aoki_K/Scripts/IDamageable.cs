
public interface IDamageable
{
    bool IsDead { get; }
    void TakeDamage(int damage);
    void Kill();
}