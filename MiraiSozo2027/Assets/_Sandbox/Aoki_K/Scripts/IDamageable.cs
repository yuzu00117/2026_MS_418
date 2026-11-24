namespace Aura.Combat
{
    public interface IDamageable
    {
        bool IsDead { get; }
        void TakeDamage(int damage = 1);
        void Kill();
    }
}
