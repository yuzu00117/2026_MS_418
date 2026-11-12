#ifndef MS_TOON_OUTLINE_INCLUDED
#define MS_TOON_OUTLINE_INCLUDED

// ============================================================================
// ToonOutline.hlsl
// 裏面押し出し法の輪郭線用の部品。マテリアル定数(CBUFFER)には依存せず、
// 値はすべて引数で受け取る(どのシェーダーからも再利用できるようにするため)。
//
//   SelectOutlineNormalOS : 押し出し方向の選択(焼き込みスムース法線 → 中心からの向き → 通常法線)
//   ExtrudeOutlineOS      : オブジェクト空間で法線方向に押し出す(太さはモデル上の長さ)
// ============================================================================

// 「実質ゼロ」とみなすしきい値(法線の長さの2乗に使う)
#define TOON_OUTLINE_THRESHOLD 1e-6
// UV3.w に書かれた「焼き込み済み」の目印。ToonSmoothNormalBaker.BakedMarker と一致させること。
// UV3 を持たないメッシュでは 0 ではなく別チャンネル(UV0 等)の値が届くことがあり、
// それを法線とみなすと押し出しが一方向に揃って輪郭線がずれるため、目印で判定する。
#define TOON_OUTLINE_BAKED_MARKER 2.0

// 押し出し方向を次の優先順で選ぶ(角で輪郭線が割れないようにするため)。
//   1. 焼き込みスムース法線(UV3.xyz)。w が目印と一致するもの(読み込み時に焼いたモデル)だけ使う。
//   2. 焼かれていないときは、原点(モデル中心)から頂点への向き。
//      Unity 標準の Cube/Cylinder など、中心が原点で凹みの無い形なら角でも線がつながる。
//      凹んだ形では太さがゆがむが、焼き込み無しのメッシュに限られる。
//   3. 頂点が原点上にあり向きが決まらないときは通常法線。
float3 SelectOutlineNormalOS(float3 normalOS, float4 bakedNormalOS, float3 positionOS)
{
    bool isBaked = abs(bakedNormalOS.w - TOON_OUTLINE_BAKED_MARKER) < 0.5
                && dot(bakedNormalOS.xyz, bakedNormalOS.xyz) > TOON_OUTLINE_THRESHOLD;
    if (isBaked)
        return normalize(bakedNormalOS.xyz);
    if (dot(positionOS, positionOS) > TOON_OUTLINE_THRESHOLD)
        return normalize(positionOS);
    return normalOS;
}

// 頂点をオブジェクト空間で方向 directionOS(正規化済み)へ outlineWidth だけ押し出す。
// オブジェクト空間で行うので太さはモデル上の長さになり、遠いほど細く・拡大したオブジェクトほど太く見える。
float3 ExtrudeOutlineOS(float3 positionOS, float3 directionOS, float outlineWidth)
{
    return positionOS + directionOS * outlineWidth;
}

#endif // MS_TOON_OUTLINE_INCLUDED
