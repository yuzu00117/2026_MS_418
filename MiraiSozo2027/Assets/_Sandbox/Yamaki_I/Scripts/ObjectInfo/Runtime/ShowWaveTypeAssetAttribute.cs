using System;
using UnityEngine;

namespace Aura.ObjectInfo
{
    /// @brief 波の種類の欄の横に、実際に使われる WaveType のアセット（SO_WaveTypeInfo の枠から引いたもの）を薄く出す
    /// 「波の種類」（WaveKind）の欄に付けると、選んでいる種類のアセットを出す
    /// bool の欄（「電磁波に反応する」など）に付けるときは、どの種類のアセットを出すかを引数で決める
    /// 見せるだけで、保存される値や動きは何も変わらない。出したアセットをクリックすると、Project ウィンドウで場所が光る
    /// 例: [InspectorLabel("波の種類")] [SerializeField, ShowWaveTypeAsset] private WaveKind _waveKind;
    [AttributeUsage(AttributeTargets.Field)]
    public sealed class ShowWaveTypeAssetAttribute : PropertyAttribute
    {
        /// @brief 「波の種類」（WaveKind）の欄に付ける。選んでいる種類のアセットを出す
        public ShowWaveTypeAssetAttribute()
        {
        }

        /// @brief bool の欄に付ける。決めた種類のアセットを出す
        /// @param kind 出すアセットの種類
        public ShowWaveTypeAssetAttribute(WaveKind kind)
        {
            Kind = kind;
            HasFixedKind = true;
        }

        /// @brief 出すアセットの種類。Has Fixed Kind が true のときだけ使う
        public WaveKind Kind { get; }

        /// @brief 種類を引数で決めたか（bool の欄に付けたとき）
        public bool HasFixedKind { get; }
    }
}
