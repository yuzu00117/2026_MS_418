using UnityEngine;

public class Enemy : Entity, IArmored
{
    public bool IsArmored { get; private set; }

    public void SetArmored(bool isArmored)
    {
        IsArmored = isArmored;
    }

    protected override bool IsDamageBlocked => IsArmored;

    public override void Kill()
    {
        IsArmored = false;
        base.Kill();
    }

    protected override void OnDeath()
    {
        Destroy(gameObject);
    }
}