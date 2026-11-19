namespace Echo.Echolocation
{
    /// <summary>ライト方式で使った持続可能な時間の戻し方。</summary>
    public enum EchoLightRecoveryMode
    {
        [UnityEngine.InspectorName("OFFの間に回復する")] WhileOff,
        [UnityEngine.InspectorName("OFFにすると満タンに戻る")] RefillOnOff,
        [UnityEngine.InspectorName("回復しない")] None
    }
}
