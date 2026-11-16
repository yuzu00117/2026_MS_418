using UnityEngine;

namespace MS.Toon
{
    /// <summary>
    /// ToonStyleProfile をシェーダーのグローバル値へ反映する。
    /// シーンに1つ置く。エディット中もプロファイル変更が即座に Scene ビューへ反映される。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class ToonStyleApplier : MonoBehaviour
    {
        [Tooltip("現在適用するプロファイル。未設定ならデフォルト(各マテリアル値)で描画する")]
        [SerializeField] private ToonStyleProfile _profile;
        [Tooltip("CycleProfile / SetProfileByIndex で切り替える候補")]
        [SerializeField] private ToonStyleProfile[] _profiles;

        private ToonStyleProfile _subscribedProfile;
        private int _currentIndex;

        /// <summary>現在有効な1つ。マテリアルのインスペクタが共通値を表示するために参照する。</summary>
        public static ToonStyleApplier Active { get; private set; }

        public ToonStyleProfile Profile => _profile;

        /// <summary>有効だが Active ではない(=別の Applier が先に有効化されている)ときに true。</summary>
        public bool IsDuplicate => isActiveAndEnabled && IsActiveExistingOther();

        private void OnEnable()
        {
            // コンパイル後は static が初期化されるので、OnEnable の再実行で Active が復元される
            if (IsActiveExistingOther())
            {
                Debug.LogWarning($"ToonStyleApplier が重複しています: '{name}' は反映されません。シーン内の Applier は1つにしてください。", this);
                return;
            }
            Active = this;
            Apply();
        }

        private void OnDisable()
        {
            // 重複側がグローバルを消すと Active の設定まで巻き込むため、触らない
            if (!IsActive())
            {
                return;
            }
            Unsubscribe();
            ToonStyleProfile.ClearGlobals();
            Active = null;
        }

        private void OnValidate()
        {
            if (isActiveAndEnabled && IsActive())
            {
                Apply();
            }
        }

        // Unity の == で比較し、破棄済みオブジェクトを指す static 参照を null として扱う
        private bool IsActiveExistingOther()
        {
            return Active != null && Active != this;
        }

        private bool IsActive()
        {
            return Active == this;
        }

        public void SetProfile(ToonStyleProfile profile)
        {
            _profile = profile;
            Apply();
        }

        public void SetProfileByIndex(int index)
        {
            if (_profiles == null || _profiles.Length == 0)
            {
                return;
            }
            _currentIndex = ((index % _profiles.Length) + _profiles.Length) % _profiles.Length;
            SetProfile(_profiles[_currentIndex]);
        }

        [ContextMenu("Cycle Profile")]
        public void CycleProfile()
        {
            SetProfileByIndex(_currentIndex + 1);
        }

        private void Apply()
        {
            if (!IsActive())
            {
                return;
            }
            Subscribe();
            if (_profile != null)
            {
                _profile.ApplyToGlobals();
            }
            else
            {
                ToonStyleProfile.ClearGlobals();
            }
        }

        private void Subscribe()
        {
            if (_subscribedProfile == _profile)
            {
                return;
            }
            Unsubscribe();
            if (_profile != null)
            {
                _profile.Changed += Apply;
                _subscribedProfile = _profile;
            }
        }

        private void Unsubscribe()
        {
            if (_subscribedProfile != null)
            {
                _subscribedProfile.Changed -= Apply;
            }
            _subscribedProfile = null;
        }
    }
}
