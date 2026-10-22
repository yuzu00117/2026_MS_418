namespace Echo.Echolocation
{
    public readonly struct EchoRevealSnapshot
    {
        public bool IsActive { get; }
        public bool HasActiveCollision { get; }
        public bool IsOperational { get; }
        public float RevealedFraction { get; }
        public uint Revision { get; }
        public int PendingPatchCount { get; }
        public EchoRevealSnapshot(bool isActive, bool hasActiveCollision, bool isOperational, float fraction, uint revision, int pending)
        { IsActive = isActive; HasActiveCollision = hasActiveCollision; IsOperational = isOperational;
          RevealedFraction = fraction; Revision = revision; PendingPatchCount = pending; }
    }
}

