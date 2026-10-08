// 対象ごとの強調色を加算してから正規化する。手前の対象で奥を消さない。
Shader "Hidden/Echo/Composite"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZWrite Off ZTest Always Cull Off
            Blend One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            half4 Frag(Varyings input):SV_Target
            {
                float4 sum=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,input.texcoord);
                // RGBは色×アルファの合計、Aは重みの合計。1対象なら従来と同じ色。
                float alpha=saturate(sum.a);
                return half4(sum.rgb/max(sum.a,0.00001)*alpha,alpha);
            }
            ENDHLSL
        }
        Pass
        {
            // 1つのRenderer内で最前面だけを選んだ画像を蓄積する。
            // 裏面・三角形の描画順・サブメッシュ数による色の偏りを防ぐ。
            ZWrite Off ZTest Always Cull Off Blend One One
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Accumulate
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            half4 Accumulate(Varyings input):SV_Target
            {return SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,input.texcoord);}
            ENDHLSL
        }
    }
}
