namespace Echo.Echolocation
{
    /// <summary>実体化する前に照射が途切れたときの、蓄積した照射時間の扱い。</summary>
    public enum EchoIrradiationInterruptionMode
    {
        [UnityEngine.InspectorName("0に戻す")] Reset,
        [UnityEngine.InspectorName("保持する")] Keep,
        [UnityEngine.InspectorName("徐々に減らす")] Decay
    }
}
