// エコー対象に付ける唯一のコンポーネント。子Rendererの所有と専用タグの指定をまとめて持つ。
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Echo.Echolocation
{
    /// <summary>子Rendererを専用タグの表示・遮断設定で描画する。</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Echo/Echo Target")]
    public sealed class EchoTarget : MonoBehaviour
    {
        [SerializeField, Tooltip("表示と遮断の設定を持つ専用タグアセット。未設定なら表示せず、遮断物として扱います。")]
        private EchoTagDefinition _definition;
        [SerializeField, Tooltip("有効にすると一覧に指定した子Rendererだけを収集します。")]
        [UnityEngine.Serialization.FormerlySerializedAs("useManualRenderers")]
        private bool _isManualRendererSelectionEnabled;
        [SerializeField, Tooltip("自身または子のRenderer。子EchoTargetの所有物は指定できません。")]
        [UnityEngine.Serialization.FormerlySerializedAs("renderers")]
        private List<Renderer> _renderers = new List<Renderer>();
        /// <summary>登録・構造変更の通知。</summary>
        public static event Action Changed;
        private bool _hasPendingRefresh;
        private EchoTag _legacyTag;
        private bool _hasSearchedLegacyTag;

        /// <summary>専用タグ。統合前のシーンでは旧EchoTagの指定を引き継ぐ。</summary>
        public EchoTagDefinition Definition
        {
            get
            {
                if (_definition) return _definition;
                var legacy = LegacyTag;
                return legacy && legacy.isActiveAndEnabled ? legacy.Definition : null;
            }
            set
            {
                _definition = value;
                // 旧EchoTagが残っていると未設定時にそちらへ戻るため、同じ値にそろえる。
                var legacy = LegacyTag;
                if (legacy) legacy.SetDefinitionWithoutRefresh(value);
                RefreshRenderers();
            }
        }
        /// <summary>このコンポーネント自身に保存された専用タグ。移行ツールが使う。</summary>
        public EchoTagDefinition OwnDefinition => _definition;
        /// <summary>統合前の旧EchoTag。移行後はnull。</summary>
        public EchoTag LegacyTag
        {
            get
            {
                // 毎フレームの遮断判定から呼ばれるため、GetComponentは初回だけにする。
                if (!_hasSearchedLegacyTag)
                {
                    _legacyTag = GetComponent<EchoTag>();
                    _hasSearchedLegacyTag = true;
                }
                return _legacyTag;
            }
        }

        // 表示の判定はGPUで行うため、結果は非同期の読み戻しで1～3フレーム遅れて届く。
        // 読み戻しの負荷を問い合わせのない対象に掛けないよう、初めて参照された時点から追跡する。
        private const int StaleFrameCount = 10;
        // GPUは接触を0か1で書き込むため、その中間で判定する。
        private const float ContactThreshold = .5f;
        private bool _isRevealTracked;
        private bool _isInContact;
        private bool _isRevealed;
        private float _lastContactTime = float.NegativeInfinity;
        private float _persistence;
        private EchoTimeMode _timeMode;
        private int _lastReportFrame = -StaleFrameCount;
        private int _pendingReadbackCount;
        private Action<AsyncGPUReadbackRequest> _readbackCallback;
        private event Action<EchoTarget, bool> _revealChanged;

        /// <summary>塗りつぶし・輪郭・元マテリアルのいずれかを表示中か。残存表示中も含む。</summary>
        public bool IsRevealed { get { _isRevealTracked = true; UpdateReveal(); return _isRevealed; } }
        /// <summary>生存中の波が今この対象に届いているか。エコー地点から遮断された部分は含まない。</summary>
        public bool IsInContact { get { _isRevealTracked = true; return _isInContact && !IsStale; } }
        /// <summary>最後に波が届いた時刻（Controllerの時間の基準）。まだ届いていなければ負の無限大。</summary>
        public float LastContactTime { get { _isRevealTracked = true; return _lastContactTime; } }
        /// <summary>IsRevealedが変わった時に、対象と新しい値を通知する。</summary>
        public event Action<EchoTarget, bool> RevealChanged
        {
            add { _isRevealTracked = true; _revealChanged += value; }
            remove => _revealChanged -= value;
        }
        /// <summary>描画側が読み戻しを行うか。</summary>
        internal bool IsRevealTracked => _isRevealTracked && isActiveAndEnabled;
        private bool IsStale => Time.frameCount - _lastReportFrame > StaleFrameCount;
        private float Now => _timeMode == EchoTimeMode.Scaled ? Time.time : Time.unscaledTime;

        /// <summary>波が対象の範囲に届いていないことをCPU側で確定した時の報告。</summary>
        internal void ReportNoContact(float persistence, EchoTimeMode timeMode)
        {
            _persistence = persistence;
            _timeMode = timeMode;
            _lastReportFrame = Time.frameCount;
            // 届く前の読み戻しは過去の接触を含み得る。先に「非接触」を確定すると値が戻って揺れるため待つ。
            if (_pendingReadbackCount == 0) _isInContact = false;
        }

        /// <summary>GPUの接触結果を読み戻す要求に使うコールバック。毎フレーム生成しない。</summary>
        internal Action<AsyncGPUReadbackRequest> GetReadbackCallback(float persistence, EchoTimeMode timeMode)
        {
            _persistence = persistence;
            _timeMode = timeMode;
            _pendingReadbackCount++;
            return _readbackCallback ??= HandleReadback;
        }

        void HandleReadback(AsyncGPUReadbackRequest request)
        {
            _pendingReadbackCount = Mathf.Max(0, _pendingReadbackCount - 1);
            if (!this || request.hasError) return;
            // R：接触中なら1、G：最後の接触時刻+1（未接触は0）。描画側の残存時間管理と同じ値。
            var value = request.GetData<Vector4>()[0];
            _isInContact = value.x > ContactThreshold;
            if (value.y > 0) _lastContactTime = value.y - 1;
            _lastReportFrame = Time.frameCount;
            UpdateReveal();
        }

        void UpdateReveal()
        {
            bool isRevealed = !IsStale && (_isInContact || Now - _lastContactTime <= _persistence);
            if (isRevealed == _isRevealed) return;
            _isRevealed = isRevealed;
            _revealChanged?.Invoke(this, isRevealed);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => Changed = null;
        void OnEnable() => RefreshRenderers();
        void OnDisable()
        {
            RefreshRenderers();
            _isInContact = false;
            _lastReportFrame = -StaleFrameCount;
            if (_isRevealTracked) UpdateReveal();
        }
        void OnDestroy() => RefreshRenderers();
        void OnValidate()
        {
            _hasSearchedLegacyTag = false;
            _hasPendingRefresh = true;
        }
        void LateUpdate()
        {
            // 残存時間の終了や報告の途絶は読み戻しを待たずに判定し、イベントを遅らせない。
            if (_isRevealTracked) UpdateReveal();
            if (!_hasPendingRefresh) return;
            _hasPendingRefresh = false;
            RefreshRenderers();
        }
        /// <summary>子の変更後に登録を更新する。</summary>
        public void RefreshRenderers() => Changed?.Invoke();
        /// <summary>旧EchoTagの追加・削除後に参照を取り直す。</summary>
        public void ForgetLegacyTag() => _hasSearchedLegacyTag = false;
        /// <summary>最も近い所有者と手動一覧の双方を満たすRendererだけを受け付ける。</summary>
        public bool Owns(Renderer renderer) => isActiveAndEnabled && renderer.GetComponentInParent<EchoTarget>(true) == this && (!_isManualRendererSelectionEnabled || _renderers.Contains(renderer));
    }
}
