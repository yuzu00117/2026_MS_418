using System;
using UnityEngine;

namespace MS.Toon
{
    /// <summary>
    /// ゲーム全体のトゥーン調の絵作りをまとめたプロファイル。
    /// ToonStyleApplier がこの値を _Global* としてシェーダーへ反映する。
    /// </summary>
    [CreateAssetMenu(fileName = "SO_ToonStyle", menuName = "MS/Toon/Style Profile")]
    public class ToonStyleProfile : ScriptableObject
    {
        // MS_Toon.shader / ProjectLighting.hlsl のグローバル名と一致させること
        private static readonly int ProfileEnabledId = Shader.PropertyToID("_GlobalProfileEnabled");
        private static readonly int SmoothnessId = Shader.PropertyToID("_GlobalSmoothness");
        private static readonly int ShadowColorId = Shader.PropertyToID("_GlobalShadowColor");
        private static readonly int AmbientTintId = Shader.PropertyToID("_GlobalAmbientTint");
        private static readonly int RimColorId = Shader.PropertyToID("_GlobalRimColor");
        private static readonly int RimPowerId = Shader.PropertyToID("_GlobalRimPower");
        private static readonly int RampMapId = Shader.PropertyToID("_GlobalRampMap");
        private static readonly int RampWidthId = Shader.PropertyToID("_GlobalRampWidth");

        [Header("Ramp")]
        [Tooltip("NdotL を色に変換する1D階調画像。Bilinear / Clamp / ミップなし推奨。未設定なら各マテリアルのランプを使う")]
        [SerializeField] private Texture2D _ramp;
        [Tooltip("階調の硬さ。0=完全トゥーン、1=滑らか")]
        [SerializeField, Range(0f, 1f)] private float _smoothness = 0.05f;

        [Header("Shadow")]
        [SerializeField] private Color _shadowColor = new Color(0.55f, 0.55f, 0.72f, 1f);
        [SerializeField, Range(0f, 1f)] private float _shadowStrength = 1f;

        [Header("Ambient")]
        [SerializeField] private Color _ambientTint = Color.white;
        [SerializeField, Range(0f, 2f)] private float _ambientStrength = 1f;

        [Header("Rim")]
        [SerializeField] private Color _rimColor = Color.white;
        [SerializeField, Range(0.5f, 8f)] private float _rimPower = 3f;

        /// <summary>インスペクターで値が変わったときに通知する(Applier が再反映する)。</summary>
        public event Action Changed;

        public Texture2D Ramp => _ramp;
        public float Smoothness => _smoothness;

        /// <summary>この内容を _Global* としてシェーダーへ設定する。</summary>
        public void ApplyToGlobals()
        {
            Shader.SetGlobalFloat(ProfileEnabledId, 1f);
            Shader.SetGlobalFloat(SmoothnessId, _smoothness);
            Shader.SetGlobalVector(ShadowColorId, new Vector4(_shadowColor.r, _shadowColor.g, _shadowColor.b, _shadowStrength));
            Shader.SetGlobalVector(AmbientTintId, new Vector4(_ambientTint.r, _ambientTint.g, _ambientTint.b, _ambientStrength));
            Shader.SetGlobalVector(RimColorId, new Vector4(_rimColor.r, _rimColor.g, _rimColor.b, 1f));
            Shader.SetGlobalFloat(RimPowerId, _rimPower);

            // ランプ未設定のときは幅0を送り、シェーダー側でマテリアルのランプにフォールバックさせる
            if (_ramp != null)
            {
                Shader.SetGlobalTexture(RampMapId, _ramp);
                Shader.SetGlobalFloat(RampWidthId, _ramp.width);
            }
            else
            {
                Shader.SetGlobalFloat(RampWidthId, 0f);
            }
        }

        /// <summary>プロファイルを無効化し、シェーダーのデフォルト値/マテリアル値へ戻す。</summary>
        public static void ClearGlobals()
        {
            Shader.SetGlobalFloat(ProfileEnabledId, 0f);
            Shader.SetGlobalFloat(RampWidthId, 0f);
        }

        private void OnValidate()
        {
            Changed?.Invoke();
        }
    }
}
