namespace Echo.Echolocation
{
    public enum EchoGlitchMode
    {
        [UnityEngine.InspectorName("常時")] Always,
        [UnityEngine.InspectorName("表示されている間")] WhileRevealed,
        [UnityEngine.InspectorName("表示開始時のみ")] OnReveal
    }
}
