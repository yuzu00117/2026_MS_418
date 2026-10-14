// マテリアルの照明や通常の影設定に依存せず、最初の遮断面までの距離を保存する。
Shader "Hidden/Echo/SourceShadowDepth"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            ZWrite On ZTest LEqual Cull Off Blend Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4x4 _EchoShadowCaptureVP;
            float3 _EchoShadowCaptureOrigin;
            struct A {float3 positionOS:POSITION;UNITY_VERTEX_INPUT_INSTANCE_ID};
            struct V {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;};
            V Vert(A input)
            {
                UNITY_SETUP_INSTANCE_ID(input);V o;o.positionWS=TransformObjectToWorld(input.positionOS);
                o.positionCS=mul(_EchoShadowCaptureVP,float4(o.positionWS,1));return o;
            }
            float Frag(V input):SV_Target {return distance(input.positionWS,_EchoShadowCaptureOrigin);}
            ENDHLSL
        }
    }
}
