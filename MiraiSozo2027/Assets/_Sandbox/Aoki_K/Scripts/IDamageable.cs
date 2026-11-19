namespace Aura.Combat
{
    public interface IDamageable
    {
        bool IsDead { get; }
        void TakeDamage(int damage);
        void Kill();
    }
}
