using UnityEngine;

namespace MS.Toon
{
    /// <summary>
    /// このオブジェクト配下のトゥーン描画だけ、輪郭線をキャラクター用プロファイルの値にする。
    /// 輪郭線以外(影・リム・環境光など)は引き続き ToonStyleApplier の共通設定に従う。
    /// マテリアルは書き換えず、MaterialPropertyBlock で上書きする。
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class ToonCharacterStyle : MonoBehaviour
    {
        private static readonly int UseGlobalOutlineId = Shader.PropertyToID("_UseGlobalOutline");
        private static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");

        [Tooltip("輪郭線の値を使うプロファイル(通常は SO_ToonStyle_Character)。輪郭線以外の項目は使わない")]
        [SerializeField] private ToonStyleProfile _profile;

        private ToonStyleProfile _subscribedProfile;
        private MaterialPropertyBlock _block;

        private void OnEnable()
        {
            Apply();
        }

        private void OnDisable()
        {
            Unsubscribe();
            Clear();
        }

        private void OnValidate()
        {
            if (isActiveAndEnabled)
            {
                Apply();
            }
        }

        private void Apply()
        {
            Subscribe();
            if (_profile == null)
            {
                Clear();
                return;
            }

            _block ??= new MaterialPropertyBlock();
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                r.GetPropertyBlock(_block);
                _block.SetFloat(UseGlobalOutlineId, 0f);
                _block.SetColor(OutlineColorId, _profile.OutlineColor);
                _block.SetFloat(OutlineWidthId, _profile.OutlineWidth);
                r.SetPropertyBlock(_block);
            }
        }

        private void Clear()
        {
            foreach (var r in GetComponentsInChildren<Renderer>(true))
            {
                r.SetPropertyBlock(null);
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
