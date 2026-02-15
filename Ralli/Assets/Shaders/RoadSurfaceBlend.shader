Shader "Ralli/RoadSurfaceBlend"
{
    Properties
    {
        _AsphaltColor("Asphalt Color", Color) = (0.13, 0.13, 0.14, 1)
        _ShoulderColor("Shoulder Color", Color) = (0.34, 0.28, 0.22, 1)
        _ForestColor("Forest Floor Color", Color) = (0.20, 0.34, 0.18, 1)
        _BlendSharpness("Surface Blend Sharpness", Range(1, 16)) = 5
        _RoadHalfWidth("Road Half Width (m)", Float) = 4
        _MarkingColor("Marking Color", Color) = (0.93, 0.93, 0.9, 1)
        _CenterLineWidth("Center Line Width (m)", Float) = 0.12
        _CenterDashLength("Center Dash Length (m)", Float) = 3.0
        _CenterGapLength("Center Gap Length (m)", Float) = 9.0
        _EdgeLineWidth("Edge Line Width (m)", Float) = 0.11
        _EdgeLineInset("Edge Line Inset (m)", Float) = 0.22
        _MarkingFeather("Marking Feather (m)", Float) = 0.03
        _MarkingWearScale("Marking Wear Scale", Float) = 0.18
        _MarkingWearStrength("Marking Wear Strength", Range(0, 1)) = 0.22
        _MinLighting("Min Lighting", Range(0, 1)) = 0.35
        _AsphaltNoiseScale("Asphalt Noise Scale", Float) = 0.5
        _AsphaltNoiseIntensity("Asphalt Noise Intensity", Range(0, 0.5)) = 0.12
        _DirtNoiseScale("Dirt Noise Scale", Float) = 0.3
        _DirtNoiseIntensity("Dirt Noise Intensity", Range(0, 0.5)) = 0.15
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float4 color : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float2 uv : TEXCOORD3;
            };

            CBUFFER_START(UnityPerMaterial)
            float4 _AsphaltColor;
            float4 _ShoulderColor;
            float4 _ForestColor;
            float _BlendSharpness;
            float _RoadHalfWidth;
            float4 _MarkingColor;
            float _CenterLineWidth;
            float _CenterDashLength;
            float _CenterGapLength;
            float _EdgeLineWidth;
            float _EdgeLineInset;
            float _MarkingFeather;
            float _MarkingWearScale;
            float _MarkingWearStrength;
            float _MinLighting;
            float _AsphaltNoiseScale;
            float _AsphaltNoiseIntensity;
            float _DirtNoiseScale;
            float _DirtNoiseIntensity;
            CBUFFER_END

            // Hash-based pseudo-random noise, returns -1..1
            float hash21(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * float3(443.897, 441.423, 437.195));
                p3 += dot(p3, p3.yzx + 19.19);
                return frac((p3.x + p3.y) * p3.z) * 2.0 - 1.0;
            }

            // Value noise with smooth interpolation
            float valueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f); // smoothstep

                float a = hash21(i);
                float b = hash21(i + float2(1.0, 0.0));
                float c = hash21(i + float2(0.0, 1.0));
                float d = hash21(i + float2(1.0, 1.0));

                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            // Two-octave noise
            float noise2oct(float2 p)
            {
                float n = valueNoise(p) * 0.65;
                n += valueNoise(p * 3.17 + 71.7) * 0.35;
                return n;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float sharpness = max(1.0, _BlendSharpness);
                float asphaltWeight = pow(saturate(input.color.r), sharpness);
                float dirtWeight = pow(saturate(input.color.g), sharpness);
                float forestMask = pow(saturate(input.color.b), sharpness);
                float forestWeight = dirtWeight * forestMask;
                float shoulderWeight = dirtWeight * (1.0 - forestMask);
                float weightSum = max(0.0001, asphaltWeight + shoulderWeight + forestWeight);
                asphaltWeight /= weightSum;
                shoulderWeight /= weightSum;
                forestWeight /= weightSum;

                float2 worldXZ = input.positionWS.xz;

                // Asphalt noise: grey variation — worn patches and fine grain
                float asphaltNoise = noise2oct(worldXZ * _AsphaltNoiseScale);
                half3 asphaltCol = _AsphaltColor.rgb * (1.0 + asphaltNoise * _AsphaltNoiseIntensity);

                // Dirt noise: warm variation — sandy to muddy patches
                float dirtNoise = noise2oct(worldXZ * _DirtNoiseScale + 200.0);
                half3 shoulderCol = _ShoulderColor.rgb * (1.0 + dirtNoise * _DirtNoiseIntensity);

                half3 baseColor = asphaltCol * asphaltWeight
                                + shoulderCol * shoulderWeight
                                + _ForestColor.rgb * forestWeight;

                // Procedural Finnish-style markings in road-local meters:
                // uv.x = lateral offset from centerline, uv.y = distance along road.
                float lateral = input.uv.x;
                float along = input.uv.y;
                float feather = max(0.001, _MarkingFeather);

                float centerLineHalf = max(0.02, _CenterLineWidth * 0.5);
                float centerDistance = abs(lateral);
                float centerBand = 1.0 - smoothstep(centerLineHalf - feather, centerLineHalf + feather, centerDistance);
                float dashLen = max(0.1, _CenterDashLength);
                float gapLen = max(0.1, _CenterGapLength);
                float dashPeriod = dashLen + gapLen;
                float dashT = frac(along / dashPeriod);
                float centerDashed = centerBand * step(dashT, dashLen / dashPeriod);

                float edgeHalf = max(0.02, _EdgeLineWidth * 0.5);
                float edgeCenter = max(0.0, _RoadHalfWidth - max(0.0, _EdgeLineInset) - edgeHalf);
                float edgeDistance = abs(abs(lateral) - edgeCenter);
                float edgeSolid = 1.0 - smoothstep(edgeHalf - feather, edgeHalf + feather, edgeDistance);

                // Keep markings constrained to asphalt-dominant area and add slight wear.
                float asphaltMask = saturate((asphaltWeight - 0.55) * 3.0);
                float wear = saturate(0.5 + 0.5 * noise2oct(input.positionWS.xz * _MarkingWearScale + 77.0));
                float wearMask = lerp(1.0, wear, saturate(_MarkingWearStrength));
                float markingMask = max(centerDashed, edgeSolid) * asphaltMask * wearMask;
                baseColor = lerp(baseColor, _MarkingColor.rgb, markingMask);

                float3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight();
                float nl = saturate(dot(normalWS, mainLight.direction));
                float lighting = max(_MinLighting, nl);

                return half4(baseColor * lighting, 1.0);
            }
            ENDHLSL
        }
    }
}
