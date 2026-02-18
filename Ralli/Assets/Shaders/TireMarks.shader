Shader "Ralli/TireMarks"
{
    Properties
    {
        _AsphaltColor ("Asphalt Mark Color", Color) = (0.05, 0.05, 0.05, 1)
        _GravelColor ("Gravel Mark Color", Color) = (0.35, 0.28, 0.18, 1)
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent+1"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "TireMarks"
            Tags { "LightMode" = "UniversalForward" }

            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Offset -1, -1
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _AsphaltColor;
                float4 _GravelColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 color      : COLOR;
                float  fogFactor  : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs posInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = posInputs.positionCS;
                output.color = input.color;
                output.fogFactor = ComputeFogFactor(posInputs.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // vertex color: R = asphalt weight (1=asphalt, 0=gravel), A = opacity
                float asphaltWeight = input.color.r;
                half3 markColor = lerp(_GravelColor.rgb, _AsphaltColor.rgb, asphaltWeight);
                markColor = MixFog(markColor, input.fogFactor);
                return half4(markColor, input.color.a);
            }
            ENDHLSL
        }
    }
}
