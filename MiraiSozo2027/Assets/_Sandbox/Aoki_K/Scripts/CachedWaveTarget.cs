/// <summary>
/// CachedTarget を拡張し、WaveDamageReceiver も保持する構造体
/// </summary>
public readonly struct CachedWaveTarget : ICachedTarget
{
    public readonly CachedTarget BaseTarget;
    public readonly WaveDamageReceiver Receiver;
    public readonly bool WasWaveTarget;

    public CachedWaveTarget(WaveDamageReceiver receiver, IDamageable damageable)
    {
        Receiver = receiver;
        BaseTarget = new CachedTarget(damageable);
        WasWaveTarget = receiver != null || BaseTarget.WasDamageTarget;
    }

    public bool HasValidReceiver => Receiver != null;
    public bool HasValidDamageable => BaseTarget.HasValidDamageable;
    public IDamageable Damageable => BaseTarget.Damageable;
    public bool IsTargetLost => WasWaveTarget && !HasValidReceiver && !HasValidDamageable;
}
