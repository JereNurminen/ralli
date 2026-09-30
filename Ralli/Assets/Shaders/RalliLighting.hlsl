#ifndef RALLI_LIGHTING_INCLUDED
#define RALLI_LIGHTING_INCLUDED

// Diffuse lighting shared by Ralli's custom shaders, so they react to the same lights as URP Lit:
// ambient (from the scene's ambient/lighting preset), the sun with its shadows, and every
// additional light (headlights, police beacons) through URP's Forward / Forward+ light loop.
// Needs: Core.hlsl + Lighting.hlsl, and the pragmas listed in RALLI_LIGHTING_PRAGMAS below.
//
// #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
// #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
// #pragma multi_compile _ _FORWARD_PLUS
// #pragma multi_compile_fragment _ _SHADOWS_SOFT
// #pragma multi_compile_fog

half3 RalliAdditionalLightsDiffuse(float3 positionWS, half3 normalWS, float4 positionCS)
{
    half3 lighting = 0;
#if defined(_ADDITIONAL_LIGHTS)
    InputData inputData = (InputData)0;
    inputData.positionWS = positionWS;
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(positionCS);
    uint lightCount = GetAdditionalLightsCount();
    LIGHT_LOOP_BEGIN(lightCount)
        Light light = GetAdditionalLight(lightIndex, positionWS, half4(1, 1, 1, 1));
        lighting += light.color * (light.distanceAttenuation * light.shadowAttenuation * saturate(dot(normalWS, light.direction)));
    LIGHT_LOOP_END
#endif
    return lighting;
}

Light RalliGetMainLight(float3 positionWS)
{
    return GetMainLight(TransformWorldToShadowCoord(positionWS));
}

// Wrapped diffuse: light reaches a bit past the terminator, so surfaces turning away from the sun
// keep their color instead of going muddy. Stylized rather than physical, on purpose.
#define RALLI_SUN_WRAP 0.4

// Ambient + sun (with shadows) + additional lights.
half3 RalliDiffuseLighting(float3 positionWS, half3 normalWS, float4 positionCS)
{
    Light mainLight = RalliGetMainLight(positionWS);
    half wrapped = saturate((dot(normalWS, mainLight.direction) + RALLI_SUN_WRAP) / (1.0 + RALLI_SUN_WRAP));
    half3 sun = mainLight.color * (mainLight.shadowAttenuation * wrapped);
    return SampleSH(normalWS) + sun + RalliAdditionalLightsDiffuse(positionWS, normalWS, positionCS);
}

#endif
