// 対象ごとの強調色を加算してから正規化する。手前の対象で奥を消さない。
Shader "Hidden/Echo/Composite"
{
    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
    TEXTURE2D_X(_EchoHistory);
    TEXTURE2D_X(_EchoObjectMask);
    TEXTURE2D_X(_EchoSceneColor);
    float4 _EchoSourceSize;
    float4 _EchoEffectTiming; // delta, original hold, outline hold, reset
    float4 _EchoOutlineColor;
    float _EchoOutlineWidth;
    float _EchoOriginalOpacity;
    float _EchoContactRegion;
    float4 _EchoGlitchColor;
    float4 _EchoGlitchShape; // width, displacement, block height, intensity
    float4 _EchoGlitchTiming; // time, speed, duration, mode
    float _EchoOriginalOcclusion;
    float _EchoOutlineOcclusion;
    ENDHLSL
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
        Pass // 2: 4x4最大値縮小。1画素の接触も平均化で消さない。
        {
            ZWrite Off ZTest Always Cull Off Blend Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Reduce
            float4 Reduce(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                int2 origin=int2(input.positionCS.xy)*4;
                float hit=0;
                [unroll] for(int y=0;y<4;y++) [unroll] for(int x=0;x<4;x++)
                {
                    int2 p=min(origin+int2(x,y),int2(_EchoSourceSize.xy)-1);
                    hit=max(hit,LOAD_TEXTURE2D_X(_BlitTexture,p).g);
                }
                return hit.xxxx;
            }
            ENDHLSL
        }
        Pass // 3: GPU上で最後の接触からの残存時間を管理。
        {
            ZWrite Off ZTest Always Cull Off Blend Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment History
            float4 History(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float hit=step(.0001,SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_PointClamp,float2(.5,.5)).g);
                float4 previous=SAMPLE_TEXTURE2D_X(_EchoHistory,sampler_PointClamp,float2(.5,.5));
                if(_EchoEffectTiming.w>.5) previous=0;
                float2 remaining=hit>.5?max(.00001,_EchoEffectTiming.yz):max(0,previous.xy-_EchoEffectTiming.x);
                float active=step(.000001,remaining.x);
                float age=active>.5 && previous.w>.5?previous.z+_EchoEffectTiming.x:0;
                return float4(remaining,age,active);
            }
            ENDHLSL
        }
        Pass // 4: シルエットの内側だけを描く。波の境界は輪郭にしない。
        {
            ZWrite Off ZTest Always Cull Off Blend One One
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Outline
            half4 Outline(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float active=step(.000001,SAMPLE_TEXTURE2D_X(_EchoHistory,sampler_PointClamp,float2(.5,.5)).y);
                float4 center=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_PointClamp,input.texcoord);
                float inner=1;
                [unroll] for(int i=0;i<16;i++)
                {
                    float angle=i*6.2831853/16;
                    float2 uv=input.texcoord+float2(cos(angle),sin(angle))*_EchoOutlineWidth*_EchoSourceSize.zw;
                    inner=min(inner,SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_PointClamp,uv).r);
                }
                float visible=_EchoOutlineOcclusion>.5?center.b:center.r;
                float alpha=center.r*visible*(1-inner)*active*_EchoOutlineColor.a;
                return half4(_EchoOutlineColor.rgb*alpha,alpha);
            }
            ENDHLSL
        }
        Pass // 5: 本来のシェーダーで描いた画像を接触状態・表示不透明度で合成。
        {
            ZWrite Off ZTest Always Cull Off Blend Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Original
            half4 Original(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float active=_EchoContactRegion>.5?1:SAMPLE_TEXTURE2D_X(_EchoHistory,sampler_PointClamp,float2(.5,.5)).w;
                float4 mask=SAMPLE_TEXTURE2D_X(_EchoObjectMask,sampler_PointClamp,input.texcoord);
                float visible=_EchoContactRegion>.5?(_EchoOriginalOcclusion>.5?mask.a:mask.g):(_EchoOriginalOcclusion>.5?mask.b:mask.r);
                float4 scene=SAMPLE_TEXTURE2D_X(_EchoSceneColor,sampler_LinearClamp,input.texcoord);
                float4 original=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,input.texcoord);
                return lerp(scene,original,active*visible*_EchoOriginalOpacity);
            }
            ENDHLSL
        }
        Pass // 6: 元の透明マテリアルも正しい背景とブレンドできるよう背景をコピー。
        {
            ZWrite Off ZTest Always Cull Off Blend Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Copy
            half4 Copy(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,input.texcoord);
            }
            ENDHLSL
        }
        Pass // 7: 表示領域の内側の境界を断片化。通常の輪郭線は描かない。
        {
            ZWrite Off ZTest Always Cull Off Blend One One
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Glitch
            float GetBoundaryMask(float2 uv)
            {
                float4 mask=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_PointClamp,uv);
                return _EchoContactRegion>.5?(_EchoOriginalOcclusion>.5?mask.a:mask.g):(_EchoOriginalOcclusion>.5?mask.b:mask.r);
            }
            float GetGlitchEdge(float2 uv)
            {
                float center=GetBoundaryMask(uv);
                float inner=1;
                [unroll] for(int i=0;i<8;i++)
                {
                    float angle=i*6.2831853/8;
                    inner=min(inner,GetBoundaryMask(uv+float2(cos(angle),sin(angle))*_EchoGlitchShape.x*_EchoSourceSize.zw));
                }
                return saturate(center-inner);
            }
            half4 Glitch(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float4 history=SAMPLE_TEXTURE2D_X(_EchoHistory,sampler_PointClamp,float2(.5,.5));
                float gate=_EchoContactRegion>.5?1:history.w;
                if(_EchoGlitchTiming.w<.5) gate=1;
                if(_EchoGlitchTiming.w>1.5) gate*=1-step(_EchoGlitchTiming.z,history.z);
                float tick=floor(_EchoGlitchTiming.x*_EchoGlitchTiming.y);
                float band=floor(input.positionCS.y/max(1,_EchoGlitchShape.z));
                float noise=frac(sin(dot(float2(band,tick),float2(12.9898,78.233)))*43758.5453);
                float2 shift=float2((noise-.5)*2*_EchoGlitchShape.y*_EchoSourceSize.z,0);
                float edge=max(GetGlitchEdge(input.texcoord),GetGlitchEdge(input.texcoord+shift));
                float alpha=edge*GetBoundaryMask(input.texcoord)*step(1-_EchoGlitchShape.w,noise)*gate*_EchoGlitchColor.a;
                float3 tint=lerp(_EchoGlitchColor.rgb,_EchoGlitchColor.brg,noise);
                return half4(tint*alpha,alpha);
            }
            ENDHLSL
        }
    }
}
