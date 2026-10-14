// スキャンの状態と設定用列挙型。入力や描画パイプラインに依存しない。
namespace Echo.Echolocation
{
    /// <summary>伝播に使用する時間。</summary>
    public enum EchoTimeMode
    {
        Scaled,
        Unscaled
    }
}
