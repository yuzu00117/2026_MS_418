using UnityEngine;

namespace Aura.Combat
{
    /// <summary>
    /// IDamageable 用の基本キャッシュ構造体（DamageDealer で使用、CachedWaveTarget のベースとしても流用）
    /// </summary>
    public readonly struct CachedTarget : ICachedTarget
    {
        public readonly IDamageable Damageable;
        public readonly Object DamageableObject; // インターフェースのDestroy判定用
        public readonly bool WasDamageTarget;    // 登録時にダメージ対象だったか（壁・床との区別用）

        public CachedTarget(IDamageable damageable)
        {
            Damageable = damageable;
            DamageableObject = damageable as Object;
            WasDamageTarget = DamageableObject != null;
        }

        public bool HasValidDamageable => DamageableObject != null && !Damageable.IsDead;
        public bool IsTargetLost => WasDamageTarget && !HasValidDamageable;
    }
}
