// Additive glow flare for vehicle lights. Everything happens on the GPU: the quad billboards toward
// the camera around its object's origin, keeps a minimum on-screen size at distance (so far-off
// headlights stay readable), and brightens as the light's forward (+Z) faces the viewer.
Shader "Ralli/LightFlare"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1, 1, 1, 1)
        _BaseSize ("Base Size (m)", Float) = 0.3
        _AngularSize ("Min Angular Size", Float) = 0.006
        _Directionality ("Directionality", Float) = 3
        _NearFade ("Near Fade Distance (m)", Float) = 2
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _BaseSize;
                float _AngularSize;
                float _Directionality;
                float _NearFade;
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
                float intensity : TEXCOORD1;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 centerWS = TransformObjectToWorld(float3(0, 0, 0));
                float3 toCamera = _WorldSpaceCameraPos - centerWS;
                float distance = max(0.001, length(toCamera));
                float3 toCameraDir = toCamera / distance;
                float size = max(_BaseSize, distance * _AngularSize);

                float3 cameraRight = UNITY_MATRIX_V[0].xyz;
                float3 cameraUp = UNITY_MATRIX_V[1].xyz;
                float3 positionWS = centerWS + (cameraRight * input.positionOS.x + cameraUp * input.positionOS.y) * (size * 2.0);
                // Nudge toward the camera so the flare doesn't sink into the car body.
                positionWS += toCameraDir * 0.2;
                output.positionCS = TransformWorldToHClip(positionWS);

                float3 forwardWS = normalize(TransformObjectToWorldDir(float3(0, 0, 1)));
                float facing = saturate(dot(forwardWS, toCameraDir));
                output.intensity = pow(facing, _Directionality) * saturate((distance - _NearFade) / max(0.01, _NearFade));
                output.uv = input.uv;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float r = saturate(length(input.uv - 0.5) * 2.0);
                float glow = 1.0 - r;
                glow = glow * glow * glow + smoothstep(0.25, 0.0, r) * 0.6;
                return half4(_Color.rgb * (glow * input.intensity), 1.0);
            }
            ENDHLSL
        }
    }
}
