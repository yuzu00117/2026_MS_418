Shader "Hidden/Echo/RevealContact"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Cull Off ZWrite Off ZTest Always Blend One One BlendOp Max
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #define ECHO_WHOLE_OBJECT_CONTACT 1
            #include "../EchoVolume.hlsl"
            float _EchoPatchProjection;
            float4x4 _EchoContactMatrix;
            struct A { float4 position:POSITION; float2 uv:TEXCOORD2; };
            struct V { float4 position:SV_POSITION; float3 world:TEXCOORD0; };
            V Vert(A i)
            {
                V o; o.world=TransformObjectToWorld(i.position.xyz);
                o.position=_EchoPatchProjection>.5 ? float4(i.uv*2-1,0,1) : mul(_EchoContactMatrix,float4(o.world,1));
                return o;
            }
            float Frag(V i):SV_Target { return EchoCoverage(i.world); }
            ENDHLSL
        }
        Pass
        {
            Cull Off ZWrite Off ZTest Always
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            Texture2D<float> _EchoContactSource;
            float4 Vert(uint id:SV_VertexID):SV_POSITION { return GetFullScreenTriangleVertexPosition(id); }
            float Frag(float4 position:SV_POSITION):SV_Target
            {
                int2 p=int2(position.xy)*2;
                return max(max(_EchoContactSource.Load(int3(p,0)),_EchoContactSource.Load(int3(p+int2(1,0),0))),
                    max(_EchoContactSource.Load(int3(p+int2(0,1),0)),_EchoContactSource.Load(int3(p+1,0))));
            }
            ENDHLSL
        }
    }
}

