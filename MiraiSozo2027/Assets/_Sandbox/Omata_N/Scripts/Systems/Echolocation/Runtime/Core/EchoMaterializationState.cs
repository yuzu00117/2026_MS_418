namespace Echo.Echolocation
{
    /// <summary>通常の描画を隠すエコー対象の実体化の段階。隠さない対象は常に実体化として扱う。</summary>
    public enum EchoMaterializationState
    {
        [UnityEngine.InspectorName("透明")] Transparent,
        [UnityEngine.InspectorName("照射中")] Irradiated,
        [UnityEngine.InspectorName("実体化")] Materialized
    }
}
