#ifndef MS_TOON_OUTLINE_INCLUDED
#define MS_TOON_OUTLINE_INCLUDED

// ============================================================================
// ToonOutline.hlsl
// 裏面押し出し法の輪郭線用の部品。マテリアル定数(CBUFFER)には依存せず、
// 値はすべて引数で受け取る(どのシェーダーからも再利用できるようにするため)。
// URP の Core.hlsl(GetVertexNormalInputs 等)を先に include しておくこと。
//
//   GetOutlineDirectionWS : 押し出し方向(ワールド空間)の選択
//                           (焼き込みスムース法線 → 中心からの向き → 通常法線)
// ============================================================================

// 「実質ゼロ」とみなすしきい値(法線の長さの2乗に使う)
#define TOON_OUTLINE_THRESHOLD 1e-6
// UV3.w に書かれた「焼き込み済み」の目印。ToonSmoothNormalBaker の *Marker と一致させること。
// UV3 を持たないメッシュでは 0 ではなく別チャンネル(UV0 等)の値が届くことがあり、
// それを法線とみなすと押し出しが一方向に揃って輪郭線がずれるため、目印で判定する。
#define TOON_OUTLINE_MARKER_TANGENT_SPACE 2.0 // タンジェント空間(スキンメッシュの骨の回転に追従する)
#define TOON_OUTLINE_MARKER_OBJECT_SPACE  3.0 // オブジェクト空間(タンジェントを用意できないメッシュ用)

bool IsOutlineMarker(float w, float marker)
{
    return abs(w - marker) < 0.5;
}

// 押し出し方向(ワールド空間、正規化済み)を次の優先順で選ぶ(角で輪郭線が割れないようにするため)。
//   1. 焼き込みスムース法線(UV3.xyz)。w の目印で座標の種類を判定する。
//      タンジェント空間のものは、その頂点の現在の (tangent, bitangent, normal) で戻すので、
//      スキンメッシュで骨が回っても向きが追従する。
//   2. 焼かれていないときは、原点(モデル中心)から頂点への向き。
//      Unity 標準の Cube/Cylinder など、中心が原点で凹みの無い形なら角でも線がつながる。
//      凹んだ形では太さがゆがむが、焼き込み無しのメッシュに限られる。
//   3. 頂点が原点上にあり向きが決まらないときは通常法線。
float3 GetOutlineDirectionWS(float3 normalOS, float4 tangentOS, float4 bakedNormal, float3 positionOS)
{
    bool hasBaked = dot(bakedNormal.xyz, bakedNormal.xyz) > TOON_OUTLINE_THRESHOLD;
    if (hasBaked && IsOutlineMarker(bakedNormal.w, TOON_OUTLINE_MARKER_TANGENT_SPACE))
    {
        VertexNormalInputs axes = GetVertexNormalInputs(normalOS, tangentOS);
        return normalize(bakedNormal.x * axes.tangentWS
                       + bakedNormal.y * axes.bitangentWS
                       + bakedNormal.z * axes.normalWS);
    }
    if (hasBaked && IsOutlineMarker(bakedNormal.w, TOON_OUTLINE_MARKER_OBJECT_SPACE))
        return TransformObjectToWorldNormal(bakedNormal.xyz);
    if (dot(positionOS, positionOS) > TOON_OUTLINE_THRESHOLD)
        return TransformObjectToWorldNormal(positionOS);
    return TransformObjectToWorldNormal(normalOS);
}

#endif // MS_TOON_OUTLINE_INCLUDED
