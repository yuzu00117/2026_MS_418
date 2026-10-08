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
    }
}
