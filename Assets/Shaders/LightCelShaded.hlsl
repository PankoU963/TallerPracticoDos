// #ifndef LIGHTING_CEL_SHADED_INCLUDED
// #define LIGHTING_CEL_SHADED_INCLUDED

// #ifndef SHADERGRAPH_PREVIEW

// struct EdgeConstants {
//     float diffuse;
//     float specular;
//     float specularOffset;
//     float distanceAttenuation;
//     float shadowAttenuation;
//     float rim;
//     float rimOffset;
// };

// struct SurfaceVariables {
//     float3 normal;
//     float3 view;
//     float smoothness;
//     float shininess;
//     float rimThreshold;
//     EdgeConstants ec;
// };

// float3 CalculateCelShading(Light light, SurfaceVariables s) {
//     // attenuations smoothed and multiplied
//     float shadowAttenuationSmoothstepped = smoothstep(0.0f, s.ec.shadowAttenuation, light.shadowAttenuation);
//     float distanceAttenuationSmoothstepped = smoothstep(0.0f, s.ec.distanceAttenuation, light.distanceAttenuation);
//     float attenuation = shadowAttenuationSmoothstepped * distanceAttenuationSmoothstepped;

//     // diffuse (cel-step)
//     float diffuse = saturate(dot(s.normal, light.direction));
//     diffuse *= attenuation;
//     diffuse = diffuse > 0 ? 1.0 : 0.0;

//     // specular (cel-step)
//     float3 h = SafeNormalize(light.direction + s.view);
//     float specular = saturate(dot(s.normal, h));
//     specular = pow(specular, s.shininess);
//     specular *= diffuse * s.smoothness;
//     specular = specular > 0.2 ? 1.0 : 0.0;

//     // rim
//     float rim = 1.0 - dot(s.view, s.normal);
//     // using rimThreshold as exponent is fine, diffuse is 0 or 1
//     rim *= pow(diffuse, s.rimThreshold);
//     rim = rim > 0.75 ? 1.0 : 0.0;

//     // final smooth remapping controlled by edge constants
//     diffuse = smoothstep(0.0f, s.ec.diffuse, diffuse);
//     specular = s.smoothness * smoothstep((1.0 - s.smoothness) * s.ec.specular + s.ec.specularOffset, s.ec.specular + s.ec.specularOffset, specular);
//     rim = s.smoothness * smoothstep(s.ec.rim - 0.5f * s.ec.rimOffset, s.ec.rim + 0.5f * s.ec.rimOffset, rim);

//     return light.color * (diffuse + max(specular, rim));
// }

// float3 AccumulateAdditionalLights(float3 positionWS, SurfaceVariables s) {
//     float3 result = float3(0, 0, 0);

//     #ifdef _ADDITIONAL_LIGHTS
//     int count = GetAdditionalLightsCount();
//     for (int i = 0; i < count; ++i) {
//         Light light = GetAdditionalLight(i, positionWS);

//         #ifdef _ADDITIONAL_LIGHT_SHADOWS
//         light.shadowAttenuation = AdditionalLightRealtimeShadow(i, positionWS);
//         #else
//         light.shadowAttenuation = 1.0;
//         #endif

//         result += CalculateCelShading(light, s);
//     }
//     #endif

//     return result;
// }

// #endif // SHADERGRAPH_PREVIEW

// void LightingCelShaded_float(float Smoothness, float RimThreshold, float3 Position, float3 Normal,
//     float3 View, float EdgeDiffuse, float EdgeSpecular, float EdgeSpecularOffset, 
//     float EdgeDistanceAttenuation, float EdgeShadowAttenuation, float EdgeRim, float EdgeRimOffset, out float3 Color) {

// #if defined(SHADERGRAPH_PREVIEW)
//     Color = float3(1.0f, 0.0f, 1.0f); // Color de preview
// #else
//     SurfaceVariables s;
//     s.normal = normalize(Normal);
//     s.view = SafeNormalize(View);
//     s.smoothness = Smoothness;
//     s.rimThreshold = RimThreshold;
//     s.shininess = exp2(10.0 * Smoothness + 1.0);

//     // fill edge constants
//     s.ec.diffuse = EdgeDiffuse;
//     s.ec.specular = EdgeSpecular;
//     s.ec.specularOffset = EdgeSpecularOffset;
//     s.ec.distanceAttenuation = EdgeDistanceAttenuation;
//     s.ec.shadowAttenuation = EdgeShadowAttenuation;
//     s.ec.rim = EdgeRim;
//     s.ec.rimOffset = EdgeRimOffset;

//     // main light shadow coord
//     #if SHADOWS_SCREEN
//     float4 clipPos = TransformWorldToHClip(Position);
//     float4 shadowCoord = ComputeScreenPos(clipPos);
//     #else
//     float4 shadowCoord = TransformWorldToShadowCoord(Position);
//     #endif

//     Light mainLight = GetMainLight(shadowCoord);
//     Color = CalculateCelShading(mainLight, s);

//     // accumulate additional lights if present
//     #ifdef _ADDITIONAL_LIGHTS
//     int pixelLightCount = GetAdditionalLightsCount();
//     for (int i = 0; i < pixelLightCount; ++i) {
//         Light add = GetAdditionalLight(i, Position);
//         #ifdef _ADDITIONAL_LIGHT_SHADOWS
//         add.shadowAttenuation = AdditionalLightRealtimeShadow(i, Position);
//         #else
//         add.shadowAttenuation = 1.0;
//         #endif
//         Color += CalculateCelShading(add, s);
//     }
//     #endif

// #endif
// }

// #endif // LIGHTING_CEL_SHADED_INCLUDED


#ifndef LIGHTING_CEL_SHADED_INCLUDED
#define LIGHTING_CEL_SHADED_INCLUDED

#ifndef SHADERGRAPH_PREVIEW

struct EdgeConstants {
    float diffuse;
    float specular;
    float specularOffset;
    float distanceAttenuation;
    float shadowAttenuation;
    float rim;
    float rimOffset;
};

struct SurfaceVariables {
    float3 normal;
    float3 view;
    float smoothness;
    float shininess;
    float rimThreshold;
    EdgeConstants ec;
};

float3 CalculateCelShading(Light light, SurfaceVariables s) {
    // attenuations smoothed and multiplied
    float shadowAttenuationSmoothstepped = smoothstep(0.0f, s.ec.shadowAttenuation, light.shadowAttenuation);
    float distanceAttenuationSmoothstepped = smoothstep(0.0f, s.ec.distanceAttenuation, light.distanceAttenuation);
    float attenuation = shadowAttenuationSmoothstepped * distanceAttenuationSmoothstepped;

    // diffuse (cel-step)
    float diffuse = saturate(dot(s.normal, light.direction));
    diffuse *= attenuation;
    diffuse = diffuse > 0 ? 1.0 : 0.0;

    // specular (cel-step)
    float3 h = SafeNormalize(light.direction + s.view);
    float specular = saturate(dot(s.normal, h));
    specular = pow(specular, s.shininess);
    specular *= diffuse * s.smoothness;
    specular = specular > 0.2 ? 1.0 : 0.0;

    // rim
    float rim = 1.0 - dot(s.view, s.normal);
    // using rimThreshold as exponent is fine, diffuse is 0 or 1
    rim *= pow(diffuse, s.rimThreshold);
    rim = rim > 0.75 ? 1.0 : 0.0;

    // final smooth remapping controlled by edge constants
    diffuse = smoothstep(0.0f, s.ec.diffuse, diffuse);
    specular = s.smoothness * smoothstep((1.0 - s.smoothness) * s.ec.specular + s.ec.specularOffset, s.ec.specular + s.ec.specularOffset, specular);
    rim = s.smoothness * smoothstep(s.ec.rim - 0.5f * s.ec.rimOffset, s.ec.rim + 0.5f * s.ec.rimOffset, rim);

    return light.color * (diffuse + max(specular, rim));
}

float3 AccumulateAdditionalLights(float3 positionWS, SurfaceVariables s) {
    float3 result = float3(0, 0, 0);

    #ifdef _ADDITIONAL_LIGHTS
    int count = GetAdditionalLightsCount();
    for (int i = 0; i < count; ++i) {
        Light light = GetAdditionalLight(i, positionWS);

        #ifdef _ADDITIONAL_LIGHT_SHADOWS
        light.shadowAttenuation = AdditionalLightRealtimeShadow(i, positionWS);
        #else
        light.shadowAttenuation = 1.0;
        #endif

        result += CalculateCelShading(light, s);
    }
    #endif

    return result;
}

#endif // SHADERGRAPH_PREVIEW

void LightingCelShaded_float(float Smoothness, float RimThreshold, float3 Position, float3 Normal,
    float3 View, float EdgeDiffuse, float EdgeSpecular, float EdgeSpecularOffset, 
    float EdgeDistanceAttenuation, float EdgeShadowAttenuation, float EdgeRim, float EdgeRimOffset, out float3 Color) {

#if defined(SHADERGRAPH_PREVIEW)
    Color = float3(1.0f, 0.0f, 1.0f); // Color de preview
#else
    SurfaceVariables s;
    s.normal = normalize(Normal);
    s.view = SafeNormalize(View);
    s.smoothness = Smoothness;
    s.rimThreshold = RimThreshold;
    s.shininess = exp2(10.0 * Smoothness + 1.0);

    // fill edge constants
    s.ec.diffuse = EdgeDiffuse;
    s.ec.specular = EdgeSpecular;
    s.ec.specularOffset = EdgeSpecularOffset;
    s.ec.distanceAttenuation = EdgeDistanceAttenuation;
    s.ec.shadowAttenuation = EdgeShadowAttenuation;
    s.ec.rim = EdgeRim;
    s.ec.rimOffset = EdgeRimOffset;

    // main light shadow coord
    #if SHADOWS_SCREEN
    float4 clipPos = TransformWorldToHClip(Position);
    float4 shadowCoord = ComputeScreenPos(clipPos);
    #else
    float4 shadowCoord = TransformWorldToShadowCoord(Position);
    #endif

    Light mainLight = GetMainLight(shadowCoord);
    Color = CalculateCelShading(mainLight, s);

    // accumulate additional lights if present
    #ifdef _ADDITIONAL_LIGHTS
    int pixelLightCount = GetAdditionalLightsCount();
    for (int i = 0; i < pixelLightCount; ++i) {
        Light add = GetAdditionalLight(i, Position);
        #ifdef _ADDITIONAL_LIGHT_SHADOWS
        add.shadowAttenuation = AdditionalLightRealtimeShadow(i, Position);
        #else
        add.shadowAttenuation = 1.0;
        #endif
        Color += CalculateCelShading(add, s);
    }
    #endif

#endif
}

#endif // LIGHTING_CEL_SHADED_INCLUDED
