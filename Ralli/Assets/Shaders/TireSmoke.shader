Shader "Ralli/TireSmoke"
{
    Properties
    {
        _MainTex ("Particle Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (0.75, 0.73, 0.70, 1)
        _AlphaSteps ("Alpha Steps", Range(2, 8)) = 3
        _AlphaCutoff ("Alpha Floor", Range(0, 0.5)) = 0.08
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+2"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "TireSmoke"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #pragma instancing_options procedural:ParticleInstancingSetup

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float _AlphaSteps;
                float _AlphaCutoff;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
                float2 uv         : TEXCOORD0;
                float  fogFactor  : TEXCOORD1;
                float3 normalWS   : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = posInputs.positionCS;
                output.color = input.color;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                // Billboard particles face camera; use view-space forward as normal for simple shading.
                output.normalWS = normalize(GetWorldSpaceViewDir(posInputs.positionWS));
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);

                half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                half rawAlpha = texColor.a * input.color.a * _Color.a;

                // Cel-shaded alpha: quantize into discrete steps.
                half steps = max(2.0, _AlphaSteps);
                half steppedAlpha = ceil(rawAlpha * steps) / steps;

                // Cut very low alpha to prevent ghostly edges.
                steppedAlpha = steppedAlpha < _AlphaCutoff ? 0.0 : steppedAlpha;

                // Simple cel-shaded lighting: 2-band (lit vs shadow).
                Light mainLight = GetMainLight();
                half nl = saturate(dot(input.normalWS, mainLight.direction));
                half lighting = nl > 0.3 ? 1.0 : 0.65;

                half3 finalColor = _Color.rgb * input.color.rgb * lighting;
                finalColor = MixFog(finalColor, input.fogFactor);

                return half4(finalColor, steppedAlpha);
            }
            ENDHLSL
        }
    }
}