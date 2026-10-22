// プロジェクト共通トゥーンシェーダー(URP / HLSL 手書き)
//
// - ライティング処理は Includes/ProjectLighting.hlsl に集約。絵作りを変える場合は基本そちらを触る。
// - 全体の見た目は _Global* (ToonStyleProfile -> ToonStyleApplier) で一括変更できる。
//   マテリアルの "Use Global Style" をオフにすると、そのマテリアルだけ個別値で動く。
// - Mobile 向け: "Lite" をオンにすると 法線マップ / リム / スペキュラ / 追加ライト を無効化する。
// - 輪郭線・ポスト用に DepthOnly / DepthNormals パスを持つ。
// - 裏面押し出し法の Outline パスを持つ(処理は Includes/ToonOutline.hlsl)。
//   クリップ空間押し出しで、画面上の太さ(画面高に対する比率)が距離に依らず一定。
//   地面等との深度干渉を避けるためカメラ側へ引き寄せて描く。_OutlineZPull を負にすると逆向き。
//   スムース法線(UV3, ToonSmoothNormalBaker)は _OutlineUseBakedNormal = 1 のときだけ使う(焼き込み済みメッシュのみ)。既定は通常の法線。
// - オブジェクト下側ほど暗くする高さグラデーション(Gradient 項目)を持つ。
// - 未対応(意図的): ライトマップ(焼き込みGI)、透過、アルファテスト。必要になったら追加する。
Shader "MS/Toon"
{
    Properties
    {
        [Header(Base)]
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1, 1, 1, 1)

        [Header(Normal)]
        [Toggle(_NORMALMAP)] _UseNormalMap ("Use Normal Map", Float) = 0
        [Normal] _BumpMap ("Normal Map", 2D) = "bump" {}
        _BumpScale ("Normal Scale", Float) = 1

        [Header(Emission)]
        [Toggle(_EMISSION)] _UseEmission ("Use Emission", Float) = 0
        _EmissionMap ("Emission Map", 2D) = "white" {}
        [HDR] _EmissionColor ("Emission Color", Color) = (0, 0, 0, 1)

        [Header(Toon Ramp)]
        [NoScaleOffset] _RampMap ("Ramp", 2D) = "white" {}
        _Smoothness ("Smoothness", Range(0, 1)) = 0.05
        _ShadowColor ("Shadow Color", Color) = (0.55, 0.55, 0.72, 1)
        _ShadowStrength ("Shadow Color Strength", Range(0, 1)) = 1

        [Header(Rim)]
        [Toggle(_TOON_RIM)] _UseRim ("Use Rim", Float) = 0
        _RimColor ("Rim Color", Color) = (1, 1, 1, 1)
        _RimPower ("Rim Power", Range(0.5, 8)) = 3
        _RimIntensity ("Rim Intensity", Range(0, 2)) = 0.5

        [Header(Specular)]
        [Toggle(_TOON_SPECULAR)] _UseSpecular ("Use Specular", Float) = 0
        _SpecularSize ("Specular Size", Range(0, 1)) = 0.5
        _SpecularIntensity ("Specular Intensity", Range(0, 2)) = 0.5

        [Header(Outline)]
        _OutlineColor ("Outline Color", Color) = (0.1, 0.1, 0.1, 1)
        _OutlineWidth ("Outline Width", Range(0, 0.01)) = 0.002
        _OutlineZPull ("Outline Z Pull", Range(-10, 10)) = 2
        _OutlineZPullMin ("Outline Z Pull Min", Range(0, 0.1)) = 0.005
        _OutlineZPullMax ("Outline Z Pull Max", Range(0, 1)) = 0.2
        [Toggle] _OutlineUseBakedNormal ("Outline Use Baked Normal", Float) = 0

        [Header(Gradient)]
        _GradientStrength ("Gradient Strength", Range(0, 1)) = 0.3
        _GradientHeight ("Gradient Height", Float) = 1
        _GradientOffset ("Gradient Offset", Range(0, 1)) = 0.5

        [Header(Style and Quality)]
        [Toggle] _UseGlobalStyle ("Use Global Style", Float) = 1
        [Toggle(_TOON_LITE)] _Lite ("Lite", Float) = 0
        [Enum(UnityEngine.Rendering.CullMode)] _Cull ("Cull", Float) = 2
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        // 全パス共通のマテリアル定数。SRP Batcher 互換のため全パスで同一の CBUFFER を宣言する。
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            float4 _BaseColor;
            float4 _BumpMap_ST;
            float  _BumpScale;
            float4 _EmissionColor;
            float4 _RampMap_TexelSize;
            float  _Smoothness;
            float4 _ShadowColor;
            float  _ShadowStrength;
            float4 _RimColor;
            float  _RimPower;
            float  _RimIntensity;
            float  _SpecularSize;
            float  _SpecularIntensity;
            float  _UseGlobalStyle;
            float4 _OutlineColor;
            float  _OutlineWidth;
            float  _OutlineZPull;
            float  _OutlineZPullMin;
            float  _OutlineZPullMax;
            float  _OutlineUseBakedNormal;
            float  _GradientStrength;
            float  _GradientHeight;
            float  _GradientOffset;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5

            #pragma vertex ToonVertex
            #pragma fragment ToonFragment

            // Material keywords (必要最小限)
            #pragma shader_feature_local _NORMALMAP
            #pragma shader_feature_local_fragment _EMISSION
            #pragma shader_feature_local_fragment _TOON_RIM
            #pragma shader_feature_local_fragment _TOON_SPECULAR
            #pragma shader_feature_local _TOON_LITE

            // Universal Pipeline keywords
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #pragma multi_compile_instancing

            #include "../Includes/ProjectLighting.hlsl"

            TEXTURE2D(_BaseMap);     SAMPLER(sampler_BaseMap);
            TEXTURE2D(_BumpMap);     SAMPLER(sampler_BumpMap);
            TEXTURE2D(_EmissionMap); SAMPLER(sampler_EmissionMap);
            TEXTURE2D(_RampMap);     SAMPLER(sampler_RampMap);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float4 tangentOS  : TANGENT;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                #if defined(_NORMALMAP) && !defined(_TOON_LITE)
                    float4 tangentWS : TEXCOORD3; // w: bitangent sign
                #endif
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    float4 shadowCoord : TEXCOORD4;
                #endif
                float fogFactor   : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings ToonVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS, input.tangentOS);

                output.positionCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.normalWS = normalInput.normalWS;
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                #if defined(_NORMALMAP) && !defined(_TOON_LITE)
                    real sign = input.tangentOS.w * GetOddNegativeScale();
                    output.tangentWS = float4(normalInput.tangentWS, sign);
                #endif
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    output.shadowCoord = ComputeScreenPos(vertexInput.positionCS);
                #endif
                output.fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
                return output;
            }

            // ランプ参照。グローバルランプが有効ならそちら、無ければマテリアルのランプ。
            float3 SampleRamp(ToonStyle style, float u, float aa)
            {
                float3 ramp;
                if (style.useGlobalRamp)
                {
                    float cg = ToonRampCoord(u, style.globalRampWidth, style.smoothness, aa);
                    ramp = SAMPLE_TEXTURE2D_LOD(_GlobalRampMap, sampler_GlobalRampMap, float2(cg, 0.5), 0).rgb;
                }
                else
                {
                    float cl = ToonRampCoord(u, _RampMap_TexelSize.z, style.smoothness, aa);
                    ramp = SAMPLE_TEXTURE2D_LOD(_RampMap, sampler_RampMap, float2(cl, 0.5), 0).rgb;
                }
                return ramp;
            }

            half4 ToonFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // --- Surface ---
                float4 baseSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv);
                float3 albedo = baseSample.rgb * _BaseColor.rgb;

                float3 normalWS = normalize(input.normalWS);
                #if defined(_NORMALMAP) && !defined(_TOON_LITE)
                    float3 tangentWS = normalize(input.tangentWS.xyz);
                    float3 bitangentWS = input.tangentWS.w * cross(normalWS, tangentWS);
                    float3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                    normalWS = normalize(TransformTangentToWorld(normalTS, float3x3(tangentWS, bitangentWS, normalWS)));
                #endif

                float3 viewDirWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

                ToonStyle style = ToonResolveStyle(_UseGlobalStyle, _Smoothness, _ShadowColor.rgb, _ShadowStrength,
                                                   _RimColor.rgb, _RimPower);
                float aa = ToonRampAA(normalWS); // 一様な制御フローで計算しておく

                // --- Main light ---
                #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
                    float4 shadowCoord = input.shadowCoord;
                #else
                    float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #endif
                Light mainLight = GetMainLight(shadowCoord, input.positionWS, half4(1, 1, 1, 1));

                float mainNdotL = dot(normalWS, mainLight.direction);
                float mainRampInput = ToonRampInput(mainNdotL, mainLight.shadowAttenuation);
                float3 mainRamp = SampleRamp(style, mainRampInput, aa);
                mainRamp = ToonShadowTint(mainRamp, style.shadowColor, style.shadowStrength);

                float3 lighting = ToonLightRadiance(mainLight, mainRamp);
                lighting += ToonAmbient(normalWS, style.ambientTint, style.ambientStrength);

                // --- Additional lights ---
                #if defined(_ADDITIONAL_LIGHTS) && !defined(_TOON_LITE)
                    InputData inputData = (InputData)0;
                    inputData.positionWS = input.positionWS;
                    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                    uint pixelLightCount = GetAdditionalLightsCount();

                    #if USE_CLUSTER_LIGHT_LOOP
                    [loop] for (uint dirIndex = 0; dirIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); dirIndex++)
                    {
                        Light dirLight = GetAdditionalLight(dirIndex, input.positionWS, half4(1, 1, 1, 1));
                        float3 r = SampleRamp(style, ToonRampInput(dot(normalWS, dirLight.direction), dirLight.shadowAttenuation), aa);
                        lighting += ToonLightRadiance(dirLight, r);
                    }
                    #endif

                    LIGHT_LOOP_BEGIN(pixelLightCount)
                        Light addLight = GetAdditionalLight(lightIndex, input.positionWS, half4(1, 1, 1, 1));
                        float3 r = SampleRamp(style, ToonRampInput(dot(normalWS, addLight.direction), addLight.shadowAttenuation), aa);
                        lighting += ToonLightRadiance(addLight, r);
                    LIGHT_LOOP_END
                #endif

                // 高さグラデーション。ピボット基準で下ほど暗くする(スペキュラ・リム・エミッションには掛けない)
                lighting *= ToonHeightGradient(input.positionWS.y, UNITY_MATRIX_M._m13, _GradientStrength, _GradientHeight, _GradientOffset);

                float3 color = albedo * lighting;

                // --- Optional: specular / rim (呼ばなければコストゼロ) ---
                #if defined(_TOON_SPECULAR) && !defined(_TOON_LITE)
                    float specMask = ToonSpecular(normalWS, viewDirWS, mainLight.direction, _SpecularSize, style.smoothness);
                    color += mainLight.color * specMask * mainLight.shadowAttenuation * _SpecularIntensity;
                #endif

                #if defined(_TOON_RIM) && !defined(_TOON_LITE)
                    color += ToonRim(normalWS, viewDirWS, style.rimColor, style.rimPower, style.smoothness,
                                     saturate(mainNdotL * 0.5 + 0.5)) * _RimIntensity;
                #endif

                #if defined(_EMISSION)
                    color += SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, input.uv).rgb * _EmissionColor.rgb;
                #endif

                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        // 裏面押し出し法の輪郭線。Cull Front で裏面だけを描き、法線方向に押し出す。
        // 深度はカメラ側へ引き寄せる(_OutlineZPull × 輪郭幅、Min/Max で制限)。地面等との干渉を避けるため。
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex OutlineVertex
            #pragma fragment OutlineFragment

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Fog.hlsl"
            #pragma multi_compile_instancing

            #include "../Includes/ToonOutline.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float3 smoothNormalOS : TEXCOORD3; // ToonSmoothNormalBaker が焼いたスムース法線
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float fogFactor   : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings OutlineVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                // 太さ 0 のときは画面外に飛ばして描画しない
                if (_OutlineWidth <= 0.0)
                {
                    output.positionCS = float4(2, 2, 2, 1);
                    return output;
                }

                float focalLength = max(abs(UNITY_MATRIX_P[1][1]), TOON_OUTLINE_EPSILON);

                float3 extrudeOS = SelectOutlineNormalOS(input.normalOS, input.smoothNormalOS, _OutlineUseBakedNormal);
                float3 normalVS = TransformWorldToViewDir(TransformObjectToWorldNormal(extrudeOS));

                float3 positionVS = TransformWorldToView(TransformObjectToWorld(input.positionOS.xyz));
                float viewDepth = -positionVS.z; // ビュー空間の前方は -z
                positionVS.z += ComputeOutlineZPull(viewDepth, focalLength, _OutlineWidth,
                                                    _OutlineZPull, _OutlineZPullMin, _OutlineZPullMax);

                float4 positionCS = TransformWViewToHClip(positionVS);
                output.positionCS = ExtrudeOutlineCS(positionCS, normalVS, focalLength, _OutlineWidth);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 OutlineFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 color = MixFog(_OutlineColor.rgb, input.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex ShadowVertex
            #pragma fragment ShadowFragment

            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            // URP がシャドウマップ生成時に設定する(法線バイアス用)
            float3 _LightDirection;
            float3 _LightPosition;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings ShadowVertex(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);

                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
                #else
                    float3 lightDirectionWS = _LightDirection;
                #endif

                float4 positionCS = TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS));
                output.positionCS = ApplyShadowClamping(positionCS);
                return output;
            }

            half4 ShadowFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                return 0;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            ZWrite On
            ColorMask R
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthVertex
            #pragma fragment DepthFragment
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half DepthFragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                return input.positionCS.z;
            }
            ENDHLSL
        }

        // _CameraNormalsTexture 用。後から輪郭線(法線ベース)やSSAOを足すときに使われる。
        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }

            ZWrite On
            Cull [_Cull]

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex DepthNormalsVertex
            #pragma fragment DepthNormalsFragment

            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/RenderingLayers.hlsl"
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS   : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings DepthNormalsVertex(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            void DepthNormalsFragment(
                Varyings input
                , out half4 outNormalWS : SV_Target0
                #ifdef _WRITE_RENDERING_LAYERS
                , out uint outRenderingLayers : SV_Target1
                #endif
            )
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                #if defined(_GBUFFER_NORMALS_OCT)
                    float3 normalWS = normalize(input.normalWS);
                    float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
                    float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);
                    half3 packedNormalWS = PackFloat2To888(remappedOctNormalWS);
                    outNormalWS = half4(packedNormalWS, 0.0);
                #else
                    float3 normalWS = NormalizeNormalPerPixel(input.normalWS);
                    outNormalWS = half4(normalWS, 0.0);
                #endif

                #ifdef _WRITE_RENDERING_LAYERS
                    outRenderingLayers = EncodeMeshRenderingLayer();
                #endif
            }
            ENDHLSL
        }
    }

    Fallback Off
    CustomEditor "MS.Toon.Editor.MSToonShaderGUI"
}
