// スキャンの状態と設定用列挙型。入力や描画パイプラインに依存しない。
namespace Echo.Echolocation
{
    /// <summary>現在位置へ追従する領域か、発生位置に残る定期的な波か、1回だけの波か。</summary>
    public enum EchoScanMode
    {
        Follow,
        Sonar,
        /// <summary>発生位置から帯状の波を1回だけ伝播する。帯が通り過ぎた場所は当たらない。</summary>
        Once
    }
}
