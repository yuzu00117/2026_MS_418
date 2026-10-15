using Aura.Inspector;
using Aura.Wave;
using UnityEngine;

namespace Aura.ObjectInfo
{
    /// @brief ビームを出す物の情報。シートの B1〜B5（種類、強さ、届く距離、念力で曲げられるか、出したもの）
    /// フィールドのギミック・敵・プレイヤーのどれがビームを出す場合も、同じ項目を持つ
    /// ビームを出す処理は持たない（ビームを出す機能の側が、これを読んで出す）。今ある WaveEmitter（Aura.Wave）は、これをまだ読まない
    [DisallowMultipleComponent]
    public sealed class BeamSourceInfo : MonoBehaviour, IObjectInfo
    {
        [InspectorLabel("波の種類")]
        [Tooltip("出すビームの波の種類（WaveType のアセット）。超音波、電磁波など。種類はアセットを作れば増やせる\n" +
                 "空なら、ビームを出す機能の側が扱いを決める")]
        [SerializeField] private WaveType _waveType;                        ///< 波の種類

        [InspectorLabel("強さ（1 秒あたり）")]
        [Tooltip("強さ。当てている間、1 秒あたりに与えるエネルギー。0 なら、当ててもエネルギーを与えない")]
        [SerializeField, Min(0.0f)] private float _energyPerSecond = 1.0f;  ///< 1 秒あたりに与えるエネルギー

        [InspectorLabel("届く距離（m）")]
        [Tooltip("届く距離（m）。ビームの通り道に沿って数え、跳ね返った後の道のりも含む（今の WaveEmitter の「ビームの届く距離（m）」と同じ数え方）\n" +
                 "0 なら届かない")]
        [SerializeField, Min(0.0f)] private float _range = 25.0f;           ///< 届く距離（m）。跳ね返った後の道のりも含む

        [InspectorLabel("念力で曲げられる")]
        [Tooltip("オンにすると、念力で曲げられる（敵やギミックのビームなど）")]
        [SerializeField] private bool _isBendable = true;                   ///< 念力で曲げられるか

        /// @brief 波の種類。空のこともある
        public WaveType WaveType => _waveType;

        /// @brief 強さ（1 秒あたりに与えるエネルギー）
        public float EnergyPerSecond => _energyPerSecond;

        /// @brief 届く距離（m）。ビームの通り道に沿って数え、跳ね返った後の道のりも含む
        public float Range => _range;

        /// @brief 念力で曲げられるか
        public bool IsBendable => _isBendable;

        /// @brief 出したもの（ギミック・敵・プレイヤー）。Inspector では決めず、ビームを出す機能の側が、出し始めるときに入れる
        /// 入るまでは Unknown。受け手の反応には使わない
        public BeamSourceKind SourceKind { get; set; }

        // 有効・無効が変わると ObjectInfoLookup の結果が変わるので、覚えた結果を捨ててもらう
        // （OnEnable があるので、Inspector に有効と無効のチェックボックスも出る。無効にすると、無いものとして扱う）
        private void OnEnable()
        {
            ObjectInfoCache.MarkDirty();
        }

        private void OnDisable()
        {
            ObjectInfoCache.MarkDirty();
        }
    }
}
