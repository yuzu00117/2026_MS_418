// 円すいの輪の波の見た目（ConeRingView が使う）。細かい泡を輪の管の表面に集めて回し、管の中心にはもやを置いて、ひとまとまりの輪に見せる
// ・頂点の入れ物は使わない。Graphics.RenderPrimitives で「6 頂点 ×（輪 1 つの泡の数 ＋ もやの数）」を「輪の数」だけ描き、
//   SV_InstanceID を輪の番号、SV_VertexID / 6 を輪の中の番号（前の HAZE_PER_RING 個がもや、その後ろが泡）、SV_VertexID % 6 を四角の角にする
// ・泡の位置は「輪が出てからの時間」と「泡ごとの決まった乱数」だけの式で決める（前のフレームを覚えない。フレームが飛んでも崩れない）
// ・輪ごとの値（撃った位置・向き・出てからの時間・速さ・届く距離・半角・色・乱数の種）は、ConeRingView が _Rings に詰めて毎フレーム渡す
// ・重ね方は 2 つ（_BlendStyle と、ConeRingView がマテリアルに入れる _SrcBlend・_DstBlend）。奥行きは書かず、壁の向こうでは隠れる
//   色を重ねる（1）: 事前乗算の半透明。後ろを「泡の濃さ」だけ隠して色を乗せるので、明るい背景の上でも見える（輪はカメラから遠い順に、輪の中はもや → 泡の順に描く。泡どうしは並べない）
//   光を足す（0）: 加算。暗い背景では泡が重なる縁が光るが、明るい背景の上ではほとんど見えない
// ・Resources フォルダーに置いて、名前（Aura/Wave/ConeRing）で探せるようにしている（ビルドにも必ず入る）
Shader "Aura/Wave/ConeRing"
{
    Properties
    {
        _BubbleCount ("輪 1 つの泡の数", Float) = 1500
        _BubbleSize ("泡の直径（m）", Float) = 0.025
        _TubeRatio ("管の太さ（輪の半径に対する割合）", Float) = 0.15
        _ShellFocus ("泡を管の表面へ寄せる", Range(0.0, 1.0)) = 0.8
        _SwirlSpeed ("管のまわりを回る速さ（周/秒）", Float) = 1.5
        _BackDim ("奥の泡の暗さ", Range(0.0, 0.9)) = 0.4
        _Brightness ("泡 1 つの明るさ", Float) = 2.0
        _UnravelStart ("ほどけ始める位置（割合）", Range(0.0, 1.0)) = 0.82
        _FloatSeconds ("浮かんで消える時間（秒）", Float) = 0.6
        _StartRadius ("出たときの輪の半径（m）", Float) = 0.1
        _MinPixels ("泡の画面上の直径の下限（画素）", Float) = 1.5
        _MaxPixels ("泡の画面上の直径の上限（画素）", Float) = 24.0
        _Coverage ("泡の濃さ（色を重ねるとき）", Range(0.0, 1.0)) = 0.7
        _HazeAmount ("もや", Range(0.0, 1.0)) = 1.0
        _BlendStyle ("重ね方（0 = 光を足す、1 = 色を重ねる）", Float) = 1.0
        [HideInInspector] _SrcBlend ("", Float) = 1.0
        [HideInInspector] _DstBlend ("", Float) = 10.0
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

        Pass
        {
            Name "WaveConeRing"
            Tags { "LightMode" = "UniversalForward" }

            // 重ね方は ConeRingView が決める（色を重ねる: One OneMinusSrcAlpha、光を足す: One One）。奥行きは書かない
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define MAX_RINGS 64      // 同時に描ける輪の数の上限（ConeRingView の MaxRings と同じ値）
            #define RING_VECTORS 5    // 輪 1 つの値の Vector4 の数（ConeRingView の VectorsPerRing と同じ値）

            CBUFFER_START(UnityPerMaterial)
                float _BubbleCount;
                float _BubbleSize;
                float _TubeRatio;
                float _ShellFocus;
                float _SwirlSpeed;
                float _BackDim;
                float _Brightness;
                float _UnravelStart;
                float _FloatSeconds;
                float _StartRadius;
                float _MinPixels;
                float _MaxPixels;
                float _Coverage;
                float _HazeAmount;
                float _BlendStyle;
            CBUFFER_END

            // 輪ごとの値（ConeRingView.PackRings が詰める）
            // [0] xyz = 撃った位置、w = 輪が出てからの時間（秒）
            // [1] xyz = 進む向き、w = 速さ（m/s）
            // [2] xyz = 輪の面の右、w = 届く距離（m）
            // [3] xyz = 輪の面の上、w = tan(半角)
            // [4] xyz = 色（線形）、w = 乱数の種
            float4 _Rings[MAX_RINGS * RING_VECTORS];

            // 輪の形と一生
            #define BIRTH_SECONDS 0.08        // 生まれてから、小さな塊が輪に広がりきるまで（秒）
            #define BIRTH_RADIUS 0.55         // 生まれた瞬間の輪の半径（広がりきった大きさに対する割合）
            #define BIRTH_TUBE 0.25           // 生まれた瞬間の管の太さ（広がりきった太さに対する割合）
            #define TUBE_THINNING 0.7         // 届く距離での管の太さ（出たときの太さに対する割合。進むほど細る）
            #define END_DIM 0.8               // 届く距離での明るさ（出たときに対する割合）
            #define WAVE_AMPLITUDE 0.07       // 輪の波打ちの大きさ（輪の半径に対する割合。ConeRingView の WaveMargin より小さくする）
            #define WAVE_START 0.45           // 届く距離のこの割合から、波打ちが育ち始める
            #define AXIAL_WAVE 0.6            // 波打ちの、進む向きの揺れ（半径の向きの揺れに対する割合）
            #define TOROIDAL_DRIFT 0.1        // 泡が輪のまわりをゆっくり回る速さ（ラジアン/秒。向きは輪ごとに決める）
            #define SWIRL_SPREAD_MIN 0.85     // 管のまわりを回る速さの、泡ごとのばらつき（下限の倍率）
            #define SWIRL_SPREAD_MAX 1.15     // 同じく上限の倍率
            #define SHELL_SPREAD 0.12         // 管の表面からのずれ（管の太さに対する割合。正規分布の標準偏差）
            #define JITTER_SCALE 0.8          // 並びの数列に足す乱数の大きさ（泡どうしの間隔に対する割合）。規則正しい模様が出ないようにする
            #define MIN_COUNT_FRACTION 0.15   // 輪が小さい間に使う泡の割合の下限
            #define COUNT_FADE_BAND 0.04      // 使う泡が増えるとき、新しい泡がなじむ幅（泡の数に対する割合）
            // 輪を離れる泡
            #define SHED_RATIO 0.04           // 途中でこぼれる泡の割合
            #define SHED_START 0.15           // こぼれ始める位置（届く距離に対する割合）
            #define FLOAT_ACCEL 1.6           // 離れた泡が浮かぶ速さの増え方（m/s^2）
            // 離れた泡の動き（浮き・前への流れ・揺れ）は足し合わさる。FLOAT_RISE_MAX ＋ FORWARD_DRIFT_MAX ＋ √2 × WOBBLE が、
            // ConeRingView の DetachMargin（描く範囲の余白）より小さくなるようにする
            #define FLOAT_RISE_MAX 0.35       // 離れた泡が浮かぶ高さの上限（m）
            #define FORWARD_DRIFT_PER_SPEED 0.04  // 離れた泡が前へ流れる距離（輪の速さ m/s に対する割合、m）
            #define FORWARD_DRIFT_MAX 0.4     // 離れた泡が前へ流れる距離の上限（m）
            #define WOBBLE 0.04               // 離れた泡の横揺れの大きさ（m。輪の面の右と上の 2 つの向きに揺れる）
            #define DETACH_GROWTH 0.3         // 離れた泡が消えるまでにふくらむ割合
            #define FLOAT_FADE_START 0.35     // 浮かんで消える時間のこの割合から、薄くなり始める
            #define POP_RATIO 0.15            // 消える前に一瞬明るくはじける泡の割合（離れた泡のうち）
            #define POP_GAIN 2.0              // はじけるときに明るくなる倍率（足す分）
            // 光り方
            #define BRIGHTNESS_SPREAD 0.3     // 泡ごとの明るさのばらつき（±の割合）
            #define SPARKLE_RATIO 0.04        // ひときわ明るい泡（きらめき）の割合
            #define SPARKLE_GAIN 3.5          // きらめきの泡の明るさの倍率
            #define SPARKLE_WHITEN 0.6        // きらめきの泡の色を、白へ寄せる割合
            #define SIZE_MIN 0.5              // 泡の直径のばらつき（下限の倍率）
            #define SIZE_MAX 2.0              // 同じく上限の倍率（小さい泡が多く、大きい泡は少し）
            #define NEAR_FADE_START 0.25      // カメラからこの距離（m）より近い泡は描かない
            #define NEAR_FADE_LENGTH 0.5      // そこからこの長さ（m）をかけて、元の明るさに戻す
            #define BUBBLE_LOOK_PIXELS 6.0    // 画面でこの直径（画素）より大きく写る泡から、泡の絵にし始める
            #define BUBBLE_LOOK_BAND 4.0      // そこからこの幅（画素）で、泡の絵に変わりきる
            // 色を重ねるとき
            #define BLEND_GLOW 0.15           // 色を重ねるときに、隠した分より少し明るく光らせる割合（「明るさ」1 あたり）
            #define BLEND_SPARKLE 0.3         // きらめき・はじける泡の、明るくなった分（倍率 − 1）を光として足す割合
            // もや（管の中心に置く、大きくぼやけた粒。泡をひとまとまりの輪に見せる）
            #define HAZE_PER_RING 40          // 輪 1 つのもやの数（ConeRingView の HazePerRing と同じ値）
            #define HAZE_SIZE 3.0             // もやの直径（管の半径に対する倍率。既定の管の太さなら、隣のもやと重なってすき間ができない大きさ。細い管の下限は HAZE_MIN_SPACING。半分を ConeRingView の TubeMarginScale 以下にする）
            #define HAZE_ALPHA 0.7            // 色を重ねるときの、もやの濃さ（「もや」1 のとき）
            #define HAZE_MIN_SPACING 2.0      // もやの直径の下限（隣のもやとの間隔に対する倍率）。細い管でも、もやの間にすき間ができないようにする（ConeRingView の HazeSpacingMargin と合わせる）
            #define HAZE_DEEP 0.5             //色を重ねるときの、もやの色の明るさ（波の色に対する割合）。泡より深い色にして、明るい背景の上でも輪の形が見えるようにする
            #define HAZE_GLOW 0.12            // 光を足すときの、もやの明るさ（「明るさ」1・「もや」1 のとき）
            #define HAZE_MAX_PIXELS 640.0     // もやの画面上の直径の上限（画素）

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 corner : TEXCOORD0;     // 四角の中の位置（-1〜1）
                float3 color : TEXCOORD1;      // 泡の色（明るさを掛けた、線形の値。色を重ねるときは、濃さを掛けた事前乗算の値）
                float bubbleLook : TEXCOORD2;  // 泡の絵にする割合（0 = ぼけた明るい点、1 = 縁と光の点のある泡）
                float alpha : TEXCOORD3;       // 後ろを隠す割合（色を重ねるときだけ。光を足すときは 0）
                float haze : TEXCOORD4;        // もやか（1 = もや、0 = 泡）
            };

            static const float2 Corners[6] =
            {
                float2(-1.0, -1.0), float2(1.0, -1.0), float2(-1.0, 1.0),
                float2(-1.0, 1.0), float2(1.0, -1.0), float2(1.0, 1.0),
            };

            // 整数を混ぜる（PCG）。同じ値からは、いつも同じ値が出る
            uint HashUint(uint value)
            {
                uint state = value * 747796405u + 2891336453u;
                uint word = ((state >> ((state >> 28u) + 4u)) ^ state) * 277803737u;
                return (word >> 22u) ^ word;
            }

            // 0〜1 の決まった乱数。泡の番号・輪の種・何番目の値か（salt。16 未満）で決まる
            float Random01(uint bubble, uint ringSeed, uint salt)
            {
                return HashUint(bubble * 16u + salt + ringSeed) * (1.0 / 4294967296.0);
            }

            // 並びの偏りが少ない数列（R2）。番号の小さい方から n 個だけ使っても、輪の上に均等に散らばる
            float2 LowDiscrepancy(uint index)
            {
                uint2 value = index * uint2(3242174889u, 2447445414u) + uint2(2147483648u, 2147483648u);
                return float2(value) * (1.0 / 4294967296.0);
            }

            Varyings Vert(uint vertexId : SV_VertexID, uint instanceId : SV_InstanceID)
            {
                Varyings output = (Varyings)0;
                // 描かない泡は、6 頂点とも画面の外の同じ所に置く（面積 0 で、描かれない）
                output.positionCS = float4(0.0, 0.0, -2.0, 1.0);
                // 輪の中の番号。前の HAZE_PER_RING 個がもや、その後ろが泡（もやを先に描いて、泡をもやの上に乗せる）
                uint slot = vertexId / 6u;
                float2 corner = Corners[vertexId % 6u];
                output.corner = corner;

                uint dataIndex = min(instanceId, (uint)(MAX_RINGS - 1)) * RING_VECTORS;
                float4 ring0 = _Rings[dataIndex];
                float4 ring1 = _Rings[dataIndex + 1u];
                float4 ring2 = _Rings[dataIndex + 2u];
                float4 ring3 = _Rings[dataIndex + 3u];
                float4 ring4 = _Rings[dataIndex + 4u];
                float age = ring0.w;
                float speed = max(ring1.w, 0.01);
                float range = max(ring2.w, 0.01);
                float tanHalf = max(ring3.w, 0.0);
                uint ringSeed = HashUint((uint)ring4.w + 1u);
                bool isBlend = _BlendStyle > 0.5;
                // 輪ごとの決まった乱数（泡の番号 0 の、使っていない salt を使う）
                float lobes = 6.0 + floor(Random01(0u, ringSeed, 13u) * 5.0);   // 波打ちの山の数（6〜10）
                float lobePhase = TWO_PI * Random01(0u, ringSeed, 14u);
                float driftSign = Random01(0u, ringSeed, 15u) < 0.5 ? -1.0 : 1.0;

                if (slot < HAZE_PER_RING)
                {
                    // もや: 管の中心に、輪のまわりに等しく並べた、大きくぼやけた粒。届く距離に向かって、輪がほどけるのに合わせて薄れる
                    uint hazeIndex = slot;
                    uint hazeId = (uint)_BubbleCount + hazeIndex;   // 乱数に使う番号（泡の番号と重ならない）
                    if (age < 0.0)
                    {
                        return output;
                    }
                    float hazeT = min(age, range / speed);
                    float hazeS = speed * hazeT;
                    float hazeProgress = saturate(hazeS / range);
                    float hazeBirth = smoothstep(0.0, 1.0, hazeT / BIRTH_SECONDS);
                    float hazeRingRadius = (_StartRadius + hazeS * tanHalf) * lerp(BIRTH_RADIUS, 1.0, hazeBirth);
                    float hazeTube = _TubeRatio * hazeRingRadius * lerp(1.0, TUBE_THINNING, hazeProgress) * lerp(BIRTH_TUBE, 1.0, hazeBirth);
                    float hazeStrength = _HazeAmount * hazeBirth * (1.0 - smoothstep(_UnravelStart, 1.0, hazeProgress)) * lerp(1.0, END_DIM, hazeProgress);
                    if (hazeStrength <= 0.0)
                    {
                        return output;
                    }
                    float hazeTheta = TWO_PI * (hazeIndex + 0.5 + 0.4 * (Random01(hazeId, ringSeed, 0u) - 0.5)) / HAZE_PER_RING
                        + driftSign * TOROIDAL_DRIFT * hazeT;
                    float hazeWave = WAVE_AMPLITUDE * smoothstep(WAVE_START, 1.0, hazeProgress);
                    float hazeRadial = hazeRingRadius * (1.0 + hazeWave * sin(lobes * hazeTheta + lobePhase));
                    float hazeAxial = hazeRingRadius * hazeWave * AXIAL_WAVE * cos(lobes * hazeTheta + lobePhase);
                    float3 hazeOutward = cos(hazeTheta) * ring2.xyz + sin(hazeTheta) * ring3.xyz;
                    float3 hazePositionWS = ring0.xyz + ring1.xyz * (hazeS + hazeAxial) + hazeOutward * hazeRadial;
                    // 管が細いときも隣のもやと重なるよう、直径は「隣のもやとの間隔 × HAZE_MIN_SPACING」より小さくしない
                    float hazeDiameter = max(HAZE_SIZE * hazeTube, HAZE_MIN_SPACING * TWO_PI * hazeRingRadius / HAZE_PER_RING) + _BubbleSize;

                    float3 hazePositionVS = TransformWorldToView(hazePositionWS);
                    float hazeDepth = max(-hazePositionVS.z, 1.0e-3);
                    bool isHazeOrthographic = unity_OrthoParams.w > 0.5;
                    float hazePixelsPerMeter = abs(UNITY_MATRIX_P[1][1]) * 0.5 * _ScaledScreenParams.y / (isHazeOrthographic ? 1.0 : hazeDepth);
                    float hazePixels = hazeDiameter * hazePixelsPerMeter;
                    if (hazePixels < _MinPixels)
                    {
                        float hazeRatio = hazePixels / max(_MinPixels, 1.0e-3);
                        hazeStrength *= hazeRatio * hazeRatio;
                    }
                    if (!isHazeOrthographic)
                    {
                        hazeStrength *= saturate((hazeDepth - NEAR_FADE_START) / NEAR_FADE_LENGTH);
                    }
                    if (hazeStrength <= 0.0)
                    {
                        return output;
                    }
                    float hazeShownPixels = clamp(hazePixels, _MinPixels, HAZE_MAX_PIXELS);
                    hazePositionVS.xy += corner * (0.5 * hazeShownPixels / max(hazePixelsPerMeter, 1.0e-4));
                    output.positionCS = TransformWViewToHClip(hazePositionVS);
                    output.haze = 1.0;
                    if (isBlend)
                    {
                        output.alpha = HAZE_ALPHA * hazeStrength;
                        output.color = ring4.rgb * (HAZE_DEEP * output.alpha * (1.0 + BLEND_GLOW * _Brightness));
                    }
                    else
                    {
                        output.color = ring4.rgb * (HAZE_GLOW * _Brightness * hazeStrength);
                    }
                    return output;
                }

                // 泡の番号（0 〜 泡の数 − 1）
                uint bubble = slot - HAZE_PER_RING;
                if (bubble >= (uint)_BubbleCount)
                {
                    return output;
                }

                // 泡ごとの決まった乱数
                float randomU = Random01(bubble, ringSeed, 0u);
                float randomV = Random01(bubble, ringSeed, 1u);
                float randomShellA = Random01(bubble, ringSeed, 2u);
                float randomShellB = Random01(bubble, ringSeed, 3u);
                float randomFill = Random01(bubble, ringSeed, 4u);
                float randomSwirl = Random01(bubble, ringSeed, 5u);
                float randomDetach = Random01(bubble, ringSeed, 6u);
                float randomShed = Random01(bubble, ringSeed, 7u);
                float randomSize = Random01(bubble, ringSeed, 8u);
                float randomBright = Random01(bubble, ringSeed, 9u);
                float randomSparkle = Random01(bubble, ringSeed, 10u);
                float randomWobble = Random01(bubble, ringSeed, 11u);
                float randomPop = Random01(bubble, ringSeed, 12u);

                // 輪の上の場所（輪のまわり u、管のまわり v）。偏りの少ない数列に、泡どうしの間隔ほどの乱数を足す
                float2 sequence = LowDiscrepancy(bubble);
                float jitter = JITTER_SCALE * rsqrt(max(_BubbleCount, 1.0));
                float u = frac(sequence.x + (randomU - 0.5) * jitter);
                float v = frac(sequence.y + (randomV - 0.5) * jitter);

                // 泡が輪を離れる位置。ほとんどは「ほどけ始める位置」から届く距離までの間で、泡の元の場所 u が波打ちの山と同じ数の塊に分かれて残る
                // （数珠のようにちぎれる。山の形は輪のまわりをゆっくり回った後の場所で決まるので、輪が遅いほど、塊は山の所からずれる）
                // ごく一部は、途中でこぼれる
                float keep = 0.5 + 0.5 * sin(lobes * TWO_PI * u + lobePhase);
                float detachFraction = lerp(_UnravelStart, 1.0, saturate(0.55 * randomDetach + 0.45 * keep));
                if (randomShed < SHED_RATIO)
                {
                    detachFraction = lerp(SHED_START, _UnravelStart, randomDetach);
                }
                float detachTime = detachFraction * range / speed;
                float t = min(age, detachTime);            // 輪の上にいた最後の時刻（離れた泡は、離れたときの場所から浮かぶ）
                float tau = max(age - detachTime, 0.0);    // 輪を離れてからの時間
                float floatProgress = tau / max(_FloatSeconds, 0.01);
                if (age < 0.0 || floatProgress >= 1.0)
                {
                    return output;   // まだ出ていないか、もう消えた泡
                }

                // 輪の大きさ。半径 = 出たときの半径 ＋ 進んだ距離 × tan(半角)（広がりは当たり判定の円すいの半角に合わせる。輪の中心が波面と同じ距離を進む）
                // 生まれた直後は、小さな塊から輪へ広がる。管は進むほど少し細る
                float s = speed * t;
                float progress = saturate(s / range);
                float birth = smoothstep(0.0, 1.0, t / BIRTH_SECONDS);
                float ringRadius = (_StartRadius + s * tanHalf) * lerp(BIRTH_RADIUS, 1.0, birth);
                float tubeRadius = _TubeRatio * ringRadius * lerp(1.0, TUBE_THINNING, progress) * lerp(BIRTH_TUBE, 1.0, birth);

                // 輪の大きさに合わせて使う泡を増やす（番号の小さい泡から使う。増えた泡は少しずつ現れる）
                float fullRadius = _StartRadius + range * tanHalf;
                float activeCount = _BubbleCount * clamp(ringRadius / max(fullRadius, 0.001), MIN_COUNT_FRACTION, 1.0);
                float countFade = saturate((activeCount - (float)bubble) / max(COUNT_FADE_BAND * _BubbleCount, 1.0));
                if (countFade <= 0.0)
                {
                    return output;
                }

                // 輪の波打ち。後半に、輪のまわりに 6〜10 個の山がその場でふくらむ（山は輪のまわりを回らない）
                float theta = TWO_PI * u + driftSign * TOROIDAL_DRIFT * t;
                float waveAmount = WAVE_AMPLITUDE * smoothstep(WAVE_START, 1.0, progress);
                float radial = ringRadius * (1.0 + waveAmount * sin(lobes * theta + lobePhase));
                float axial = ringRadius * waveAmount * AXIAL_WAVE * cos(lobes * theta + lobePhase);

                // 管の上の場所。多くを管の表面の近くに集める（_ShellFocus が 1 に近いほど薄い殻、0 なら管の中に一様に散る）
                float gaussian = sqrt(-2.0 * log(max(randomShellA, 1.0e-6))) * cos(TWO_PI * randomShellB);
                float shell = 1.0 + SHELL_SPREAD * clamp(gaussian, -2.5, 2.5);
                float rho = tubeRadius * lerp(sqrt(randomFill), shell, _ShellFocus);
                // 管のまわりの回り。φ を減らす向きに回すと、輪の穴の内側（φ = π）が前へ、外側（φ = 0）が後ろへ流れる（本物の渦の輪と同じ）
                float swirl = _SwirlSpeed * lerp(SWIRL_SPREAD_MIN, SWIRL_SPREAD_MAX, randomSwirl);
                float phi = TWO_PI * (v - swirl * t);

                float3 axis = ring1.xyz;
                float3 outward = cos(theta) * ring2.xyz + sin(theta) * ring3.xyz;
                float3 tubeNormal = outward * cos(phi) + axis * sin(phi);
                float3 positionWS = ring0.xyz + axis * (s + axial) + outward * radial + tubeNormal * rho;

                // 色を重ねるときは、「明るさ」を掛けずに 1 を基準にして数える（ここから先の値が、そのまま後ろを隠す濃さの元になる。「明るさ」は光らせる強さだけに効く）
                float brightness = (isBlend ? 1.0 : _Brightness) * lerp(1.0 - BRIGHTNESS_SPREAD, 1.0 + BRIGHTNESS_SPREAD, randomBright);
                brightness *= birth * countFade * lerp(1.0, END_DIM, progress);
                float glowGain = 1.0;   // きらめき・はじけで明るくなる倍率（色を重ねるとき、1 を超えた分だけを光として足す）
                float diameter = _BubbleSize * (SIZE_MIN + (SIZE_MAX - SIZE_MIN) * randomSize * randomSize * randomSize);
                if (tau > 0.0)
                {
                    // 輪を離れた泡は、前へ少し流れて止まり、上へ浮かびながら小さく揺れて消える
                    float rise = min(0.5 * FLOAT_ACCEL * tau * tau, FLOAT_RISE_MAX);
                    float drift = min(FORWARD_DRIFT_PER_SPEED * speed, FORWARD_DRIFT_MAX) * (1.0 - exp(-4.0 * tau));
                    float wobblePhase = TWO_PI * randomWobble;
                    float3 wobble = (sin(7.0 * tau + wobblePhase) * ring2.xyz + cos(5.0 * tau + wobblePhase) * ring3.xyz) * (WOBBLE * saturate(4.0 * tau));
                    positionWS += float3(0.0, rise, 0.0) + axis * drift + wobble;
                    diameter *= 1.0 + DETACH_GROWTH * floatProgress;
                    float fade = 1.0 - smoothstep(FLOAT_FADE_START, 1.0, floatProgress);
                    if (randomPop < POP_RATIO)
                    {
                        // ごく一部は、消える前に一瞬明るくふくらんではじける
                        float pop = smoothstep(0.6, 0.8, floatProgress) * (1.0 - smoothstep(0.8, 0.95, floatProgress));
                        brightness *= 1.0 + POP_GAIN * pop;
                        glowGain *= 1.0 + POP_GAIN * pop;
                        diameter *= 1.0 + 0.4 * pop;
                        fade = 1.0 - smoothstep(0.85, 1.0, floatProgress);
                    }
                    brightness *= fade;
                }

                // きらめき（ごく一部の泡だけ、ひときわ明るく白く）
                float sparkle = randomSparkle < SPARKLE_RATIO ? 1.0 : 0.0;
                brightness *= lerp(1.0, SPARKLE_GAIN, sparkle);
                glowGain *= lerp(1.0, SPARKLE_GAIN, sparkle);

                // カメラから見て管の奥側の泡を暗くする（手前と奥の明るさの差で、回る向きが読める）
                float facing = dot(tubeNormal, GetWorldSpaceNormalizeViewDir(positionWS));
                brightness *= lerp(1.0 - _BackDim, 1.0, saturate(0.5 + 0.5 * facing));

                // 画面上の大きさ。下限より小さく写る泡は下限の大きさにして、その分暗くする（光の量を保ち、遠くのちらつきを止める）
                float3 positionVS = TransformWorldToView(positionWS);
                float depth = max(-positionVS.z, 1.0e-3);
                bool isOrthographic = unity_OrthoParams.w > 0.5;
                float pixelsPerMeter = abs(UNITY_MATRIX_P[1][1]) * 0.5 * _ScaledScreenParams.y / (isOrthographic ? 1.0 : depth);
                float pixels = diameter * pixelsPerMeter;
                if (pixels < _MinPixels)
                {
                    float ratio = pixels / max(_MinPixels, 1.0e-3);
                    brightness *= ratio * ratio;
                }
                float shownPixels = clamp(pixels, _MinPixels, _MaxPixels);
                if (!isOrthographic)
                {
                    // カメラのすぐ近くの泡は描かない（大きく写りすぎるため）
                    brightness *= saturate((depth - NEAR_FADE_START) / NEAR_FADE_LENGTH);
                }
                if (brightness <= 0.0)
                {
                    return output;
                }

                float halfSize = 0.5 * shownPixels / max(pixelsPerMeter, 1.0e-4);
                positionVS.xy += corner * halfSize;
                output.positionCS = TransformWViewToHClip(positionVS);
                float3 tint = lerp(ring4.rgb, float3(1.0, 1.0, 1.0), sparkle * SPARKLE_WHITEN);
                if (isBlend)
                {
                    // 色を重ねる: 1 を基準にした見え方（ばらつき・奥の暗さ・現れ方・消え方）を、後ろを隠す濃さにする
                    // きらめき・はじける泡だけ、明るくなった分（glowGain − 1）を光として足す（ふつうの泡は、ばらつきで 1 を超えても足さない）
                    float visibility = brightness;
                    output.alpha = saturate(_Coverage * visibility);
                    output.color = tint * (output.alpha * (1.0 + BLEND_GLOW * _Brightness) + BLEND_SPARKLE * (glowGain - 1.0) * (visibility / glowGain));
                }
                else
                {
                    output.color = tint * brightness;
                }
                output.bubbleLook = saturate((shownPixels - BUBBLE_LOOK_PIXELS) / BUBBLE_LOOK_BAND);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float fromCenterSqr = dot(input.corner, input.corner);
                clip(1.0 - fromCenterSqr);
                float fromCenter = sqrt(fromCenterSqr);
                // 小さく写る泡: 真ん中が明るく、外へなだらかに消える丸
                float blob = (1.0 - fromCenterSqr) * (1.0 - fromCenterSqr);
                if (input.haze > 0.5)
                {
                    // もや: 真ん中がいちばん濃く、縁へ向かってなだらかに消える、ぼやけた丸
                    float soft = blob * (1.0 - fromCenterSqr);
                    return half4(input.color * soft, input.alpha * soft);
                }
                // 大きく写る泡: 細い明るい縁、薄く透ける中、左上に光の点 1 つ
                float rim = smoothstep(0.6, 0.85, fromCenter) * (1.0 - smoothstep(0.88, 1.0, fromCenter));
                float2 highlightOffset = input.corner - float2(-0.35, 0.38);
                float highlight = saturate(1.0 - dot(highlightOffset, highlightOffset) / 0.04);
                float bubbleShape = 0.9 * rim + 0.15 * (1.0 - fromCenter) + 1.5 * highlight * highlight;
                float shape = lerp(blob, bubbleShape, input.bubbleLook);
                return half4(input.color * shape, saturate(input.alpha * shape));
            }
            ENDHLSL
        }
    }

    FallBack Off
}
