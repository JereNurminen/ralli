// Mirror panel: shows a mirror camera's render texture, flipped left-right like a real mirror,
// trimmed to a superellipse (0 = rectangle, 1 = ellipse). Unlit so it stays readable.
Shader "Ralli/MirrorView"
{
    Properties
    {
        _MainTex ("Mirror Texture", 2D) = "black" {}
        _Roundness ("Roundness (0 = rectangle, 1 = ellipse)", Range(0, 1)) = 0.3
        _Brightness ("Brightness", Range(0, 2)) = 0.9
    }

    SubShader
    {
        Tags { "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Roundness;
                float _Brightness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                // Superellipse |x|^n + |y|^n <= 1: n = 2 is an ellipse, large n approaches a rectangle.
                float2 q = abs(input.uv - 0.5) * 2.0;
                float n = lerp(16.0, 2.0, _Roundness);
                clip(1.0 - (pow(q.x, n) + pow(q.y, n)));

                float2 mirroredUv = float2(1.0 - input.uv.x, input.uv.y);
                half3 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, mirroredUv).rgb;
                return half4(color * _Brightness, 1.0);
            }
            ENDHLSL
        }
    }
}
