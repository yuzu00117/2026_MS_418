// 水面の泡用の「物体の中かどうか」の地図を描くシェーダー(WaterFoamDistanceMap が使う)。
// 真上から見た 1 枚の絵にして、物体の中なら 1、外なら 0 を書く。
// 通常のカメラは使わず、世界座標から直接クリップ座標を作る(行列の向きの取り違えを避けるため)。
Shader "Hidden/Aura/Water/FoamMask"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Opaque"
        }

        Pass
        {
            Name "FoamMask"

            // 裏向きの面も描く(物体の内側から見た面を拾うため)。
            // ZTest LEqual は、反転 Z の環境でも Unity が自動で読み替えるので、そのまま書いてよい
            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                // x, y: 地図の範囲の最小 XZ(m)、z: 1 / 範囲の一辺(1/m)
                float4 _FoamMaskArea;
                // x: 水面の高さ(m)、y: 1 / 調べる高さ(1/m)
                float4 _FoamMaskHeight;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);

                // 水面側のシェーダーは uv = (xz - 最小) * 1/範囲 で地図を読む(uv.y = 0 が画像の 0 行目)。
                // 描く側もこれと同じ向きにするため、画像の 0 行目が画面の上端になる環境
                // (UNITY_UV_STARTS_AT_TOP: D3D / Metal / Vulkan)では y を反転する。
                // 描き込み先が画像のとき _ProjectionParams.x が環境や描き方で変わりうるが、
                // この描き方(DrawMesh + 自前のクリップ座標)では環境の違いだけで決まるので、このマクロの方が確実
                float2 uv = (positionWS.xz - _FoamMaskArea.xy) * _FoamMaskArea.z;
                float2 clipXY = uv * 2.0 - 1.0;
                #if UNITY_UV_STARTS_AT_TOP
                    clipXY.y = -clipXY.y;
                #endif

                // 水面からの高さを深度に割り当てる(水面が一番手前、高いほど奥)。
                // 0〜1 に収めない(saturate しない)ことで、水面より下の部分は手前側の切り取りで
                // 水面の高さちょうどで切られる。物体を水面の高さで輪切りにしたのと同じ絵になる
                float height = positionWS.y - _FoamMaskHeight.x;
                float depth = lerp(UNITY_NEAR_CLIP_VALUE, UNITY_RAW_FAR_CLIP_VALUE, height * _FoamMaskHeight.y);

                output.positionCS = float4(clipXY, depth, 1.0);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            // 水面の高さから真上を見上げて、最初に見える面を調べる(深度テストで水面に一番近い面だけが残る)。
            // その面の外向きが上を向いていれば、物体を内側から見ていることになるので、その点は物体の中(1)。
            // 橋の床の裏のように外向きが下なら、物体の外から見上げているだけなので外(0)
            float Frag(Varyings input) : SV_Target
            {
                return input.normalWS.y > 0.0 ? 1.0 : 0.0;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
