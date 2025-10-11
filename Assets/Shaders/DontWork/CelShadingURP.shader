Shader "Custom/URP/CelShadingURP"
{
	Properties
	{
		_BaseMap ("Base (RGB)", 2D) = "white" {}
		_Color ("Tint Color", Color) = (1,1,1,1)
		_Ramp ("Lighting Ramp (RGBA)", 2D) = "white" {}
		_RimColor ("Rim Color", Color) = (1,1,1,1)
		_RimPower ("Rim Power", Range(0.1, 8)) = 2.0
		_OutlineColor ("Outline Color", Color) = (0,0,0,1)
		_OutlineWidth ("Outline Width", Range(0,0.1)) = 0.005
		_Cutoff ("Alpha Cutoff", Range(0,1)) = 0.5
	}

	SubShader
	{
		Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }

		// Outline pass - draw backfaces slightly scaled to create a black outline
		Pass
		{
			Name "Outline"
			Tags { "LightMode" = "UniversalForward" }
			Cull Front
			ZWrite On
			ZTest LEqual
			Blend Off

			HLSLPROGRAM
			#pragma vertex vert_outline
			#pragma fragment frag_outline
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

			// Compatibility helper functions (some older examples use these helpers)
			float3 TransformObjectToWorldPos(float4 posOS) { return mul(unity_ObjectToWorld, posOS).xyz; }
			float3 TransformObjectToWorldNormal(float3 nOS) { return normalize(mul((float3x3)unity_ObjectToWorld, nOS)); }
			float4 TransformWorldToHClip(float3 posWS)
			{
			#if defined(UNITY_MATRIX_VP)
				return mul(UNITY_MATRIX_VP, float4(posWS, 1.0));
			#else
				return mul(UNITY_MATRIX_MVP, float4(posWS, 1.0));
			#endif
			}

			struct Attributes
			{
				float4 positionOS : POSITION;
				float3 normalOS   : NORMAL;
			};

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
			};

			float _OutlineWidth;
			float4 _OutlineColor;

			Varyings vert_outline(Attributes v)
			{
				Varyings o;
				// Work in object space to compute an outline offset that scales with view distance
				float3 normalOS = normalize(v.normalOS);
				float3 posOS = v.positionOS.xyz;
				// model-view position to estimate distance to camera
				float3 posVS = mul(UNITY_MATRIX_MV, float4(posOS, 1.0)).xyz;
				float dist = length(posVS);
				posOS += normalOS * _OutlineWidth * dist * 0.5;
				o.positionCS = mul(UNITY_MATRIX_MVP, float4(posOS, 1.0));
				return o;
			}

			half4 frag_outline(Varyings i) : SV_Target
			{
				return _OutlineColor;
			}

			ENDHLSL
		}

		// Main forward pass - cel shading using a ramp texture
		Pass
		{
			Name "UniversalForward"
			Tags { "LightMode" = "UniversalForward" }

			HLSLPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
			#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

			// Compatibility helper functions
			float3 TransformObjectToWorldPos(float4 posOS) { return mul(unity_ObjectToWorld, posOS).xyz; }
			float3 TransformObjectToWorldNormal(float3 nOS) { return normalize(mul((float3x3)unity_ObjectToWorld, nOS)); }
			float4 TransformWorldToHClip(float3 posWS)
			{
			#if defined(UNITY_MATRIX_VP)
				return mul(UNITY_MATRIX_VP, float4(posWS, 1.0));
			#else
				return mul(UNITY_MATRIX_MVP, float4(posWS, 1.0));
			#endif
			}

			TEXTURE2D(_BaseMap);
			SAMPLER(sampler_BaseMap);
			TEXTURE2D(_Ramp);
			SAMPLER(sampler_Ramp);

			float4 _Color;
			float _Cutoff;
			float4 _RimColor;
			float _RimPower;

			struct Attributes
			{
				float4 positionOS : POSITION;
				float3 normalOS   : NORMAL;
				float2 uv         : TEXCOORD0;
			};

			struct Varyings
			{
				float4 positionCS : SV_POSITION;
				float3 normalWS   : NORMAL;
				float3 viewDirWS  : TEXCOORD0;
				float2 uv         : TEXCOORD1;
				// Fog omitted: UNITY_FOG_COORDS not defined in URP Core includes
			};

			Varyings vert(Attributes v)
			{
				Varyings o;
				// World-space position and normal
				float3 posWS = mul(unity_ObjectToWorld, v.positionOS).xyz;
				// Clip space position
				#if defined(UNITY_MATRIX_VP)
					o.positionCS = mul(UNITY_MATRIX_VP, float4(posWS, 1.0));
				#else
					o.positionCS = mul(UNITY_MATRIX_MVP, v.positionOS);
				#endif
				o.normalWS = normalize(mul((float3x3)unity_ObjectToWorld, v.normalOS));
				o.viewDirWS = normalize(_WorldSpaceCameraPos - posWS);
				o.uv = v.uv;
				// Fog transfer omitted
				return o;
			}

			// Simple lighting: sample main directional light from URP
			half4 SampleMainLight(Varyings IN)
			{
				// Get main light data using URP helper
				Light mainLight = GetMainLight();
				float3 lightDir = -mainLight.direction.xyz;
				float3 N = normalize(IN.normalWS);
				float NdotL = saturate(dot(N, lightDir));

				// Quantize lighting using ramp texture
				float rampU = NdotL;
				float4 rampSample = SAMPLE_TEXTURE2D(_Ramp, sampler_Ramp, float2(rampU, 0.5));

				// Rim lighting
				float rim = pow(saturate(1 - dot(normalize(IN.viewDirWS), N)), _RimPower);

				float3 diffuse = rampSample.rgb * mainLight.color.rgb;
				float3 rimCol = _RimColor.rgb * rim;

				return half4(diffuse + rimCol, 1.0);
			}

			half4 frag(Varyings IN) : SV_Target
			{
				float4 baseCol = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _Color;
				if (baseCol.a < _Cutoff) discard;

				half4 lit = SampleMainLight(IN);
				float3 final = baseCol.rgb * lit.rgb;

				return half4(final, baseCol.a);
			}

			ENDHLSL
		}

	}

	Fallback "Diffuse"
}