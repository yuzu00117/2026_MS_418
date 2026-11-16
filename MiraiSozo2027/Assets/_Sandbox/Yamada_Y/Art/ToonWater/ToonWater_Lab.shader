// トゥーン調の水面シェーダーの研究用(URP用)。きらめきと白波の見た目を試すために、ToonWater から分けたもの。
// 本番で使うのは ToonWater。ここでの改良は自動では ToonWater に入らない。
// 深さに応じた段階的な色、浅瀬の透け、物体の周りの泡、形のある波、浅瀬の光の模様、日光のきらめきを描く。
// 波は頂点を動かして作るので、細かく分割した板(WaterGridMesh)に貼ること。
// テクスチャは使わず、すべて計算で模様を作る。
// 必要な設定: URP Asset の Depth Texture と Opaque Texture をオンにすること。
Shader "MiraiSozo/Sandbox/ToonWater_Lab"
{
    Properties
    {
        [Header(Depth Color)]
        _ShallowColor ("浅い所の色", Color) = (0.45, 0.92, 0.95, 1)
        _DeepColor ("深い所の色", Color) = (0.04, 0.24, 0.62, 1)
        _DepthMaxDistance ("一番深い色になる深さ(m)", Float) = 4
        _DepthGradientPower ("色の変わり方(1 でまっすぐ、大きいほど浅い色が長く続く)", Range(0.2, 5)) = 1

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
        _WaveDirection ("波の進む向き(XとZ)", Vector) = (1, 0.3, 0, 0)
        _WaveLength ("一番大きな波の間隔(m)", Float) = 8
        _WaveSteepness ("波のとがり具合", Range(0, 1)) = 0.5
        _WaveSpread ("波の向きのばらつき", Range(0, 1)) = 0.5
        _WaveSpeed ("波の速さ", Float) = 0.5

        [Header(Wave Shading)]
        _WaveLitColor ("波の明るい面の色", Color) = (0.75, 0.97, 1, 1)
        _WaveLitAmount ("明るい面になる傾き", Range(0, 1)) = 0.12
        _WaveLitOpacity ("明るい面の濃さ", Range(0, 1)) = 0.5
        _WaveShadeAmount ("暗い面になる傾き", Range(0, 1)) = 0.12
        _WaveShadeStrength ("暗い面の暗さ", Range(0, 1)) = 0.2

        [Header(Crest Foam)]
        _CrestFoamThreshold ("白波が出る波のとがり", Range(0, 1)) = 0.55
        _CrestFoamOpacity ("白波の中心の濃さ", Range(0, 1)) = 0.95
        _CrestFoamEdgeOpacity ("白波の縁の濃さ", Range(0, 1)) = 0.45
        _CrestFoamCoreWidth ("白波の中心の広さ", Range(0, 0.5)) = 0.12
        _CrestFoamCoverage ("白波が立つ場所の多さ", Range(0, 1)) = 0.5
        _CrestFoamPatchScale ("白波が立つ場所のまだらの大きさ", Float) = 0.02
        _CrestFoamMinHeight ("白波が立つ高さ(波の高さに対する割合)", Range(0, 1)) = 0.2
        _CrestFoamBreakScale ("白波の縁の崩れの細かさ", Float) = 0.6
        _CrestFoamBreakStrength ("白波の縁の崩れの強さ", Range(0, 1)) = 0.4
        _CrestFoamLineLength ("白波の線の長さ", Range(1, 10)) = 4
        _CrestFoamFadeStart ("白波が薄くなり始める距離(m)", Float) = 40
        _CrestFoamFadeEnd ("白波が一番薄くなる距離(m)", Float) = 150
        _CrestFoamFarOpacity ("遠くの白波の濃さ", Range(0, 1)) = 0.25

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
                half4 _DeepColor;
                float _DepthMaxDistance;
                float _DepthGradientPower;

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

                float4 _WaveDirection;
                float _WaveLength;
                float _WaveSteepness;
                float _WaveSpread;
                float _WaveSpeed;

                half4 _WaveLitColor;
                float _WaveLitAmount;
                float _WaveLitOpacity;
                float _WaveShadeAmount;
                float _WaveShadeStrength;
                float _CrestFoamThreshold;
                float _CrestFoamOpacity;
                float _CrestFoamEdgeOpacity;
                float _CrestFoamCoreWidth;
                float _CrestFoamCoverage;
                float _CrestFoamPatchScale;
                float _CrestFoamMinHeight;
                float _CrestFoamBreakScale;
                float _CrestFoamBreakStrength;
                float _CrestFoamLineLength;
                float _CrestFoamFadeStart;
                float _CrestFoamFadeEnd;
                float _CrestFoamFarOpacity;

                float _ShadowStrength;
            CBUFFER_END

            static const float kEpsilon = 1e-4;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float fogFactor : TEXCOORD1;
                // 波で動かす前の水平位置。波の傾きと高さを画素ごとに計算し直して、陰の境目をくっきりさせるために使う
                float2 restXZ : TEXCOORD2;
                // メッシュの面の向き。水面(上向き)と水の箱の側面(横向き)を見分けるために使う
                float3 normalWS : TEXCOORD3;
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


            // ---- 波(ゲルストナー波: 頂点を上下だけでなく前後にも動かし、頂上がとがった波にする) ----
            static const int kWaveCount = 6;
            // 明暗の塗り分けに使う大きな波の本数。小さな波まで使うと、波の重なりでできた小さなこぶごとに
            // 明暗の点が出て、水玉模様のように見えるため
            static const int kLargeWaveCount = 3;
            static const float kGravity = 9.8;
            // 各波の 間隔の倍率 / 向きのずれ(ラジアン、ばらつき 1 のとき) / とがり具合の配分(合計 1)。
            // 間隔を半端な倍率にし、向きも左右に大きく散らして、波の列が平行に並んで規則的に見えるのを防ぐ
            static const float3 kWaveParams[kWaveCount] =
            {
                float3(1.00,  0.00, 0.30),
                float3(0.73,  0.90, 0.22),
                float3(0.53, -0.75, 0.17),
                float3(0.37,  1.60, 0.13),
                float3(0.27, -1.40, 0.10),
                float3(0.19,  2.30, 0.08),
            };

            struct WaveSample
            {
                float3 offset;  // 頂点を動かす量
                float3 normal;  // 波の面の向き
                float3 largeNormal; // 大きな波だけで求めた面の向き(明暗の塗り分け用)
                // 波の高さ(-1 一番低い 〜 1 全部の波の山が重なった高さ)
                float height;
                // 大きな波だけで求めた、水面が横に押し縮められている度合い(0 平ら 〜 1 大きな波の山が全部重なった所)。
                // 大きな波の頂上に沿って線状に大きくなるので、白波を出す場所に使う。
                // 小さな波まで含めると、小さなこぶごとに白波が出て点の集まりになるため
                float compression;
            };

            // 白波が「立たない高さ」から「立つ高さ」へ切り替わる幅。急に切り替わると白波の端が直線的になる
            static const float kCrestHeightSoftness = 0.2;
            // まだら模様で白波を消す境目のぼかし幅
            static const float kCrestPatchSoftness = 0.15;

            WaveSample SampleWaves(float2 xz)
            {
                float2 baseDir = normalize(_WaveDirection.xy + kEpsilon);
                float baseAngle = atan2(baseDir.y, baseDir.x);
                float3 offset = 0;
                float3 tangent = float3(1, 0, 0);
                float3 binormal = float3(0, 0, 1);
                float3 largeTangent = tangent;
                float3 largeBinormal = binormal;
                float amplitudeSum = 0;
                float largeSteepnessSum = 0;

                [unroll]
                for (int i = 0; i < kWaveCount; i++)
                {
                    float3 param = kWaveParams[i];
                    float angle = baseAngle + param.y * _WaveSpread;
                    float2 dir = float2(cos(angle), sin(angle));
                    float k = TWO_PI / max(_WaveLength * param.x, kEpsilon);
                    // 本物の波と同じく、長い波ほど速く進む
                    float speed = sqrt(kGravity / k) * _WaveSpeed;
                    // とがり具合の合計を 1 以下にして、頂上で面が折り返す(ねじれる)のを防ぐ
                    float steepness = _WaveSteepness * param.z;
                    float amplitude = steepness / k;
                    float f = k * (dot(dir, xz) - speed * _Time.y);
                    float s = sin(f);
                    float c = cos(f);

                    offset += float3(dir.x * amplitude * c, amplitude * s, dir.y * amplitude * c);
                    float3 tangentStep = float3(-dir.x * dir.x * steepness * s, dir.x * steepness * c, -dir.x * dir.y * steepness * s);
                    float3 binormalStep = float3(-dir.x * dir.y * steepness * s, dir.y * steepness * c, -dir.y * dir.y * steepness * s);
                    tangent += tangentStep;
                    binormal += binormalStep;
                    if (i < kLargeWaveCount)
                    {
                        largeTangent += tangentStep;
                        largeBinormal += binormalStep;
                        largeSteepnessSum += steepness;
                    }
                    amplitudeSum += amplitude;
                }

                WaveSample result;
                result.offset = offset;
                result.normal = normalize(cross(binormal, tangent));
                result.largeNormal = normalize(cross(largeBinormal, largeTangent));
                result.height = offset.y / max(amplitudeSum, kEpsilon);
                // 水平方向の動きの伸び縮み(ヤコビアン)。1 より小さい所は押し縮められている
                float largeJacobian = largeTangent.x * largeBinormal.z - largeTangent.z * largeBinormal.x;
                result.compression = saturate((1.0 - largeJacobian) / max(largeSteepnessSum, kEpsilon));
                return result;
            }

            // 面の上向き具合がこの範囲で「側面」から「水面」へ切り替わる
            static const float kTopFaceMin = 0.5;
            static const float kTopFaceMax = 0.8;

            // 模様を貼るための 2D 座標。水面は真上から、側面は横から見た位置を使う。
            // 側面で真上から見た位置を使うと、上から下まで同じ値になり模様が縦に伸びるため
            float2 ProjectPatternUV(float3 positionWS, float3 normalWS, float isTop)
            {
                float2 sideUV = abs(normalWS.x) > abs(normalWS.z) ? positionWS.zy : positionWS.xy;
                return isTop > 0.5 ? positionWS.xz : sideUV;
            }

            // 光のゆらぎを 3 方向から貼るとき、向きの近い方をどれだけ優先するか。大きいほど境目の混ざる幅が狭い
            static const float kTriplanarSharpness = 4.0;

            // 光のゆらぎの線(1 線の上 〜 0 線の外)
            float CausticsLine(float2 uv, float2 distortion, float time)
            {
                float caustics = VoronoiEdge(uv * _CausticsScale + distortion * 0.3, time * _CausticsSpeed);
                return 1.0 - smoothstep(_CausticsWidth, _CausticsWidth + 0.02, caustics);
            }

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.restXZ = positionWS.xz;
                positionWS += SampleWaves(positionWS.xz).offset;

                output.positionWS = positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float time = _Time.y;
                float3 faceNormal = normalize(input.normalWS);
                // 1 で水面、0 で水の箱の側面。白波・きらめき・波の明暗は水面にしか出ないので、側面では消す
                float topFace = smoothstep(kTopFaceMin, kTopFaceMax, faceNormal.y);
                float2 worldUV = ProjectPatternUV(input.positionWS, faceNormal, topFace);
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                WaveSample wave = SampleWaves(input.restXZ);

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

                // ---- 深さでなめらかに変わる色(浅い色 → 深い色) ----
                // 2 色の間をまっすぐ変えるだけだと浅瀬の明るい色がすぐ消えて見えることがあるので、
                // 変わり方を曲げられるようにする
                float depth01 = saturate(waterDepth / max(_DepthMaxDistance, kEpsilon));
                float gradientT = pow(depth01, max(_DepthGradientPower, kEpsilon));
                half3 color = lerp(_ShallowColor.rgb, _DeepColor.rgb, gradientT);

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
                // 水の下にある物の面の向きを、隣の画素との位置の差から求める。
                // 地面には真上から、壁には横から模様を貼り、間は混ぜてつなぐ。
                // 真上からだけ貼ると、壁では上から下まで同じ模様になり縦に伸びるため
                float3 sceneCross = cross(ddy(sceneWS), ddx(sceneWS));
                float3 sceneNormal = sceneCross / max(length(sceneCross), kEpsilon);
                float3 triWeight = pow(abs(sceneNormal), kTriplanarSharpness) + float3(0.0, kEpsilon, 0.0);
                triWeight /= triWeight.x + triWeight.y + triWeight.z;
                float causticsLine =
                    CausticsLine(sceneWS.zy, distortion, time) * triWeight.x +
                    CausticsLine(sceneWS.xz, distortion, time) * triWeight.y +
                    CausticsLine(sceneWS.xy, distortion, time) * triWeight.z;
                causticsLine *= 1.0 - saturate(waterDepth / max(_CausticsDistance, kEpsilon));
                color = lerp(color, _CausticsColor.rgb, causticsLine * _CausticsOpacity);

                // ---- 遠くの水面ほど明るい色に寄せる ----
                float3 viewDirWS = normalize(GetWorldSpaceViewDir(input.positionWS));
                // 側面では波の向きではなく面そのものの向きを使う。波の向き(ほぼ上向き)のままだと、
                // 横から見た側面が全部遠くの水面の色になってしまうため
                float3 surfaceNormal = normalize(lerp(faceNormal, wave.normal, topFace));
                float fresnel = pow(1.0 - saturate(dot(viewDirWS, surfaceNormal)), _FresnelPower) * _FresnelStrength;
                color = lerp(color, _HorizonColor.rgb, fresnel);

                // ---- 影を受ける ----
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                float shadow = lerp(1.0 - _ShadowStrength, 1.0, mainLight.shadowAttenuation);
                color *= shadow;

                // ---- 波の面の塗り分け(トゥーン調) ----
                // 平らな水面と比べて、光の方を向いている面は明るく、背を向けている面は暗く、段で塗り分ける。
                // 大きな波の面だけで塗り分け、波の斜面に沿った帯にする
                float slopeLight = dot(wave.largeNormal, mainLight.direction) - saturate(mainLight.direction.y);
                float slopeAA = max(fwidth(slopeLight), kEpsilon);
                float litBand = smoothstep(_WaveLitAmount - slopeAA, _WaveLitAmount + slopeAA, slopeLight);
                float shadeBand = smoothstep(_WaveShadeAmount - slopeAA, _WaveShadeAmount + slopeAA, -slopeLight);
                color = lerp(color, _WaveLitColor.rgb, litBand * _WaveLitOpacity * mainLight.shadowAttenuation * topFace);
                color *= 1.0 - shadeBand * _WaveShadeStrength * topFace;

                // ---- 波の頂上の白波 ----
                // 本物の白波は、波が高く盛り上がって、しかも頂上がとがった所にだけ立つ。
                // そこで「高さ」と「とがり具合」の両方がそろった所だけを白波の候補にする
                float heightMask = smoothstep(_CrestFoamMinHeight, _CrestFoamMinHeight + kCrestHeightSoftness, wave.height);
                float crestValue = wave.compression * heightMask;
                // 本物の海でも白波は全部の波には立たないので、大きなまだら模様で白波を消す場所を作る。
                // 消す方向にだけ使い、低い所に白波を足してしまわないようにする
                float patchNoise = GradientNoise(worldUV * _CrestFoamPatchScale + time * 0.01);
                crestValue -= smoothstep(0.0, kCrestPatchSoftness, (1.0 - _CrestFoamCoverage) - patchNoise);
                // 大小 2 つのなめらかな模様で白波を削る。模様を波の頂上に沿った向きに引き伸ばし、
                // 削られ方を粒ではなく「途切れ途切れの線」にする
                float2 waveDir = normalize(_WaveDirection.xy + kEpsilon);
                float2 crestUV = float2(dot(worldUV, float2(-waveDir.y, waveDir.x)) / max(_CrestFoamLineLength, 1.0), dot(worldUV, waveDir));
                float breakNoise =
                    GradientNoise(crestUV * _CrestFoamBreakScale + time * 0.12) * 0.65 +
                    GradientNoise(crestUV * _CrestFoamBreakScale * 2.3 - time * 0.2) * 0.35;
                crestValue += (0.5 - breakNoise) * _CrestFoamBreakStrength;
                float crestAA = max(fwidth(crestValue), kEpsilon);
                float crestBody = smoothstep(_CrestFoamThreshold - crestAA, _CrestFoamThreshold + crestAA, crestValue);
                float coreEdge = _CrestFoamThreshold + _CrestFoamCoreWidth;
                float crestCore = smoothstep(coreEdge - crestAA, coreEdge + crestAA, crestValue);
                // 中心は濃く、縁は半透明にして、泡の厚みの違いを出す
                float crestFoam = crestBody * lerp(_CrestFoamEdgeOpacity, _CrestFoamOpacity, crestCore);
                // 遠くでは白波の列が詰まって白い縞に見えるので薄くする
                float cameraDistance = length(viewVec);
                float crestFade = smoothstep(_CrestFoamFadeStart, max(_CrestFoamFadeEnd, _CrestFoamFadeStart + kEpsilon), cameraDistance);
                crestFoam *= lerp(1.0, _CrestFoamFarOpacity, crestFade) * topFace;
                color = lerp(color, _FoamColor.rgb, crestFoam);

                // ---- 物体の周りの泡 ----
                float2 foamUV = worldUV * _FoamNoiseScale + float2(1.0, 0.7) * time * _FoamSpeed;
                float foamNoise = GradientNoise(foamUV);
                float foamT = viewDepth / max(_FoamDistance, kEpsilon);
                // 物体から離れるほど泡が出にくくなり、まだら模様になる
                float foamPatch = smoothstep(foamT - 0.02, foamT + 0.02, foamNoise) * step(foamT, 1.0);
                // 物体にぴったり沿った縁取り(少し波打たせる)
                float foamLine = 1.0 - step(_FoamLineWidth, viewDepth + (foamNoise - 0.5) * _FoamLineWidth);
                float foam = saturate(max(foamPatch, foamLine)) * _FoamColor.a;
                float allFoam = max(foam, crestFoam);
                color = lerp(color, _FoamColor.rgb, foam);

                // ---- 日光のきらめき ----
                float sparkleTime = time * _SparkleSpeed;
                float bumpX = GradientNoise(worldUV * _SparkleScale + sparkleTime) - 0.5;
                float bumpZ = GradientNoise(worldUV * _SparkleScale * 1.37 - sparkleTime * 0.8 + 3.1) - 0.5;
                // 波の面の向きに細かい凹凸を足して、波の傾きに合わせて光るようにする
                float3 sparkleNormal = normalize(wave.normal + float3(bumpX, 0.0, bumpZ) * _SparkleBumpiness);
                float3 halfDir = normalize(mainLight.direction + viewDirWS);
                float sparkle = step(_SparkleThreshold, saturate(dot(sparkleNormal, halfDir)));
                sparkle *= mainLight.shadowAttenuation * (1.0 - allFoam) * topFace;
                color += sparkle * _SparkleColor.rgb;

                color = MixFog(color, input.fogFactor);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
