using Aura.Inspector;
using Aura.Wave;
using UnityEngine;

namespace Aura.ObjectInfo
{
    /// @brief 波の種類（WaveType のアセット）への関門。電磁波の枠と超音波の枠に、アセットを 1 つずつ割り当てる
    /// 使う側は「波の種類」（WaveKind: 電磁波/超音波）で選び、WaveType のアセットを毎回 Inspector で選ばずに済む
    /// 役割はこの対応表だけで、波を選ぶ以外の機能は持たせない
    /// アセットは Resources フォルダに 1 つだけ置く（名前は SO_WaveTypeInfo。Resources.Load で自動で見つけるため、フォルダ名は Unity の決まりで Resources にする）
    [CreateAssetMenu(fileName = "SO_WaveTypeInfo", menuName = "Aura/Object Info/Wave Type Info")]
    public sealed class WaveTypeInfo : ScriptableObject
    {
        /// @brief Resources から読むときの名前（Resources フォルダからの相対パス）
        private const string ResourcesPath = "SO_WaveTypeInfo";

        private static WaveTypeInfo _instance;  ///< 読み込んだ対応表。まだ読んでいない・見つからないときは null
        private static bool _hasWarned;         ///< 見つからない警告を出したか（一度だけ出す）

        [InspectorLabel("電磁波のアセット")]
        [Tooltip("電磁波の WaveType のアセット（SO_ElectromagneticWave）を入れる")]
        [SerializeField] private WaveType _electromagnetic;  ///< 電磁波の枠

        [InspectorLabel("超音波のアセット")]
        [Tooltip("超音波の WaveType のアセット（SO_Ultrasound）を入れる")]
        [SerializeField] private WaveType _ultrasound;       ///< 超音波の枠

        /// @brief 対応表のアセット。Resources/SO_WaveTypeInfo を読む。見つからなければ null（警告を一度だけ出す）
        public static WaveTypeInfo Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = Resources.Load<WaveTypeInfo>(ResourcesPath);
                    if (_instance == null && !_hasWarned)
                    {
                        _hasWarned = true;
                        Debug.LogWarning("WaveTypeInfo: Resources/" + ResourcesPath + " が見つからないので、波の種類からアセットを引けない");
                    }
                }
                return _instance;
            }
        }

        /// @brief 種類（電磁波/超音波）から WaveType のアセットを返す
        /// @param kind 波の種類
        /// @return 枠に入れたアセット。対応表が無いか、枠が空なら null
        public static WaveType GetWaveType(WaveKind kind)
        {
            WaveTypeInfo instance = Instance;
            return instance != null ? instance.Get(kind) : null;
        }

        /// @brief WaveType のアセットから種類（電磁波/超音波）を逆引きする
        /// @param type WaveType のアセット
        /// @param outKind 見つかった種類
        /// @return どちらかの枠のアセットなら true。空のとき、対応表に無いアセットのとき、対応表が無いときは false
        public static bool TryGetKind(WaveType type, out WaveKind outKind)
        {
            outKind = WaveKind.Electromagnetic;
            WaveTypeInfo instance = Instance;
            if (type == null || instance == null)
            {
                return false;
            }
            if (type == instance._electromagnetic)
            {
                outKind = WaveKind.Electromagnetic;
                return true;
            }
            if (type == instance._ultrasound)
            {
                outKind = WaveKind.Ultrasound;
                return true;
            }
            return false;
        }

        /// @brief 枠から WaveType のアセットを返す
        /// @param kind 波の種類
        /// @return 枠に入れたアセット。空なら null
        public WaveType Get(WaveKind kind)
        {
            return kind == WaveKind.Ultrasound ? _ultrasound : _electromagnetic;
        }

        // domain reload を切っていると、前の再生で読んだ結果が残るので、再生を始めるたびに読み直す
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            _hasWarned = false;
        }
    }
}
