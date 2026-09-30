// Faint fake volumetric beam for spot lights: additive, brightest at the apex, fading along the
// beam's length and toward its silhouette edges so it reads as haze rather than a solid cone.
Shader "Ralli/LightCone"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 1, 1, 1)
        _LengthFalloff ("Length Falloff", Range(0.5, 6)) = 1.8
        _EdgeSoftness ("Edge Softness", Range(0.5, 6)) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _LengthFalloff;
                float _EdgeSoftness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float along : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.along = input.uv.y;
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
                float facing = abs(dot(normalize(input.normalWS), viewDir));
                float lengthFade = pow(saturate(1.0 - input.along), _LengthFalloff);
                float edgeFade = pow(facing, _EdgeSoftness);
                half3 beam = _Color.rgb * (lengthFade * edgeFade);
                return half4(MixFogColor(beam, half3(0, 0, 0), input.fogFactor), 1.0);
            }
            ENDHLSL
        }
    }
}
