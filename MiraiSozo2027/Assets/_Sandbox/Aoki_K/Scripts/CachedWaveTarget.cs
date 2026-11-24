namespace Aura.Combat
{
    /// <summary>
    /// WaveDamageReceiver 用のキャッシュ構造体（WaveDamageDealer で使用）
    /// </summary>
    public readonly struct CachedWaveTarget : ICachedTarget
    {
        public readonly WaveDamageReceiver Receiver;
        public readonly bool WasWaveTarget;

        public CachedWaveTarget(WaveDamageReceiver receiver)
        {
            Receiver = receiver;
            WasWaveTarget = receiver != null;
        }

        public bool HasValidReceiver => Receiver != null;
        public bool IsTargetLost => WasWaveTarget && !HasValidReceiver;
    }
}
