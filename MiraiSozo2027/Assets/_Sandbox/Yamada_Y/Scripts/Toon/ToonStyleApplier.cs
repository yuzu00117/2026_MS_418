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

        public ToonStyleProfile Profile => _profile;

        private void OnEnable()
        {
            Apply();
        }

        private void OnDisable()
        {
            Unsubscribe();
            ToonStyleProfile.ClearGlobals();
        }

        private void OnValidate()
        {
            if (isActiveAndEnabled)
            {
                Apply();
            }
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
