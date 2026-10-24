using Aura.Inspector;
using Aura.Wave;
using UnityEngine;

namespace Aura.ObjectInfo
{
    /// @brief 波で動く物の情報。シートの O3「反応する波の種類」と O4「動くまでに当てる時間」
    /// 反応は波の種類だけで決まり、誰が出したビーム（ギミック・敵・プレイヤー）かは見ない
    /// 動かす処理は持たない（ReactionTrigger が、これを読んで動かす）
    [DisallowMultipleComponent]
    public sealed class WaveReaction : MonoBehaviour, IObjectInfo
    {
        [InspectorLabel("電磁波に反応する")]
        [Tooltip("オンにすると、電磁波のビームに反応する。「超音波に反応する」と両方オンなら、どちらの波にも反応する\n" +
                 "両方オフなら、どの波にも反応しない\n" +
                 "ゲームの途中で反応する波が変わる敵・ギミックは、React Only To Electromagnetic / React Only To Ultrasound（UnityEvent からも呼べる）か、コードの Set Reaction で切り替える")]
        [SerializeField, ShowWaveTypeAsset(WaveKind.Electromagnetic)] private bool _reactsToElectromagnetic;  ///< 電磁波に反応するか

        [InspectorLabel("超音波に反応する")]
        [Tooltip("オンにすると、超音波のビームに反応する")]
        [SerializeField, ShowWaveTypeAsset(WaveKind.Ultrasound)] private bool _reactsToUltrasound;  ///< 超音波に反応するか

        [InspectorLabel("動くまでの秒数（強さ 1）")]
        [Tooltip("動くまでに溜める量。強さ 1 のビームを当て続けたときの秒数で数える。例: 3 なら、強さ 1 のビームを 3 秒当てると動く\n" +
                 "強いビームほど早く動く（強さ 2 なら半分の時間）。0 なら、当たった瞬間に動く")]
        [SerializeField, Min(0.0f)] private float _requiredSeconds = 1.0f;  ///< 動くまでに溜める量（強さ 1 のビームで当てる秒数）

        // 旧: WaveType のアセットの配列と「どの波にも反応する」。読み込んだときにチェックボックスへ写して空にする（移行用に隠して残す）
        [SerializeField, HideInInspector] private WaveType[] _reactiveTypes;
        [SerializeField, HideInInspector] private bool _canReactToAnyType;

        /// @brief 電磁波に反応するか。実行中に変えてもよい
        [ShowInEventMenu]
        public bool ReactsToElectromagnetic
        {
            get => _reactsToElectromagnetic;
            set => _reactsToElectromagnetic = value;
        }

        /// @brief 超音波に反応するか。実行中に変えてもよい
        [ShowInEventMenu]
        public bool ReactsToUltrasound
        {
            get => _reactsToUltrasound;
            set => _reactsToUltrasound = value;
        }

        /// @brief 動くまでに溜める量（強さ 1 のビームで当てる秒数）。強いビームほど早く溜まる。0 なら当たった瞬間に動く
        public float RequiredSeconds => _requiredSeconds;

        /// @brief その種類の波に反応するか。波の種類だけで決まる
        /// @param kind 当たった波の種類
        /// @return 反応するなら true
        public bool CanReactTo(WaveKind kind)
        {
            return kind == WaveKind.Ultrasound ? _reactsToUltrasound : _reactsToElectromagnetic;
        }

        /// @brief その種類の波に反応するか（WaveType のアセットで調べる）。対応表（SO_WaveTypeInfo）に無いアセットには反応しない
        /// @param type 当たった波の種類のアセット
        /// @return 反応するなら true
        public bool CanReactTo(WaveType type)
        {
            return WaveTypeInfo.TryGetKind(type, out WaveKind kind) && CanReactTo(kind);
        }

        /// @brief 反応する波を変える（Inspector のチェックボックスと同じ）
        /// @param kind 波の種類
        /// @param reacts 反応するなら true
        public void SetReaction(WaveKind kind, bool reacts)
        {
            if (kind == WaveKind.Ultrasound)
            {
                _reactsToUltrasound = reacts;
            }
            else
            {
                _reactsToElectromagnetic = reacts;
            }
        }

        /// @brief 電磁波だけに反応するようにする。途中で反応する波が変わる敵・ギミック用（UnityEvent からも呼べる）
        [ShowInEventMenu]
        public void ReactOnlyToElectromagnetic()
        {
            _reactsToElectromagnetic = true;
            _reactsToUltrasound = false;
        }

        /// @brief 超音波だけに反応するようにする。途中で反応する波が変わる敵・ギミック用（UnityEvent からも呼べる）
        [ShowInEventMenu]
        public void ReactOnlyToUltrasound()
        {
            _reactsToUltrasound = true;
            _reactsToElectromagnetic = false;
        }

        private void Awake()
        {
            MigrateReactiveTypes();
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (MigrateReactiveTypes())
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

        /// @brief 旧欄（WaveType のアセットの配列と「どの波にも反応する」）が残っていれば、チェックボックスへ写して空にする
        /// 対応表（SO_WaveTypeInfo）に無いアセットは、警告を出して捨てる。対応表がまだ読めないときは、何もせず次の読み込みでやり直す
        /// @return 写したら true
        private bool MigrateReactiveTypes()
        {
            bool hasMigrated = false;
            if (_canReactToAnyType)
            {
                _reactsToElectromagnetic = true;
                _reactsToUltrasound = true;
                _canReactToAnyType = false;
                hasMigrated = true;
            }
            if (_reactiveTypes != null && _reactiveTypes.Length > 0 && WaveTypeInfo.Instance != null)
            {
                foreach (WaveType type in _reactiveTypes)
                {
                    if (WaveTypeInfo.TryGetKind(type, out WaveKind kind))
                    {
                        SetReaction(kind, true);
                    }
                    else if (type != null)
                    {
                        Debug.LogWarning($"WaveReaction: 対応表（SO_WaveTypeInfo）に無い波の種類 {type.name} が旧欄にあったので捨てた", this);
                    }
                }
                _reactiveTypes = null;
                hasMigrated = true;
            }
            return hasMigrated;
        }
    }
}
