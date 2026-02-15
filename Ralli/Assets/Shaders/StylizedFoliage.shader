Shader "Ralli/StylizedFoliage"
{
    Properties
    {
        _BaseMap("Blob Source (A = Cutout)", 2D) = "white" {}
        _BottomColor("Bottom Color", Color) = (0.16, 0.34, 0.17, 1)
        _TopColor("Top Color", Color) = (0.36, 0.60, 0.24, 1)
        _GradientMinY("Gradient Min Y", Float) = 0
        _GradientMaxY("Gradient Max Y", Float) = 10
        _BlobTiling("Blob Tiling", Float) = 6
        _BlobNoiseScale("Blob Noise Scale", Float) = 0.22
        _BlobNoiseStrength("Blob Noise Strength", Range(0, 1)) = 0.2
        _Cutoff("Alpha Cutoff", Range(0, 1)) = 0.45
        _LightSteps("Light Steps", Range(2, 6)) = 3
        _BandSoftness("Band Softness", Range(0, 1)) = 0.35
        _MinLight("Min Light", Range(0, 1)) = 0.3
        _ShadowStrength("Shadow Strength", Range(0, 1)) = 0.8
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "AlphaTest"
            "RenderType" = "TransparentCutout"
        }

        Cull Off
        ZWrite On

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
                float4 shadowCoord : TEXCOORD3;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
            float4 _BottomColor;
            float4 _TopColor;
            float _GradientMinY;
            float _GradientMaxY;
            float _BlobTiling;
            float _BlobNoiseScale;
            float _BlobNoiseStrength;
            float _Cutoff;
            float _LightSteps;
            float _BandSoftness;
            float _MinLight;
            float _ShadowStrength;
            CBUFFER_END

            float Hash21(float2 p)
            {
                p = frac(p * float2(234.34, 435.345));
                p += dot(p, p + 34.23);
                return frac(p.x * p.y);
            }

            float2 GetBlobUv(float2 uv, float3 positionWS)
            {
                float tiling = max(1.0, _BlobTiling);
                float2 cell = floor(uv * tiling);
                float n = Hash21(cell + positionWS.xz * _BlobNoiseScale);
                float2 jitter = (n - 0.5) * _BlobNoiseStrength / tiling;
                return (cell + 0.5) / tiling + jitter;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionHCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.shadowCoord = TransformWorldToShadowCoord(output.positionWS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 blobUv = GetBlobUv(input.uv, input.positionWS);
                half4 blobSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, blobUv);
                clip(blobSample.a - _Cutoff);

                float gradientRange = max(0.001, _GradientMaxY - _GradientMinY);
                float h = saturate((input.positionWS.y - _GradientMinY) / gradientRange);
                half3 canopyBase = lerp(_BottomColor.rgb, _TopColor.rgb, h);

                float3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight(input.shadowCoord);
                float nl = saturate(dot(normalWS, mainLight.direction));

                float steps = max(2.0, _LightSteps);
                float scaled = nl * (steps - 1.0);
                float bandLow = floor(scaled) / (steps - 1.0);
                float bandHigh = ceil(scaled) / (steps - 1.0);
                float f = frac(scaled);
                float band = lerp(bandLow, bandHigh, smoothstep(0.5 - _BandSoftness * 0.5, 0.5 + _BandSoftness * 0.5, f));

                float shadowMul = lerp(1.0, mainLight.shadowAttenuation, _ShadowStrength);
                float lightTerm = max(_MinLight, band * shadowMul);

                return half4(canopyBase * lightTerm, 1.0);
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
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
            float _BlobTiling;
            float _BlobNoiseScale;
            float _BlobNoiseStrength;
            float _Cutoff;
            CBUFFER_END

            float Hash21(float2 p)
            {
                p = frac(p * float2(234.34, 435.345));
                p += dot(p, p + 34.23);
                return frac(p.x * p.y);
            }

            float2 GetBlobUv(float2 uv, float3 positionWS)
            {
                float tiling = max(1.0, _BlobTiling);
                float2 cell = floor(uv * tiling);
                float n = Hash21(cell + positionWS.xz * _BlobNoiseScale);
                float2 jitter = (n - 0.5) * _BlobNoiseStrength / tiling;
                return (cell + 0.5) / tiling + jitter;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.uv = input.uv;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 blobUv = GetBlobUv(input.uv, input.positionWS);
                half4 blobSample = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, blobUv);
                clip(blobSample.a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }
}
