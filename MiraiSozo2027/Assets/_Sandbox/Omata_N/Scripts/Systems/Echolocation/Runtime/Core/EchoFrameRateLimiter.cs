using UnityEngine;

namespace Echo.Echolocation
{
    /// <summary>エコーの有無に依存せず、ゲーム全体のFPS上限を管理する。</summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Echo/Frame Rate Limiter")]
    public sealed class EchoFrameRateLimiter : MonoBehaviour
    {
        [SerializeField, InspectorName("FPS上限を有効にする")]
        [Tooltip("OFFではPC版のFPS上限を解除します。コンポーネント自体を無効にすると適用前の設定へ戻します。")]
        [UnityEngine.Serialization.FormerlySerializedAs("limitEnabled")]
        private bool _isLimitEnabled = true;
        [SerializeField, Min(1), InspectorName("上限FPS")]
        [Tooltip("初期値は60。30・60・120・144などを指定できます。再生中の変更も反映します。")]
        [UnityEngine.Serialization.FormerlySerializedAs("maxFps")]
        private int _maxFps = 60;
        private static EchoFrameRateLimiter _owner;
        private int _previousFps;
        private int _previousVSync;
        private int _appliedFps;
        /// <summary>将来の設定画面からも上限の有効・無効を変更できる。</summary>
        public bool IsLimitEnabled
        {
            get => _isLimitEnabled;
            set
            {
                _isLimitEnabled = value;
                ApplyIfOwner();
            }
        }

        /// <summary>1以上の目標FPS。実機がこのFPSを達成することを保証する値ではない。</summary>
        public int MaxFps
        {
            get => _maxFps;
            set
            {
                _maxFps = Mathf.Max(1, value);
                ApplyIfOwner();
            }
        }

        void OnEnable()
        {
            // FPS設定はアプリ全体で共有されるため、複数の管理者が奪い合わないようにする。
            if (_owner && _owner != this)
            {
                Debug.LogWarning("FPS上限の管理は1つだけ配置してください。重複したコンポーネントを無効にしました。", this);
                enabled = false;
                return;
            }

            _owner = this;
            _previousFps = Application.targetFrameRate;
            _previousVSync = QualitySettings.vSyncCount;
            ApplyIfOwner();
        }

        void OnValidate()
        {
            // OnValidateではUnityの描画設定へ触らず、数値の補正だけを行う。
            _maxFps = Mathf.Max(1, _maxFps);
        }

        void Update()
        {
            // Inspectorからの変更をメインスレッドで反映。変更がなければ設定し直さない。
            if (_owner == this && _appliedFps != (_isLimitEnabled ? _maxFps : -1))
                ApplyIfOwner();
        }

        void ApplyIfOwner()
        {
            if (_owner != this || !isActiveAndEnabled)
                return;
            // PCではVSyncが有効だとtargetFrameRateより優先されるので、この管理中だけ解除する。
            QualitySettings.vSyncCount = 0;
            _appliedFps = _isLimitEnabled ? Mathf.Max(1, _maxFps) : -1;
            Application.targetFrameRate = _appliedFps;
        }

        void OnDisable()
        {
            if (_owner != this)
                return;
            // 他の設定システムが後から変更した値は上書きしない。
            if (Application.targetFrameRate == _appliedFps)
                Application.targetFrameRate = _previousFps;
            if (QualitySettings.vSyncCount == 0)
                QualitySettings.vSyncCount = _previousVSync;
            _owner = null;
        }
    }
}
