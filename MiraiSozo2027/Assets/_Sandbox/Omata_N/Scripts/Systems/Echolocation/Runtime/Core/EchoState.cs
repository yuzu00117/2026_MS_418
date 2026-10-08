// スキャンの状態と設定用列挙型。入力や描画パイプラインに依存しない。
namespace Echo.Echolocation
{
    /// <summary>現在の表示段階。</summary>
    public enum EchoState
    {
        Hidden,
        Revealing,
        Active,
        Hiding
    }
}
