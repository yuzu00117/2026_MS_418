// 元マテリアルを変更せず、全サブメッシュの到達した表面だけを再描画する。
Shader "Hidden/Echo/Highlight"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Cull Off ZWrite On ZTest Less Blend Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "EchoVolume.hlsl"
            struct Attributes { float4 positionOS:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS=TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.positionWS);return o;
            }
            half4 Frag(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float coverage=EchoCoverage(input.positionWS);
                if(coverage<=.0001||_EchoTint.a<=0) discard;
                if(_EchoOcclusion>0.5)
                {
                    float raw=SampleSceneDepth(GetNormalizedScreenSpaceUV(input.positionCS));
                    // 正射影でも利用できるようワールド位置からアイ深度を復元する。
                    #if !UNITY_REVERSED_Z
                    raw=lerp(UNITY_NEAR_CLIP_VALUE,1,raw);
                    #endif
                    float3 sceneWS=ComputeWorldSpacePosition(GetNormalizedScreenSpaceUV(input.positionCS),raw,UNITY_MATRIX_I_VP);
                    float sceneEye=-TransformWorldToView(sceneWS).z;
                    float ourEye=-TransformWorldToView(input.positionWS).z;
                    if(ourEye>sceneEye+0.001)discard;
                }
                // 合成時の二重アルファ乗算を避けるためRGBを事前乗算する。
                return half4(_EchoTint.rgb*_EchoTint.a,_EchoTint.a)*coverage;
            }
            ENDHLSL
        }
        Pass
        {
            Name "ObjectMask"
            // R: 全体のシルエット、B: カメラから見える表面。
            // 波との接触はWorldContactパスでカメラから独立して判定する。
            Cull Back ZWrite On ZTest LEqual Blend Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragMask
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "EchoVolume.hlsl"
            struct Attributes { float4 positionOS:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; UNITY_VERTEX_OUTPUT_STEREO };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.positionWS=TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS=TransformWorldToHClip(o.positionWS);
                return o;
            }
            Texture3D<float> _EchoSurfaceMemory;
            float4x4 _EchoWorldToMemory;
            float _EchoContactTime, _EchoHasSurfaceMemory;
            float GetContactAge(float3 world)
            {
                if(_EchoHasSurfaceMemory<.5) return 60000;
                float3 uv=mul(_EchoWorldToMemory,float4(world,1)).xyz;
                uint width,height,depth; _EchoSurfaceMemory.GetDimensions(width,height,depth);
                int3 baseVoxel=int3(floor(uv*float3(width,height,depth)-.5));
                float stamp=0;
                [unroll] for(int z=0;z<2;z++) [unroll] for(int y=0;y<2;y++) [unroll] for(int x=0;x<2;x++)
                    stamp=max(stamp,_EchoSurfaceMemory.Load(int4(clamp(baseVoxel+int3(x,y,z),0,int3(width,height,depth)-1),0)));
                return stamp>0?max(0,_EchoContactTime+1-stamp):60000;
            }
            half4 FragMask(Varyings input):SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float visible=1;
                if(_EchoOcclusion>.5)
                {
                    float2 uv=GetNormalizedScreenSpaceUV(input.positionCS);
                    float raw=SampleSceneDepth(uv);
                    #if !UNITY_REVERSED_Z
                    raw=lerp(UNITY_NEAR_CLIP_VALUE,1,raw);
                    #endif
                    float3 sceneWS=ComputeWorldSpacePosition(uv,raw,UNITY_MATRIX_I_VP);
                    visible=step(-TransformWorldToView(input.positionWS).z,-TransformWorldToView(sceneWS).z+.001);
                }
                float coverage=EchoCoverage(input.positionWS);
                return half4(1,coverage,visible,GetContactAge(input.positionWS));
            }
            ENDHLSL
        }
        Pass
        {
            Name "WorldContact"
            Cull Off ZWrite Off ZTest Always Blend One One BlendOp Max
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ContactVert
            #pragma fragment ContactFrag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #define ECHO_WHOLE_OBJECT_CONTACT 1
            #include "EchoVolume.hlsl"
            float4x4 _EchoContactMatrix;
            struct Attributes { float4 positionOS:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; };
            Varyings ContactVert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                o.positionWS=TransformObjectToWorld(input.positionOS.xyz);
                o.positionCS=mul(_EchoContactMatrix,float4(o.positionWS,1));
                return o;
            }
            half4 ContactFrag(Varyings input):SV_Target
            {
                float hit=EchoCoverage(input.positionWS);
                return hit.xxxx;
            }
            ENDHLSL
        }
    }
}
