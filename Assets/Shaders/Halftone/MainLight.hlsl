void MainLight_half(out half3 Direction)
{
#if SHADERGRAPH_PREVIEW
	Direction = half3(0, 1, 0);
#else
	Light light = GetMainLight();
	Direction = light.direction;
#endif
}


// void MainLight_half(out half3 Direction)
// {
// #if SHADERGRAPH_PREVIEW
//     Direction = half3(0, 1, 0);
// #else
//     // URP: usar GetMainLight() si está disponible
//     // Asegúrate de incluir la librería URP adecuada en el shader si hace falta:
//     // #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

//     #ifdef UNITY_PIPELINE_URP
//         Light mainLight = GetMainLight();
//         Direction = mainLight.direction; // dirección normalizada desde la luz
//     #else
//         #ifdef _WorldSpaceLightPos0
//             // Built-in fallback: para directional light w == 0, _WorldSpaceLightPos0.xyz es dirección
//             Direction = half3(_WorldSpaceLightPos0.xyz);
//         #else
//             // Fallback seguro si no hay acceso a URP ni a built-in
//             Direction = half3(0, 0, 1);
//         #endif
//     #endif
// #endif
// }