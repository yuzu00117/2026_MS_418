// 範囲内の床・壁、波の先端、空間の境界線を表示する。検出対象のタグとは独立。
Shader "Hidden/Echo/Range"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
        #include "EchoVolume.hlsl"
        float4 _EchoRangeFill,_EchoRangeWave,_EchoRangeErase,_EchoRangeLine;
        float _EchoRangeWidth;
        int _EchoBoundaryWave;
        float3 RangeWorld(float2 uv,out bool valid)
        {
            float raw=SampleSceneDepth(uv);
            #if UNITY_REVERSED_Z
            valid=raw>0.000001;
            #else
            valid=raw<0.999999;raw=lerp(UNITY_NEAR_CLIP_VALUE,1,raw);
            #endif
            return ComputeWorldSpacePosition(uv,raw,UNITY_MATRIX_I_VP);
        }
        ENDHLSL
        Pass
        {
            ZWrite Off ZTest Always Cull Off Blend One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            half4 Frag(Varyings input):SV_Target
            {
                bool valid;float3 p=RangeWorld(GetNormalizedScreenSpaceUV(input.positionCS),valid);
                if(!valid)discard;
                float outer=0,inner=0,coverage=0;bool inside=false;
                // 重複部分は塗りを一度だけ行い、各波の先端のうち最も強いものを表示する。
                [loop] for(int i=0;i<_EchoPulseCount;i++)if(EchoContainsAt(p,i))
                {
                    inside=true;float distance=length(p-_EchoOrigins[i].xyz);float4 volume=_EchoVolumes[i];
                    coverage=1;
                    outer=max(outer,volume.w*(1-smoothstep(0,_EchoRangeWidth,volume.x-distance)));
                    inner=max(inner,volume.z*(1-smoothstep(0,_EchoRangeWidth,distance-volume.y)));
                }
                if(!inside)discard;
                half4 color=lerp(_EchoRangeFill,_EchoRangeWave,outer);
                color=lerp(color,_EchoRangeErase,inner);
                return half4(color.rgb*color.a,color.a)*coverage;
            }
            ENDHLSL
        }
        Pass
        {
            ZWrite Off ZTest Always Cull Off Blend One OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex LineVert
            #pragma target 3.5
            #pragma fragment LineFrag
            struct A {float3 positionOS:POSITION;};
            struct V {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;};
            V LineVert(A input){V o;o.positionWS=TransformObjectToWorld(input.positionOS);o.positionCS=TransformWorldToHClip(o.positionWS);return o;}
            half4 LineFrag(V input):SV_Target
            {
                bool valid;float3 scene=RangeWorld(GetNormalizedScreenSpaceUV(input.positionCS),valid);
                if(valid&&-TransformWorldToView(input.positionWS).z>-TransformWorldToView(scene).z+.015)discard;
                if(!EchoSourceVisible(input.positionWS,_EchoBoundaryWave))discard;
                return half4(_EchoRangeLine.rgb*_EchoRangeLine.a,_EchoRangeLine.a);
            }
            ENDHLSL
        }
    }
}
