// トゥーン調の水面シェーダー(URP用・試作)。
// 深さに応じた段階的な色、浅瀬の透け、物体の周りの泡、流れる白い筋、浅瀬の光の模様、日光のきらめきを描く。
// テクスチャは使わず、すべて計算で模様を作る。
// 必要な設定: URP Asset の Depth Texture と Opaque Texture をオンにすること。
Shader "MiraiSozo/Sandbox/ToonWater"
{
    Properties
    {
        [Header(Depth Color)]
        _ShallowColor ("浅い所の色", Color) = (0.45, 0.92, 0.95, 1)
        _MidColor ("中間の色", Color) = (0.12, 0.62, 0.92, 1)
        _DeepColor ("深い所の色", Color) = (0.04, 0.24, 0.62, 1)
        _DepthMaxDistance ("一番深い色になる深さ(m)", Float) = 4
        _ColorSteps ("色の段数", Range(1, 8)) = 4
        _StepSoftness ("段の境目のぼかし", Range(0, 1)) = 0.15
        _BandNoiseScale ("段の境目の揺らぎの細かさ", Float) = 0.4
        _BandNoiseStrength ("段の境目の揺らぎの強さ", Range(0, 0.5)) = 0.12

        [Header(Transparency)]
        _ShallowClarity ("浅瀬の透明度", Range(0, 1)) = 0.7
        _ClarityDistance ("透けて見える深さ(m)", Float) = 1.5
        _RefractionStrength ("水中のゆがみ", Range(0, 0.1)) = 0.015

        [Header(Horizon)]
        _HorizonColor ("遠くの水面の色", Color) = (0.55, 0.85, 1, 1)
        _FresnelPower ("遠くの色の効き始め", Range(1, 10)) = 5
        _FresnelStrength ("遠くの色の強さ", Range(0, 1)) = 0.5

        [Header(Edge Foam)]
        _FoamColor ("泡の色", Color) = (1, 1, 1, 1)
        _FoamDistance ("泡が出る範囲", Float) = 0.6
        _FoamLineWidth ("泡の縁取りの太さ", Float) = 0.08
        _FoamNoiseScale ("泡の模様の細かさ", Float) = 4
        _FoamSpeed ("泡の動く速さ", Float) = 0.3

        [Header(Streaks)]
        _StreakColor ("白い筋の色", Color) = (1, 1, 1, 1)
        _StreakScale ("白い筋の細かさ", Float) = 0.12
        _StreakStretch ("白い筋の伸び", Range(1, 20)) = 10
        _StreakDirection ("白い筋の向き(XとZ)", Vector) = (1, 0.3, 0, 0)
        _StreakSpeed ("白い筋の流れる速さ", Float) = 0.1
        _StreakWidth ("白い筋の太さ", Range(0, 0.1)) = 0.012
        _StreakDensity ("白い筋の量", Range(0, 1)) = 0.3
        _StreakOpacity ("白い筋の濃さ", Range(0, 1)) = 0.6
        _StreakFadeStart ("白い筋が薄くなり始める距離(m)", Float) = 15
        _StreakFadeEnd ("白い筋が消える距離(m)", Float) = 50

        [Header(Caustics)]
        _CausticsColor ("浅瀬の光の模様の色", Color) = (0.85, 1, 1, 1)
        _CausticsScale ("光の模様の細かさ", Float) = 1.2
        _CausticsSpeed ("光の模様の動く速さ", Float) = 0.6
        _CausticsWidth ("光の模様の線の太さ", Range(0, 0.5)) = 0.08
        _CausticsDistance ("光の模様が見える深さ(m)", Float) = 2
        _CausticsOpacity ("光の模様の濃さ", Range(0, 1)) = 0.4

        [Header(Sparkle)]
        _SparkleColor ("きらめきの色", Color) = (1, 1, 1, 1)
        _SparkleScale ("きらめきの細かさ", Float) = 3
        _SparkleThreshold ("きらめきの少なさ", Range(0.9, 1)) = 0.985
        _SparkleSpeed ("きらめきの変わる速さ", Float) = 0.5
        _SparkleBumpiness ("きらめきの散らばり", Range(0, 2)) = 0.8

        [Header(Waves)]
        _WaveHeight ("波の高さ(m)", Float) = 0.05
        _WaveLength ("波の間隔(m)", Float) = 6
        _WaveSpeed ("波の速さ", Float) = 0.6

        [Header(Lighting)]
        _ShadowStrength ("影の濃さ", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        Pass
        {
            Name "ToonWater"
            Tags { "LightMode" = "UniversalForward" }

            // 水の下の景色は Opaque Texture から自分で合成するので、出力は不透明として上書きする
            Blend One Zero
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            // 半透明の水は画面空間の影を使えないため、_MAIN_LIGHT_SHADOWS_SCREEN は含めない
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ShallowColor;
                half4 _MidColor;
                half4 _DeepColor;
                float _DepthMaxDistance;
                float _ColorSteps;
                float _StepSoftness;
                float _BandNoiseScale;
                float _BandNoiseStrength;

                float _ShallowClarity;
                float _ClarityDistance;
                float _RefractionStrength;

                half4 _HorizonColor;
                float _FresnelPower;
                float _FresnelStrength;

                half4 _FoamColor;
                float _FoamDistance;
                float _FoamLineWidth;
                float _FoamNoiseScale;
                float _FoamSpeed;

                half4 _StreakColor;
                float _StreakScale;
                float _StreakStretch;
                float4 _StreakDirection;
                float _StreakSpeed;
                float _StreakWidth;
                float _StreakDensity;
                float _StreakOpacity;
                float _StreakFadeStart;
                float _StreakFadeEnd;

                half4 _CausticsColor;
                float _CausticsScale;
                float _CausticsSpeed;
                float _CausticsWidth;
                float _CausticsDistance;
                float _CausticsOpacity;

                half4 _SparkleColor;
                float _SparkleScale;
                float _SparkleThreshold;
                float _SparkleSpeed;
                float _SparkleBumpiness;

                float _WaveHeight;
                float _WaveLength;
                float _WaveSpeed;

                float _ShadowStrength;
            CBUFFER_END

            static const float kEpsilon = 1e-4;

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float fogFactor : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // ---- 模様づくり用の関数 ----

            float2 Hash22(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }

            // なめらかなランダム模様。戻り値はおおよそ 0〜1
            float GradientNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float2 u = f * f * (3.0 - 2.0 * f);

                float n00 = dot(Hash22(i) * 2.0 - 1.0, f);
                float n10 = dot(Hash22(i + float2(1, 0)) * 2.0 - 1.0, f - float2(1, 0));
                float n01 = dot(Hash22(i + float2(0, 1)) * 2.0 - 1.0, f - float2(0, 1));
                float n11 = dot(Hash22(i + float2(1, 1)) * 2.0 - 1.0, f - float2(1, 1));

                return lerp(lerp(n00, n10, u.x), lerp(n01, n11, u.x), u.y) + 0.5;
            }

            // 網目模様(セルの境目ほど 0 に近い)。浅瀬の光の模様に使う
            float VoronoiEdge(float2 p, float time)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                float nearest = 8.0;
                float second = 8.0;

                [unroll]
                for (int y = -1; y <= 1; y++)
                {
                    [unroll]
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 cell = float2(x, y);
                        float2 offset = 0.5 + 0.5 * sin(time + TWO_PI * Hash22(i + cell));
                        float d = length(cell + offset - f);
                        if (d < nearest)
                        {
                            second = nearest;
                            nearest = d;
                        }
                        else if (d < second)
                        {
                            second = d;
                        }
                    }
                }
                return second - nearest;
            }

            // 深さ 0〜1 を 浅い→中間→深い の3色に割り当てる
            half3 DepthGradient(float t)
            {
                half3 shallowToMid = lerp(_ShallowColor.rgb, _MidColor.rgb, saturate(t * 2.0));
                half3 midToDeep = lerp(_MidColor.rgb, _DeepColor.rgb, saturate(t * 2.0 - 1.0));
                return t < 0.5 ? shallowToMid : midToDeep;
            }

            // 水面をゆるく上下させる高さ
            float WaveOffset(float2 xz)
            {
                float k = TWO_PI / max(_WaveLength, kEpsilon);
                float t = _Time.y * _WaveSpeed;
                float wave1 = sin(dot(xz, float2(1.0, 0.6)) * k + t);
                float wave2 = sin(dot(xz, float2(-0.4, 1.0)) * k * 1.3 + t * 1.2);
                return (wave1 + wave2) * 0.5 * _WaveHeight;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                positionWS.y += WaveOffset(positionWS.xz);

                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float time = _Time.y;
                float2 worldUV = input.positionWS.xz;
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);

                // ---- 水の深さを求める ----
                // カメラから見た水面までの距離と、水面の向こうにある物体までの距離
                float surfaceEye = LinearEyeDepth(input.positionWS, GetWorldToViewMatrix());
                float sceneEye = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);

                // 視線方向の差(泡に使う)
                float viewDepth = max(sceneEye - surfaceEye, 0.0);

                // 水底の位置を復元し、真下方向の深さを出す(色・透け・光の模様に使う)
                float3 viewVec = input.positionWS - _WorldSpaceCameraPos;
                float3 sceneWS = _WorldSpaceCameraPos + viewVec * (sceneEye / max(surfaceEye, kEpsilon));
                float waterDepth = max(input.positionWS.y - sceneWS.y, 0.0);

                // ---- 深さで段階的に変わる色 ----
                float bandNoise = GradientNoise(worldUV * _BandNoiseScale + time * 0.05) - 0.5;
                float depth01 = saturate(waterDepth / max(_DepthMaxDistance, kEpsilon) + bandNoise * _BandNoiseStrength);
                float steps = max(_ColorSteps, 1.0);
                float scaled = depth01 * steps;
                float stepEdge = smoothstep(1.0 - max(_StepSoftness, kEpsilon), 1.0, frac(scaled));
                float banded = saturate((floor(scaled) + stepEdge) / steps);
                half3 color = DepthGradient(banded);

                // ---- 浅瀬は水の下が透けて見える ----
                float2 distortion = float2(
                    GradientNoise(worldUV * 1.5 + time * 0.2),
                    GradientNoise(worldUV * 1.5 + 17.3 - time * 0.2)) - 0.5;
                float2 refractUV = screenUV + distortion * _RefractionStrength;
                // ゆがませた先が水面より手前の物体なら、ゆがませない(手前の物が水に映り込むのを防ぐ)
                float refractEye = LinearEyeDepth(SampleSceneDepth(refractUV), _ZBufferParams);
                refractUV = refractEye < surfaceEye ? screenUV : refractUV;
                half3 underwater = SampleSceneColor(refractUV) * _ShallowColor.rgb;
                float clarity = _ShallowClarity * (1.0 - saturate(waterDepth / max(_ClarityDistance, kEpsilon)));
                color = lerp(color, underwater, clarity);

                // ---- 浅瀬の光の模様(水底に貼りつくように描く) ----
                float caustics = VoronoiEdge(sceneWS.xz * _CausticsScale + distortion * 0.3, time * _CausticsSpeed);
                float causticsLine = 1.0 - smoothstep(_CausticsWidth, _CausticsWidth + 0.02, caustics);
                causticsLine *= 1.0 - saturate(waterDepth / max(_CausticsDistance, kEpsilon));
                color = lerp(color, _CausticsColor.rgb, causticsLine * _CausticsOpacity);

                // ---- 遠くの水面ほど明るい色に寄せる ----
                float3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                float fresnel = pow(1.0 - saturate(viewDirWS.y), _FresnelPower) * _FresnelStrength;
                color = lerp(color, _HorizonColor.rgb, fresnel);

                // ---- 影を受ける ----
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float shadow = lerp(1.0 - _ShadowStrength, 1.0, mainLight.shadowAttenuation);
                color *= shadow;

                // ---- 流れる白い筋 ----
                float2 streakDir = normalize(_StreakDirection.xy + kEpsilon);
                float2 streakSide = float2(-streakDir.y, streakDir.x);
                float2 streakUV = float2(dot(worldUV, streakDir) / _StreakStretch, dot(worldUV, streakSide)) * _StreakScale;
                streakUV.x -= time * _StreakSpeed;
                float streakNoise = GradientNoise(streakUV);
                // 筋を出す場所。筋と同じ向きに引き伸ばした模様を使い、長い区間ごとに出し入れする。
                // 0/1 で切らずになめらかに変化させ、その値で線の太さを決めることで、端が筆の払いのように細く消える
                float2 maskUV = streakUV * float2(0.5, 0.7) + float2(5.7, time * 0.02);
                float streakMask = smoothstep(1.0 - _StreakDensity, 1.0 - _StreakDensity + 0.15, GradientNoise(maskUV));
                float streakHalfWidth = _StreakWidth * streakMask;
                float streakAA = fwidth(streakNoise);
                float streakLine = 1.0 - smoothstep(streakHalfWidth, streakHalfWidth + streakAA, abs(streakNoise - 0.5));
                // 線が1画素より細くなる所は、太さの代わりに濃さを落として、ぷつっと切れて見えないようにする
                streakLine *= saturate(streakHalfWidth / max(streakAA, kEpsilon));
                // 遠くでは筋が細かく詰まって見えるので、距離に応じて薄くする
                float cameraDistance = length(viewVec);
                float streakFade = 1.0 - smoothstep(_StreakFadeStart, max(_StreakFadeEnd, _StreakFadeStart + kEpsilon), cameraDistance);
                color = lerp(color, _StreakColor.rgb, streakLine * streakFade * _StreakOpacity);

                // ---- 物体の周りの泡 ----
                float2 foamUV = worldUV * _FoamNoiseScale + float2(1.0, 0.7) * time * _FoamSpeed;
                float foamNoise = GradientNoise(foamUV);
                float foamT = viewDepth / max(_FoamDistance, kEpsilon);
                // 物体から離れるほど泡が出にくくなり、まだら模様になる
                float foamPatch = smoothstep(foamT - 0.02, foamT + 0.02, foamNoise) * step(foamT, 1.0);
                // 物体にぴったり沿った縁取り(少し波打たせる)
                float foamLine = 1.0 - step(_FoamLineWidth, viewDepth + (foamNoise - 0.5) * _FoamLineWidth);
                float foam = saturate(max(foamPatch, foamLine)) * _FoamColor.a;
                color = lerp(color, _FoamColor.rgb, foam);

                // ---- 日光のきらめき ----
                float sparkleTime = time * _SparkleSpeed;
                float bumpX = GradientNoise(worldUV * _SparkleScale + sparkleTime) - 0.5;
                float bumpZ = GradientNoise(worldUV * _SparkleScale * 1.37 - sparkleTime * 0.8 + 3.1) - 0.5;
                float3 sparkleNormal = normalize(float3(bumpX * _SparkleBumpiness, 1.0, bumpZ * _SparkleBumpiness));
                float3 halfDir = normalize(mainLight.direction + viewDirWS);
                float sparkle = step(_SparkleThreshold, saturate(dot(sparkleNormal, halfDir)));
                sparkle *= mainLight.shadowAttenuation * (1.0 - foam);
                color += sparkle * _SparkleColor.rgb;

                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
