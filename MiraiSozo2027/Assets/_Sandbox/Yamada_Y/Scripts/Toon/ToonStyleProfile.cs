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
        private static readonly int RimIntensityId = Shader.PropertyToID("_GlobalRimIntensity");
        private static readonly int OutlineColorId = Shader.PropertyToID("_GlobalOutlineColor");
        private static readonly int OutlineWidthId = Shader.PropertyToID("_GlobalOutlineWidth");

        // インスペクタで開いた時点で、そのスタイルが何を狙った設定か分かるようにするための説明文
        [Header("Description")]
        [SerializeField, TextArea(2, 6)] private string _description;

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
        [Tooltip("リムライトの強さ。MS_Toon の _RimIntensity と同じ範囲")]
        [SerializeField, Range(0f, 2f)] private float _rimIntensity = 0.5f;

        // 範囲と既定値は MS_Toon.shader の _Outline* マテリアルプロパティに合わせている
        [Header("Outline")]
        [SerializeField] private Color _outlineColor = new Color(0.1f, 0.1f, 0.1f, 1f);
        [Tooltip("輪郭線の太さ(ワールド上の長さ)。カメラから遠いほど細く見える")]
        [SerializeField, Range(0f, 0.1f)] private float _outlineWidth = 0.02f;

        /// <summary>インスペクターで値が変わったときに通知する(Applier が再反映する)。</summary>
        public event Action Changed;

        public string Description => _description;
        public Texture2D Ramp => _ramp;
        public float Smoothness => _smoothness;
        public Color ShadowColor => _shadowColor;
        public float ShadowStrength => _shadowStrength;
        public Color RimColor => _rimColor;
        public float RimPower => _rimPower;
        public float RimIntensity => _rimIntensity;
        public Color OutlineColor => _outlineColor;
        public float OutlineWidth => _outlineWidth;

        /// <summary>この内容を _Global* としてシェーダーへ設定する。</summary>
        public void ApplyToGlobals()
        {
            Shader.SetGlobalFloat(ProfileEnabledId, 1f);
            Shader.SetGlobalFloat(SmoothnessId, _smoothness);
            Shader.SetGlobalVector(ShadowColorId, ToVector(_shadowColor, _shadowStrength));
            Shader.SetGlobalVector(AmbientTintId, ToVector(_ambientTint, _ambientStrength));
            Shader.SetGlobalVector(RimColorId, ToVector(_rimColor, 1f));
            Shader.SetGlobalFloat(RimPowerId, _rimPower);
            Shader.SetGlobalFloat(RimIntensityId, _rimIntensity);
            Shader.SetGlobalVector(OutlineColorId, ToVector(_outlineColor, _outlineColor.a));
            Shader.SetGlobalFloat(OutlineWidthId, _outlineWidth);

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

        // 色のRGBに、シェーダー側が同じ変数で受ける強さ(w)を詰めて送るための変換
        private static Vector4 ToVector(Color color, float w)
        {
            return new Vector4(color.r, color.g, color.b, w);
        }

        private void OnValidate()
        {
            Changed?.Invoke();
        }
    }
}
