// スキャンの状態と設定用列挙型。入力や描画パイプラインに依存しない。
namespace Echo.Echolocation
{
    /// <summary>OFF時の消去方法。</summary>
    public enum EchoOffMode
    {
        Immediate,
        Wave
    }
}
