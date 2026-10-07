using System;
using UnityEngine;

namespace Aura.Wave
{
    /// @brief 単発の波（WavePulse）を飛ばすときの設定。WaveEmitter が撃つときに作って渡す
    /// 撃った後に WaveEmitter の設定を変えても、飛んでいる波はこの設定のまま進む
    internal struct WavePulseSettings
    {
        public WaveTraceSettings Trace;                 ///< 通り道をたどるときの設定（種類、当たり判定、反射）
        public Vector3 Origin;                          ///< 出る位置
        public Vector3 Direction;                       ///< 進む向き（正規化したもの）
        public float Energy;                            ///< 当たったときに渡すエネルギー。跳ね返るたびに減る
        public Vector3 Up;                              ///< 見た目の波が揺れる面を決める上の向き
        public float Speed;                             ///< 進む速さ（m/s）
        public float Length;                            ///< 見た目の波の塊の長さ（m）
        public Material LineMaterial;                   ///< 線のマテリアル。null なら既定
        public Action<WaveHit, bool> DeliveredCallback; ///< 物に当たって波を渡した後に呼ぶ。bool は受け付けられたか
    }
}
