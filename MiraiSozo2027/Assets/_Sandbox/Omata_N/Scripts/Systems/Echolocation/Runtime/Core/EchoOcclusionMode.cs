// スキャンの状態と設定用列挙型。入力や描画パイプラインに依存しない。
namespace Echo.Echolocation
{
    /// <summary>機能ごとのカメラ遮蔽。既存データは共通設定を継承する。</summary>
    public enum EchoEffectOcclusionMode
    {
        [UnityEngine.InspectorName("共通設定に従う")] Inherit,
        [UnityEngine.InspectorName("壁越しに表示する")] ThroughWalls,
        [UnityEngine.InspectorName("壁で隠す")] VisibleOnly
    }

    /// <summary>壁越し表示またはシーン深度による遮蔽。</summary>
    public enum EchoOcclusionMode
    {
        ThroughWalls,
        VisibleOnly
    }
}
