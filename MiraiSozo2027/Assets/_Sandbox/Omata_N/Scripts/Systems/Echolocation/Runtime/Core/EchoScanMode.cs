// スキャンの状態と設定用列挙型。入力や描画パイプラインに依存しない。
namespace Echo.Echolocation
{
    /// <summary>現在位置へ追従する領域か、発生位置に残る定期的な波か、1回だけの波か。</summary>
    public enum EchoScanMode
    {
        /// <summary>ライト方式。プレイヤーの位置・向きへ追従する領域を照らし続ける。</summary>
        [UnityEngine.InspectorName("ライト方式")] Light,
        /// <summary>断続方式。発生位置・向きを固定した波を一定間隔で出し直す。</summary>
        [UnityEngine.InspectorName("断続方式")] Intermittent,
        /// <summary>単発方式。発生位置から帯状の波を1回だけ伝播する。帯が通り過ぎた場所は当たらない。</summary>
        [UnityEngine.InspectorName("単発方式")] Single
    }
}
