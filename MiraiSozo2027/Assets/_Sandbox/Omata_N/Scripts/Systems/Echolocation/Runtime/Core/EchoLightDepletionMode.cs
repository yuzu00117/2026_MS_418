namespace Echo.Echolocation
{
    /// <summary>ライト方式の持続可能な時間を使い切ったときの扱い。</summary>
    public enum EchoLightDepletionMode
    {
        [UnityEngine.InspectorName("自動でOFFになる")] TurnOff,
        [UnityEngine.InspectorName("最小の範囲で照らし続ける")] KeepMinimum
    }
}
