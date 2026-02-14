Shader "Ralli/ForestSkybox"
{
    Properties
    {
        _SkyColor ("Sky Color", Color) = (0.5, 0.7, 1.0, 1.0)
        _HorizonColor ("Horizon Color", Color) = (0.8, 0.85, 0.9, 1.0)
        _GroundColor ("Ground Color", Color) = (0.15, 0.25, 0.1, 1.0)
        _HorizonSharpness ("Horizon Sharpness", Range(1, 50)) = 10
    }

    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _SkyColor;
                half4 _HorizonColor;
                half4 _GroundColor;
                half _HorizonSharpness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 viewDir : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.viewDir = input.positionOS.xyz;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 dir = normalize(input.viewDir);
                float y = dir.y;

                // Blend factor: 0 at horizon, 1 at zenith/nadir
                float skyBlend = saturate(y * _HorizonSharpness);
                float groundBlend = saturate(-y * _HorizonSharpness);

                half3 col = lerp(_HorizonColor.rgb, _SkyColor.rgb, skyBlend);
                col = lerp(col, _GroundColor.rgb, groundBlend);

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
}