#ifndef MS_TOON_OUTLINE_INCLUDED
#define MS_TOON_OUTLINE_INCLUDED

// ============================================================================
// ToonOutline.hlsl
// 裏面押し出し法の輪郭線用の部品。マテリアル定数(CBUFFER)には依存せず、
// 値はすべて引数で受け取る(どのシェーダーからも再利用できるようにするため)。
//
//   SelectOutlineNormalOS : 押し出しに使う法線の選択(通常法線 or 焼き込みスムース法線)
//   ComputeOutlineZPull   : 深度干渉を避けるためのビュー空間 z のずらし量
//   ExtrudeOutlineCS      : クリップ空間での押し出し
// ============================================================================

// 0 除算を避けるための下限(焦点距離・ベクトル長に使う)
#define TOON_OUTLINE_EPSILON   1e-8
// 「実質ゼロ」とみなすしきい値(法線の長さの2乗・ZPull 値に使う)
#define TOON_OUTLINE_THRESHOLD 1e-6

// 焼き込みスムース法線(UV3)は useBakedNormal = 1 のときだけ使う。
// 未焼き込みメッシュの UV3 は 0 なので、ゼロ長のときは通常法線にフォールバックする。
float3 SelectOutlineNormalOS(float3 normalOS, float3 smoothNormalOS, float useBakedNormal)
{
    if (useBakedNormal > 0.5 && dot(smoothNormalOS, smoothNormalOS) > TOON_OUTLINE_THRESHOLD)
        return normalize(smoothNormalOS);
    return normalOS;
}

// ビュー空間 z に加算する量を返す(カメラ側が +)。輪郭幅に比例させ、Min/Max で制限する。
// zPull を負にすると逆向き(奥側)にずらす。
float ComputeOutlineZPull(float viewDepth, float focalLength, float outlineWidth,
                          float zPull, float zPullMin, float zPullMax)
{
    if (abs(zPull) <= TOON_OUTLINE_THRESHOLD)
        return 0.0;

    // 世界空間での輪郭幅(画面高 2 がビュー深度 viewDepth 相当になる換算)
    float worldWidth = 2.0 * outlineWidth * viewDepth / focalLength;
    float pull = clamp(abs(zPull) * worldWidth + zPullMin, 0.0, zPullMax);
    return pull * ((zPull < 0.0) ? -1.0 : 1.0);
}

// クリップ空間押し出し: 画面上の太さが距離に依らず一定(outlineWidth = 画面高に対する比率)
// focalLength は |P11|。呼び出し側で epsilon 以上にして渡すこと。
float4 ExtrudeOutlineCS(float4 positionCS, float3 normalVS, float focalLength, float outlineWidth)
{
    // 投影行列を通して Y 反転(RT描画時)を吸収する
    // P00 = f/aspect なのでアスペクトもここで掛かる(以降で aspect 除算はしない)
    // 割るのは view xy の長さと |P11|(= f) → 太さが方向・画角に依らず一定になる
    float2 nxy = normalVS.xy;
    float2 dirCS = mul((float2x2)UNITY_MATRIX_P, nxy);
    // ゼロ長でも 0 除算しないよう epsilon を入れる
    float2 dir = dirCS * (rsqrt(max(dot(nxy, nxy), TOON_OUTLINE_EPSILON)) / focalLength);
    float2 offset = dir * (outlineWidth * 2.0); // NDC は高さ 2 なので x2
    positionCS.xy += offset * positionCS.w;
    return positionCS;
}

#endif // MS_TOON_OUTLINE_INCLUDED
