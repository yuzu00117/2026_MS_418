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
    float _EchoOriginalOcclusion;
    float _EchoOutlineOcclusion;
    float4 _EchoEffect; // enabled, through walls, whole display, time
    float4 _EchoPersistence; // enabled, seconds, whole persistence, unused
    float GetEffectCoverage(float4 mask, float4 history)
    {
        float current=_EchoEffect.z>.5?history.x:mask.g;
        float age=_EchoPersistence.z>.5?(history.y>0?max(0,_EchoEffect.w+1-history.y):60000):mask.a;
        float held=_EchoPersistence.x>.5 && _EchoPersistence.y>0?step(age,_EchoPersistence.y):0;
        float visible=_EchoEffect.y>.5?mask.r:mask.b;
        return saturate(max(current,held))*visible*_EchoEffect.x;
    }
    float GetMaskCoverage(float2 uv)
    {
        float4 mask=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_PointClamp,uv);
        float4 history=SAMPLE_TEXTURE2D_X(_EchoHistory,sampler_PointClamp,float2(.5,.5));
        return GetEffectCoverage(mask,history);
    }
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
                return float4(hit,hit>.5?_EchoEffectTiming.x+1:previous.y,0,0);
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
                float center=GetMaskCoverage(input.texcoord);
                [branch] if(center<=0) return 0;
                float inner=1;
                [unroll] for(int i=0;i<16;i++)
                {
                    float angle=i*6.2831853/16;
                    float2 uv=input.texcoord+float2(cos(angle),sin(angle))*_EchoOutlineWidth*_EchoSourceSize.zw;
                    inner=min(inner,GetMaskCoverage(uv));
                }
                float alpha=center*(1-inner)*_EchoOutlineColor.a;
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
                float4 history=SAMPLE_TEXTURE2D_X(_EchoHistory,sampler_PointClamp,float2(.5,.5));
                float4 mask=SAMPLE_TEXTURE2D_X(_EchoObjectMask,sampler_PointClamp,input.texcoord);
                float coverage=GetEffectCoverage(mask,history);
                float4 scene=SAMPLE_TEXTURE2D_X(_EchoSceneColor,sampler_LinearClamp,input.texcoord);
                float4 original=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,input.texcoord);
                return lerp(scene,original,coverage*_EchoOriginalOpacity);
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
        Pass // 7: タグアセットの塗りつぶし。部分/全体、残存、壁越しを独立指定。
        {
            ZWrite Off ZTest Always Cull Off Blend One One
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Fill
            half4 Fill(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float alpha=GetMaskCoverage(input.texcoord)*_EchoOutlineColor.a;
                return half4(_EchoOutlineColor.rgb*alpha,alpha);
            }
            ENDHLSL
        }
    }
}
