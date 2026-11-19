using UnityEngine;

public enum WaveDamageMode
{
    [InspectorName("多段ヒット方式（無敵時間ごとに連続ヒット）")]
    MultiHit,

    [InspectorName("継続照射チャージ方式（一定時間当て続けるごとにヒット）")]
    ChargeHit
}
