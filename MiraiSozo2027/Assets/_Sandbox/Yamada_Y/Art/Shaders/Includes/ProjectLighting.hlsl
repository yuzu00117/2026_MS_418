#ifndef MS_PROJECT_LIGHTING_INCLUDED
#define MS_PROJECT_LIGHTING_INCLUDED

// ============================================================================
// ProjectLighting.hlsl
// プロジェクト共通のトゥーンライティング部品。
//
// 設計方針
//  - 機能ごとに独立した関数に分け、呼ばなければ無効(コストゼロ)になる。
//      ToonRampCoord / ToonLightRadiance : 階調(必須)
//      ToonShadowTint                    : 影色(任意)
//      ToonAmbient                       : 環境光(任意)
//      ToonRim                           : リムライト(任意)
//      ToonSpecular                      : スペキュラ(任意)
//  - グローバル値(_Global*)はプロファイル(ScriptableObject)から C# が設定する。
//    未設定だと全て 0 になるため、_GlobalProfileEnabled == 0 のときは
//    シェーダー側のデフォルト値/マテリアル値にフォールバックする。
//  - ランプは「NdotL(+影) -> 1D テクスチャ」で色に変換する。
//    ランプ画像は Bilinear / Clamp / ミップなしでインポートすること。
// ============================================================================

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// ---------------------------------------------------------------------------
// プロファイル未設定時のデフォルト値(ここを変えると全体のデフォルトが変わる)
// ---------------------------------------------------------------------------
#define MS_TOON_DEFAULT_SMOOTHNESS      0.05
#define MS_TOON_DEFAULT_SHADOW_COLOR    float3(0.55, 0.55, 0.72)
#define MS_TOON_DEFAULT_SHADOW_STRENGTH 1.0
#define MS_TOON_DEFAULT_AMBIENT_TINT    float3(1.0, 1.0, 1.0)
#define MS_TOON_DEFAULT_AMBIENT_STRENGTH 1.0
#define MS_TOON_DEFAULT_RIM_COLOR       float3(1.0, 1.0, 1.0)
#define MS_TOON_DEFAULT_RIM_POWER       3.0

// ---------------------------------------------------------------------------
// グローバル入力(Shader.SetGlobal* で設定。マテリアルのプロパティではない)
// ---------------------------------------------------------------------------
CBUFFER_START(MSToonGlobals)
    float  _GlobalProfileEnabled;   // 1: プロファイル有効 / 0: デフォルト値を使用
    float  _GlobalSmoothness;       // 階調の硬さ (0=完全トゥーン, 1=滑らか)
    float4 _GlobalShadowColor;      // rgb: 影色, a: 影色の強さ
    float4 _GlobalAmbientTint;      // rgb: 環境光の色味, a: 環境光の強さ
    float4 _GlobalRimColor;         // rgb: リム色
    float  _GlobalRimPower;         // リムの絞り
    float  _GlobalRampWidth;        // グローバルランプの幅(px)。0 ならグローバルランプなし
CBUFFER_END

TEXTURE2D(_GlobalRampMap);
SAMPLER(sampler_GlobalRampMap);

// ---------------------------------------------------------------------------
// スタイル解決: グローバル(プロファイル) or マテリアル値 or デフォルト
// ---------------------------------------------------------------------------
struct ToonStyle
{
    float  smoothness;
    float3 shadowColor;
    float  shadowStrength;
    float3 ambientTint;
    float  ambientStrength;
    float3 rimColor;
    float  rimPower;
    bool   useGlobalRamp;
    float  globalRampWidth;
};

// useGlobalStyle: マテリアル側の「グローバルスタイルを使う」トグル(0/1)
ToonStyle ToonResolveStyle(float useGlobalStyle,
                           float localSmoothness, float3 localShadowColor, float localShadowStrength,
                           float3 localRimColor, float localRimPower)
{
    ToonStyle s;
    bool g = (useGlobalStyle > 0.5) && (_GlobalProfileEnabled > 0.5);

    s.smoothness      = g ? _GlobalSmoothness     : localSmoothness;
    s.shadowColor     = g ? _GlobalShadowColor.rgb : localShadowColor;
    s.shadowStrength  = g ? _GlobalShadowColor.a   : localShadowStrength;
    s.ambientTint     = g ? _GlobalAmbientTint.rgb : MS_TOON_DEFAULT_AMBIENT_TINT;
    s.ambientStrength = g ? _GlobalAmbientTint.a   : MS_TOON_DEFAULT_AMBIENT_STRENGTH;
    s.rimColor        = g ? _GlobalRimColor.rgb    : localRimColor;
    s.rimPower        = g ? _GlobalRimPower        : localRimPower;
    s.useGlobalRamp   = g && (_GlobalRampWidth > 0.5);
    s.globalRampWidth = _GlobalRampWidth;
    return s;
}

// ---------------------------------------------------------------------------
// 階調(ランプ)
// ---------------------------------------------------------------------------

// NdotL を 0..1 に写像してランプ参照用の値にする。影は暗い側へ押し込む。
// shadowAtten を掛けることで、影もランプの階調で表現される。
float ToonRampInput(float nDotL, float shadowAtten)
{
    return saturate(nDotL * 0.5 + 0.5) * shadowAtten;
}

// ランプのU座標を返す。
// smoothness=0 : テクセル中心でスナップ(点サンプル相当 = 完全トゥーン)
// smoothness=1 : バイリニアそのまま(滑らか)
// aa           : 1ピクセルあたりの u の変化量(境界のジャギー抑制用。ループ外で求めて渡す)
float ToonRampCoord(float u, float rampWidth, float smoothness, float aa)
{
    float w = max(rampWidth, 1.0);
    float x = u * w - 0.5;
    float i = floor(x);
    float f = x - i;
    float edge = clamp(aa * w * 0.5, 0.02, 0.5);
    float hardF = smoothstep(0.5 - edge, 0.5 + edge, f);
    float newF = lerp(hardF, f, saturate(smoothness));
    return (i + newF + 0.5) / w;
}

// 1ピクセル内の法線の変化量から、ランプ境界のアンチエイリアス幅を求める。
// 必ずループ・分岐の外(一様な制御フロー)で呼ぶこと。
float ToonRampAA(float3 normalWS)
{
    return saturate(length(fwidth(normalWS)) * 0.5);
}

// ライト1灯分の放射輝度を返す(ランプ適用済み)。
// rampColor は呼び出し側で ToonRampCoord の結果をサンプルしたもの。
float3 ToonLightRadiance(Light light, float3 rampColor)
{
    return light.color * rampColor * light.distanceAttenuation;
}

// ---------------------------------------------------------------------------
// 影色(任意): ランプ値 0 のとき shadowColor、1 のとき白へ補間する。
// 影の「色」だけを後から差し替えられるように、ランプとは分離している。
// ---------------------------------------------------------------------------
float3 ToonShadowTint(float3 rampColor, float3 shadowColor, float strength)
{
    float3 tinted = lerp(shadowColor, float3(1, 1, 1), saturate(rampColor));
    return lerp(rampColor, tinted, saturate(strength));
}

// ---------------------------------------------------------------------------
// 環境光(任意): SH の環境光に色味と強さを掛ける。
// ---------------------------------------------------------------------------
float3 ToonAmbient(float3 normalWS, float3 tint, float strength)
{
    return max(SampleSH(normalWS), 0.0) * tint * strength;
}

// ---------------------------------------------------------------------------
// リムライト(任意)
// lightMask: 0..1。ライト側のみに出したいときに ToonRampInput 相当を渡す。
// smoothness: 階調の硬さと同じ値を渡すと縁のシャープさが統一される。
// ---------------------------------------------------------------------------
float3 ToonRim(float3 normalWS, float3 viewDirWS, float3 rimColor, float rimPower,
               float smoothness, float lightMask)
{
    float nDotV = saturate(dot(normalWS, viewDirWS));
    float rim = pow(1.0 - nDotV, max(rimPower, 0.01));
    float soft = max(saturate(smoothness) * 0.5, 0.02);
    rim = smoothstep(0.5 - soft, 0.5 + soft, rim);
    return rimColor * rim * saturate(lightMask);
}

// ---------------------------------------------------------------------------
// スペキュラ(任意): Blinn-Phong をしきい値で切り出したハイライト。
// size: 0..1 (大きいほどハイライトが広い)
// 戻り値は 0..1 のマスク。ライト色・強さは呼び出し側で掛ける。
// ---------------------------------------------------------------------------
float ToonSpecular(float3 normalWS, float3 viewDirWS, float3 lightDirWS,
                   float size, float smoothness)
{
    float3 h = normalize(lightDirWS + viewDirWS);
    float nDotH = saturate(dot(normalWS, h));
    float threshold = 1.0 - saturate(size) * 0.15;
    float soft = max(saturate(smoothness) * 0.05, 0.002);
    return smoothstep(threshold - soft, threshold + soft, nDotH);
}

// ---------------------------------------------------------------------------
// 高さグラデーション(任意): ピボット基準で下ほど暗くするための係数を返す。
// 戻り値は lighting に掛ける。スペキュラ・リム・エミッションには掛けない想定。
// strength: 最下部での暗さ(0..1) / height: グラデーションの高さ(ワールド長) / offset: 明るい側への底上げ
// ---------------------------------------------------------------------------
#define MS_TOON_GRADIENT_MIN_HEIGHT 1e-4

float ToonHeightGradient(float positionWSY, float pivotY, float strength, float height, float offset)
{
    float g = saturate((positionWSY - pivotY) / max(height, MS_TOON_GRADIENT_MIN_HEIGHT) + offset);
    return lerp(1.0 - strength, 1.0, g);
}

#endif // MS_PROJECT_LIGHTING_INCLUDED
