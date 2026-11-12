namespace Echo.Echolocation
{
    /// <summary>実体化した後、エコーが当たらなくなったときの扱い。</summary>
    public enum EchoMaterializedPersistenceMode
    {
        [UnityEngine.InspectorName("ずっと実体化")] Permanent,
        [UnityEngine.InspectorName("一定時間で透明に戻る")] RevertAfterIdle
    }
}
