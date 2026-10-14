using UnityEngine;

namespace Aura.Wave
{
    /// @brief 波の通り道をたどる（WaveTracer）ときの設定。WaveEmitter が作り、単発とビームで共通に使う
    internal struct WaveTraceSettings
    {
        public WaveType Type;                               ///< 波の種類
        public WaveFireMode FireMode;                       ///< 撃ち方
        public GameObject Source;                           ///< 波を出した物
        public float Radius;                                ///< 当たり判定の半径（m）。0 なら線で判定する
        public float Range;                                 ///< 届く距離（m）。跳ね返った後の道のりも合わせて数える
        public LayerMask HitMask;                           ///< 当たるレイヤー
        public QueryTriggerInteraction TriggerInteraction;  ///< トリガーに当たるか
        public Transform IgnoreRoot;                        ///< 撃った本人。跳ね返る前はこの下のコライダーに当たらず、跳ね返った後は当たるとそこで止まる（波は渡さない）
        public WaveReflectionMode ReflectionMode;           ///< 跳ね返るかどうかの決め方
        public int MaxReflections;                          ///< 跳ね返る回数の上限
        public float ReflectionEnergyMultiplier;            ///< 跳ね返るたびにエネルギーに掛ける割合
        public bool IsPreview;                              ///< true なら予測線用。通り道をたどるだけで、受け手に波を渡さない
    }
}
