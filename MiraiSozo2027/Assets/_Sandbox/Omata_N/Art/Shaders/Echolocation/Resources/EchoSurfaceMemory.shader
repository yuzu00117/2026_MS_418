Shader "Hidden/Echo/SurfaceMemory"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Cull Off ZTest Always ZWrite Off ColorMask 0
            HLSLPROGRAM
            #pragma target 5.0
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "../EchoVolume.hlsl"
            RWTexture3D<float> _EchoSurfaceMemoryWrite : register(u1);
            float4x4 _EchoWorldToMemory;
            int _EchoMemoryAxis, _EchoMemoryResolution;
            float _EchoContactStamp;
            struct Attributes { float4 positionOS:POSITION; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 memory:TEXCOORD1; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS=TransformObjectToWorld(input.positionOS.xyz);
                output.memory=mul(_EchoWorldToMemory,float4(output.positionWS,1)).xyz;
                float2 uv=_EchoMemoryAxis==0?output.memory.xy:(_EchoMemoryAxis==1?output.memory.yz:output.memory.xz);
                output.positionCS=float4(uv*2-1,.5,1);
                return output;
            }
            float4 Frag(Varyings input):SV_Target
            {
                if(EchoCoverage(input.positionWS)>.5)
                {
                    int3 voxel=clamp(int3(input.memory*_EchoMemoryResolution),0,_EchoMemoryResolution-1);
                    _EchoSurfaceMemoryWrite[voxel]=_EchoContactStamp;
                }
                return 0;
            }
            ENDHLSL
        }
    }
}
