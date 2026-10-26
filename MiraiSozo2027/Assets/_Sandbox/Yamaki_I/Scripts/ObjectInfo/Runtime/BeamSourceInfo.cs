using Aura.Inspector;
using Aura.Wave;
using UnityEngine;

namespace Aura.ObjectInfo
{
    /// @brief ビームを出す物の情報。シートの B1〜B5（種類、強さ、届く距離、念力で曲げられるか、出したもの）
    /// フィールドのギミック・敵・プレイヤーのどれがビームを出す場合も、同じ項目を持つ
    /// ビームを出す処理は持たない（WaveEmitter（Aura.ObjectInfoDemo）が、これを読んで出す）
    [DisallowMultipleComponent]
    public sealed class BeamSourceInfo : MonoBehaviour, IObjectInfo
    {
        [InspectorLabel("波の種類")]
        [Tooltip("出すビームの波の種類。電磁波か超音波を選ぶ\n" +
                 "WaveType のアセットはここでは選ばない（種類とアセットの対応は、Resources/SO_WaveTypeInfo の枠で 1 か所に決める）\n" +
                 "横に、実際に使われるアセットが薄く出る（クリックすると Project ウィンドウで場所が光る）")]
        [SerializeField, ShowWaveTypeAsset] private WaveKind _waveKind = WaveKind.Electromagnetic;  ///< 波の種類

        // 旧: WaveType のアセットを直接選んでいた欄。読み込んだときに「波の種類」へ写して空にする（移行用に隠して残す）
        [SerializeField, HideInInspector] private WaveType _waveType;

        [InspectorLabel("強さ（1 秒あたり）")]
        [Tooltip("強さ。当てている間、1 秒あたりに与えるエネルギー。0 なら、当ててもエネルギーを与えない")]
        [SerializeField, Min(0.0f)] private float _energyPerSecond = 1.0f;  ///< 1 秒あたりに与えるエネルギー

        [InspectorLabel("届く距離（m）")]
        [Tooltip("届く距離（m）。ビームの通り道に沿って数え、跳ね返った後の道のりも含む\n" +
                 "0 なら届かない")]
        [SerializeField, Min(0.0f)] private float _range = 25.0f;           ///< 届く距離（m）。跳ね返った後の道のりも含む

        [InspectorLabel("念力で曲げられる")]
        [Tooltip("オンにすると、念力で曲げられる（敵やギミックのビームなど）")]
        [SerializeField] private bool _isBendable = true;                   ///< 念力で曲げられるか

        /// @brief 波の種類（電磁波/超音波）。実行中に変えてもよい（次のフレームのビームから効く）
        public WaveKind WaveKind
        {
            get => _waveKind;
            set => _waveKind = value;
        }

        /// @brief 波の種類の WaveType アセット。Resources/SO_WaveTypeInfo の枠から引く。対応表が無いか、枠が空なら null
        public WaveType WaveType => WaveTypeInfo.GetWaveType(_waveKind);

        /// @brief 強さ（1 秒あたりに与えるエネルギー）
        public float EnergyPerSecond => _energyPerSecond;

        /// @brief 届く距離（m）。ビームの通り道に沿って数え、跳ね返った後の道のりも含む
        public float Range => _range;

        /// @brief 念力で曲げられるか
        public bool IsBendable => _isBendable;

        /// @brief 出したもの（ギミック・敵・プレイヤー）。Inspector では決めず、ビームを出す機能の側が、出し始めるときに入れる
        /// 入るまでは Unknown。受け手の反応には使わない
        public BeamSourceKind SourceKind { get; set; }

        private void Awake()
        {
            MigrateWaveType();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (MigrateWaveType())
            {
                UnityEditor.EditorUtility.SetDirty(this);
            }
        }
#endif

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

        /// @brief 旧欄（WaveType のアセット）が残っていれば、「波の種類」へ写して空にする
        /// 対応表（SO_WaveTypeInfo）に無いアセットは、警告を出して捨てる（「波の種類」は今の値のまま）
        /// @return 写したら true
        private bool MigrateWaveType()
        {
            if (_waveType == null || WaveTypeInfo.Instance == null)
            {
                return false;
            }
            if (WaveTypeInfo.TryGetKind(_waveType, out WaveKind kind))
            {
                _waveKind = kind;
            }
            else
            {
                Debug.LogWarning($"BeamSourceInfo: 対応表（SO_WaveTypeInfo）に無い波の種類 {_waveType.name} が旧欄にあったので捨てた。「波の種類」は今の値のまま", this);
            }
            _waveType = null;
            return true;
        }
    }
}
