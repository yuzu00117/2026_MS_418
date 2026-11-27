using System;
using UnityEngine;

namespace MS.Toon
{
    /// <summary>
    /// ゲーム全体のトゥーン調の絵作りをまとめたプロファイル。
    /// ToonStyleApplier がこの値を _Global* としてシェーダーへ反映する。
    /// マテリアルに「個別の設定ファイル」として登録すると、そのマテリアルだけこの値で描く
    /// (登録の仕組みは Editor 側の ToonMaterialProfileLink)。
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

        // マテリアル側のプロパティ名。MS_Toon.shader の Properties と一致させること
        private static readonly int UseGlobalOutlineId = Shader.PropertyToID("_UseGlobalOutline");
        private static readonly int UseGlobalStyleId = Shader.PropertyToID("_UseGlobalStyle");
        private static readonly int MatOutlineColorId = Shader.PropertyToID("_OutlineColor");
        private static readonly int MatOutlineWidthId = Shader.PropertyToID("_OutlineWidth");
        private static readonly int MatSmoothnessId = Shader.PropertyToID("_Smoothness");
        private static readonly int MatShadowColorId = Shader.PropertyToID("_ShadowColor");
        private static readonly int MatShadowStrengthId = Shader.PropertyToID("_ShadowStrength");
        private static readonly int MatAmbientTintId = Shader.PropertyToID("_AmbientTint");
        private static readonly int MatAmbientStrengthId = Shader.PropertyToID("_AmbientStrength");
        private static readonly int MatRimColorId = Shader.PropertyToID("_RimColor");
        private static readonly int MatRimPowerId = Shader.PropertyToID("_RimPower");
        private static readonly int MatRimIntensityId = Shader.PropertyToID("_RimIntensity");
        private static readonly int MatRampMapId = Shader.PropertyToID("_RampMap");

        /// <summary>
        /// マテリアルに登録したプロファイルの GUID を保存するタグ名。
        /// マテリアルは ScriptableObject への参照を持てないため、文字列タグに GUID を入れて紐づける。
        /// </summary>
        public const string MaterialTagKey = "MSToonStyleProfile";

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

        // 共通設定(Applier)として使うときは無視される。一部の項目だけ変えたいキャラ用の設定ファイルで、
        // 残りの項目は共通設定に従わせるために分けている
        [Header("Per-Material Use")]
        [Tooltip("マテリアルに個別登録したとき、輪郭線(色・太さ)をこのファイルの値にする。オフなら共通設定に従う")]
        [SerializeField] private bool _overrideOutline = true;
        [Tooltip("マテリアルに個別登録したとき、影・環境光・リム・階調をこのファイルの値にする。オフなら共通設定に従う")]
        [SerializeField] private bool _overrideStyle;

        /// <summary>インスペクターで値が変わったときに通知する(Applier が再反映する)。</summary>
        public event Action Changed;

#if UNITY_EDITOR
        /// <summary>どのプロファイルでも値が変わったら通知する(登録先マテリアルの書き戻し用。エディタ専用)。</summary>
        public static event Action<ToonStyleProfile> AnyChanged;
#endif

        public string Description => _description;
        public Texture2D Ramp => _ramp;
        public float Smoothness => _smoothness;
        public Color ShadowColor => _shadowColor;
        public float ShadowStrength => _shadowStrength;
        public Color AmbientTint => _ambientTint;
        public float AmbientStrength => _ambientStrength;
        public Color RimColor => _rimColor;
        public float RimPower => _rimPower;
        public float RimIntensity => _rimIntensity;
        public Color OutlineColor => _outlineColor;
        public float OutlineWidth => _outlineWidth;
        public bool OverrideOutline => _overrideOutline;
        public bool OverrideStyle => _overrideStyle;

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

        /// <summary>
        /// 個別登録されたマテリアルへ、このプロファイルの値を書き込む。
        /// 上書きする項目のトグルはオフ(個別値で描く)、上書きしない項目はオン(共通設定に従う)にする。
        /// 値が同じなら何もしない(読み込み時の再検証でアセットを無駄に変更済みにしないため)。
        /// </summary>
        /// <returns>マテリアルを変更したら true</returns>
        public bool WriteTo(Material material)
        {
            if (material == null)
            {
                return false;
            }

            bool changed = false;
            changed |= SetFloat(material, UseGlobalOutlineId, _overrideOutline ? 0f : 1f);
            changed |= SetFloat(material, UseGlobalStyleId, _overrideStyle ? 0f : 1f);

            if (_overrideOutline)
            {
                changed |= SetColor(material, MatOutlineColorId, _outlineColor);
                changed |= SetFloat(material, MatOutlineWidthId, _outlineWidth);
            }

            if (_overrideStyle)
            {
                changed |= SetFloat(material, MatSmoothnessId, _smoothness);
                changed |= SetColor(material, MatShadowColorId, _shadowColor);
                changed |= SetFloat(material, MatShadowStrengthId, _shadowStrength);
                changed |= SetColor(material, MatAmbientTintId, _ambientTint);
                changed |= SetFloat(material, MatAmbientStrengthId, _ambientStrength);
                changed |= SetColor(material, MatRimColorId, _rimColor);
                changed |= SetFloat(material, MatRimPowerId, _rimPower);
                changed |= SetFloat(material, MatRimIntensityId, _rimIntensity);

                // ランプ未設定のプロファイルでは、マテリアル自身のランプを残す(共通設定と同じ扱い)
                if (_ramp != null && material.HasProperty(MatRampMapId) && material.GetTexture(MatRampMapId) != _ramp)
                {
                    material.SetTexture(MatRampMapId, _ramp);
                    changed = true;
                }
            }

            return changed;
        }

        private static bool SetFloat(Material material, int id, float value)
        {
            if (!material.HasProperty(id) || Mathf.Approximately(material.GetFloat(id), value))
            {
                return false;
            }
            material.SetFloat(id, value);
            return true;
        }

        private static bool SetColor(Material material, int id, Color value)
        {
            if (!material.HasProperty(id) || material.GetColor(id) == value)
            {
                return false;
            }
            material.SetColor(id, value);
            return true;
        }

        // 色のRGBに、シェーダー側が同じ変数で受ける強さ(w)を詰めて送るための変換
        private static Vector4 ToVector(Color color, float w)
        {
            return new Vector4(color.r, color.g, color.b, w);
        }

        private void OnValidate()
        {
            Changed?.Invoke();
#if UNITY_EDITOR
            AnyChanged?.Invoke(this);
#endif
        }
    }
}
