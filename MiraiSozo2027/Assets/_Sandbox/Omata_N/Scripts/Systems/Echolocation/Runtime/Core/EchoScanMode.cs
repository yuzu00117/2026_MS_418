// スキャンの状態と設定用列挙型。入力や描画パイプラインに依存しない。
namespace Echo.Echolocation
{
    /// <summary>現在位置へ追従する領域か、発生位置に残る定期的な波か。</summary>
    public enum EchoScanMode
    {
        Follow,
        Sonar
    }
}
