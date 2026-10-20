// スキャンの状態と設定用列挙型。入力や描画パイプラインに依存しない。
namespace Echo.Echolocation
{
    /// <summary>壁越し表示またはシーン深度による遮蔽。</summary>
    public enum EchoOcclusionMode
    {
        ThroughWalls,
        VisibleOnly
    }
}
