Shader "Ralli/RoadSurfaceBlend"
{
    Properties
    {
        _AsphaltColor("Asphalt Color", Color) = (0.13, 0.13, 0.14, 1)
        _ShoulderColor("Shoulder Color", Color) = (0.34, 0.28, 0.22, 1)
        _ForestColor("Forest Floor Color", Color) = (0.20, 0.34, 0.18, 1)
        _MinLighting("Min Lighting", Range(0, 1)) = 0.35
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
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float4 color : TEXCOORD1;
            };

            CBUFFER_START(UnityPerMaterial)
            float4 _AsphaltColor;
            float4 _ShoulderColor;
            float4 _ForestColor;
            float _MinLighting;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float asphaltWeight = saturate(input.color.r);
                float dirtWeight = saturate(input.color.g);
                float forestMask = saturate(input.color.b);
                float forestWeight = dirtWeight * forestMask;
                float shoulderWeight = dirtWeight * (1.0 - forestMask);
                float weightSum = max(0.0001, asphaltWeight + shoulderWeight + forestWeight);
                asphaltWeight /= weightSum;
                shoulderWeight /= weightSum;
                forestWeight /= weightSum;

                half4 baseColor = _AsphaltColor * asphaltWeight + _ShoulderColor * shoulderWeight + _ForestColor * forestWeight;
                float3 normalWS = normalize(input.normalWS);
                Light mainLight = GetMainLight();
                float nl = saturate(dot(normalWS, mainLight.direction));
                float lighting = max(_MinLighting, nl);

                return half4(baseColor.rgb * lighting, 1.0);
            }
            ENDHLSL
        }
    }
}
