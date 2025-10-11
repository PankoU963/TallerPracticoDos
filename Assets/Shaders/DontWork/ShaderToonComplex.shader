Shader "ShaderToon/Toon"
{
	Properties
	{
		_OutlineColor("Outline Color", Color) = (0,0,0,1)
		_OutlineWidth("Outline Width", Range(0,0.2)) = 0.01

		_Color("Color", Color) = (0.5, 0.65, 1, 1)
		_MainTex("Main Texture", 2D) = "white" {}	
		_AmbientColor("Ambient Color", Color) = (0.4, 0.4, 0.4, 1)
		[HDR]
		_SpecularColor("Specular Color", Color) = (0.9, 0.9, 0.9, 1)
		_Glossiness("Glossiness", Float) = 32

		[HDR]
		_RimColor("Rim Color", Color) = (1, 1, 1, 1)
		_RimAmount("Rim Amount", Range(0,1)) = 0.716
		_RimThreshold("Rim Threshold", Range(0,1)) = 0.1
		_ShowShadowDebug("Show Shadow Debug", Range(0,1)) = 0
		// Controls how strongly the light fades with distance (0 = no attenuation, 1 = full manual attenuation)
		_DistanceAttenuation("Distance Attenuation", Range(0,1)) = 1
		// Maximum distance used for manual attenuation calculations (world units). 0 = disabled
		_MaxLightRange("Max Light Range", Float) = 10
		// Softens the angular cutoff for positional lights (spot/point) to hide hard edges
		_SpotSmoothness("Spot Smoothness", Range(0,1)) = 0.2
		// Multiply shadow influence (use >1 to darken shadows, <1 to lighten)
		_ShadowMultiplier("Shadow Multiplier", Range(0,2)) = 1
		// Ramp and ForwardAdd controls
		_RampTex ("Ramp (RGB)", 2D) = "gray" {}
		_RampSteps ("Ramp Steps", Range(1,16)) = 3
		_RampSmooth ("Ramp Smooth", Range(0,1)) = 0.0
		_ForwardAddUseRamp("ForwardAdd Use Ramp", Range(0,1)) = 0
		_ForwardAddUseSmoothstep("ForwardAdd Use Smoothstep", Range(0,1)) = 0
	}
	SubShader
	{
		Pass
		{
			Tags
			{
				"LightMode" = "ForwardBase"
				//"PassFlags" = "OnlyDirectional"
			}
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_fwdbase
			
			#include "UnityCG.cginc"
			#include "Lighting.cginc"
			#include "AutoLight.cginc"

			struct appdata
			{
				float4 vertex : POSITION;				
				float4 uv : TEXCOORD0;
				float3 normal : NORMAL;
			};

			struct v2f //vertex to fragment
			{
				SHADOW_COORDS(2)
				float4 pos : SV_POSITION;
				float2 uv : TEXCOORD0;
				float3 worldNormal : NORMAL;
				float3 viewDir : TEXCOORD1;
				float3 worldPos : TEXCOORD3; // world-space position, needed for point/spot lights
			};

			sampler2D _MainTex;
			float4 _MainTex_ST;
			
			v2f vert (appdata v)
			{
				v2f o;
				o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				o.worldNormal = UnityObjectToWorldNormal(v.normal);
				o.viewDir = WorldSpaceViewDir(v.vertex);
				o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
				TRANSFER_SHADOW(o)
				return o;
			}
			
			float4 _Color;
			float4 _AmbientColor;
			float4 _SpecularColor;
			float _Glossiness;
			float4 _RimColor;
			float _RimAmount;
			float _RimThreshold;


			// Runtime tuning uniforms (declare in this CGPROGRAM so fragAdd can access them)
			float _DistanceAttenuation;
			float _MaxLightRange;
			float _SpotSmoothness;
			float _ShadowMultiplier;


			float4 frag (v2f i) : SV_Target
			{
				float3 normal = normalize(i.worldNormal);
				// _WorldSpaceLightPos0 is a float4: when w == 0 it's a directional light (direction),
				// when w == 1 it's a positional light (position in world space).
				float3 lightDir0 = (_WorldSpaceLightPos0.w == 0) ? normalize(_WorldSpaceLightPos0.xyz) : normalize(_WorldSpaceLightPos0.xyz - i.worldPos);
				float NdotL = dot(lightDir0, normal);
				//float lightingIntensity = NdotL > 0 ? 1 : 0; //luz dura

				// Apply unity's shadow attenuation and allow an artist-controlled multiplier
				float shadow = SHADOW_ATTENUATION(i) * _ShadowMultiplier;

				// Distance attenuation (manual control): normalize distance into [0,1] using _MaxLightRange
				float distAtten = 1.0;
				if (_MaxLightRange > 0)
				{
					float dist = length((_WorldSpaceLightPos0.xyz - i.worldPos));
					distAtten = saturate(1.0 - dist / _MaxLightRange);
					// allow artist control to scale how strongly distance affects the light
					distAtten = lerp(1.0, distAtten, _DistanceAttenuation);
				}

				// Spot smoothing: soften the angular cutoff of positional lights to reduce hard 'cuts'
				float smoothNdot = NdotL;
				if (_SpotSmoothness > 0 && _WorldSpaceLightPos0.w == 1)
				{
					// For positional lights, apply a small smoothstep to avoid hard angular cuts.
					smoothNdot = smoothstep(-_SpotSmoothness, 1.0, NdotL);
				}

				float lightingIntensity = smoothstep(0.0, 0.02, smoothNdot * shadow * distAtten); //luz suave
				float4 light = lightingIntensity * _LightColor0;

				float3 viewDir = normalize(i.viewDir);

				float3 halfVec = normalize(_WorldSpaceLightPos0 + viewDir);
				float NdotH = dot(normal, halfVec);

				float specularIntensity = pow(NdotH * lightingIntensity, _Glossiness * _Glossiness);
				float specularIntensitySmooth = smoothstep(0.005, 0.01, specularIntensity);
				float4 specular = specularIntensitySmooth * _SpecularColor;

				float4 rimDot = 1 - dot(viewDir, normal);
				float rimIntensity = rimDot * pow(NdotL, _RimThreshold);
				rimIntensity = smoothstep(_RimAmount - 0.01, _RimAmount + 0.01, rimIntensity);
				float4 rim = rimIntensity * _RimColor;


				float4 sample = tex2D(_MainTex, i.uv);

				return _Color * sample * (_AmbientColor + light + specular + rim);
			}
			ENDCG
		}

		// ForwardAdd pass: this pass runs once per additional light (point/spot/directional extra lights)
		// It uses additive blending (Blend One One) so each light's contribution is accumulated on top
		// of the ForwardBase pass. Unity sets the current light parameters (position/color) for each
		// ForwardAdd pass; within the pass we can use the same names (`_WorldSpaceLightPos0`,
		// `_LightColor0`) to refer to that light.
		Pass
		{
			Name "FORWARDADD"
			Tags { "LightMode" = "ForwardAdd" }
			Blend One One
			ZWrite Off
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment fragAdd
			#pragma multi_compile_fwdbase
			
			// Reuse the same includes and structs as the base pass. We already have these in scope
			#include "UnityCG.cginc"
			#include "Lighting.cginc"
			#include "AutoLight.cginc"
			
			// Sampler and material uniforms (declare early so vert() can use them)
			sampler2D _MainTex;
			sampler2D _RampTex;
			float4 _MainTex_ST;

			// Redeclare appdata and v2f here because each CGPROGRAM has its own scope.
			struct appdata
			{
				float4 vertex : POSITION;                
				float4 uv : TEXCOORD0;
				float3 normal : NORMAL;
			};

			struct v2f //vertex to fragment
			{
				SHADOW_COORDS(2)
				float4 pos : SV_POSITION;
				float2 uv : TEXCOORD0;
				float3 worldNormal : NORMAL;
				float3 viewDir : TEXCOORD1;
				float3 worldPos : TEXCOORD3; // world-space position, needed for point/spot lights
			};

			float4 _Color;
			float4 _AmbientColor;
			float4 _SpecularColor;
			float _Glossiness;
			float4 _RimColor;
			float _RimAmount;
			float _RimThreshold;

						// Runtime tuning uniforms (must be declared here so fragAdd can access them)
			float _DistanceAttenuation;
			float _MaxLightRange;
			float _SpotSmoothness;
			float _ShadowMultiplier;

			// ramp and forward-add options
			float _RampSteps;
			float _RampSmooth;
			float _ForwardAddUseRamp;
			float _ForwardAddUseSmoothstep;


			// Duplicate vert() so ForwardAdd can run the same vertex transform
			v2f vert (appdata v)
			{
				v2f o;
				o.pos = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				o.worldNormal = UnityObjectToWorldNormal(v.normal);
				o.viewDir = WorldSpaceViewDir(v.vertex);
				o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
				TRANSFER_SHADOW(o)
				return o;
			}
			// The same v2f and appdata are used by the vertex shader. vert() already computes
			// world normal and viewDir; Unity will call this same vert entry point for ForwardAdd.
			
			// Fragment program for additive lights. It replicates the toon lighting logic from
			// the base pass but only outputs the additional light contribution (diffuse + specular + rim).
			float4 fragAdd (v2f i) : SV_Target
			{
				// Normalize inputs
				float3 normal = normalize(i.worldNormal);
				float3 viewDir = normalize(i.viewDir);
				// Handle directional vs positional lights for ForwardAdd as well
				float3 lightDir0 = (_WorldSpaceLightPos0.w == 0) ? normalize(_WorldSpaceLightPos0.xyz) : normalize(_WorldSpaceLightPos0.xyz - i.worldPos);
				float NdotL = dot(lightDir0, normal);
				float shadow = SHADOW_ATTENUATION(i) * _ShadowMultiplier; // support shadowing per-additive-light
				// Distance attenuation
				float distAtten = 1.0;
				if (_MaxLightRange > 0)
				{
					float dist = length((_WorldSpaceLightPos0.xyz - i.worldPos));
					distAtten = saturate(1.0 - dist / _MaxLightRange);
					distAtten = lerp(1.0, distAtten, _DistanceAttenuation);
				}
				float smoothNdot = NdotL;
				if (_SpotSmoothness > 0 && _WorldSpaceLightPos0.w == 1)
				{
					smoothNdot = smoothstep(-_SpotSmoothness, 1.0, NdotL);
				}
				float rawIntensity = smoothNdot * shadow * distAtten;

				// optionally quantize using ramp
				float useVal = rawIntensity;
				if (_ForwardAddUseRamp > 0.5)
				{
					float steps = max(1.0, floor(_RampSteps + 0.5));
					float invSteps = 1.0 / steps;
					float quant = floor(rawIntensity * steps) * invSteps + invSteps * 0.5;
					useVal = lerp(quant, rawIntensity, _RampSmooth);
					float rampSample = tex2D(_RampTex, float2(useVal, 0.5)).r;
					useVal = rawIntensity * rampSample;
				}

				float lightingIntensity;
				if (_ForwardAddUseSmoothstep > 0.5)
				{
					lightingIntensity = smoothstep(0.0, 0.02, useVal);
				}
				else
				{
					lightingIntensity = saturate(useVal);
				}

				float4 light = lightingIntensity * _LightColor0;
				
				// Specular (half vector) using the same toon-friendly smoothing
				float3 halfVec = normalize(_WorldSpaceLightPos0 + viewDir);
				float NdotH = dot(normal, halfVec);
				float specularIntensity = pow(NdotH * lightingIntensity, _Glossiness * _Glossiness);
				float specularIntensitySmooth = smoothstep(0.005, 0.01, specularIntensity);
				float4 specular = specularIntensitySmooth * _SpecularColor;
				
				// Rim effect (view-angle dependent) — keep same parameters as base pass
				float4 rimDot = 1 - dot(viewDir, normal);
				float rimIntensity = rimDot * pow(NdotL, _RimThreshold);
				rimIntensity = smoothstep(_RimAmount - 0.01, _RimAmount + 0.01, rimIntensity);
				float4 rim = rimIntensity * _RimColor;
				
				// Sample texture and return only the light contribution (no ambient)
				float4 sample = tex2D(_MainTex, i.uv);
				float4 add = (_Color * sample) * (light + specular + rim);
				return add;
			}
			ENDCG
		}
		UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"

		// Outline pass: extrude along normals and draw as solid color
		Pass
		{
			Name "OUTLINE"
			Tags { "LightMode" = "Always" }
			Cull Front
			ZWrite On
			ColorMask RGB
			CGPROGRAM
			#pragma vertex vertOutline
			#pragma fragment fragOutline
			#pragma target 3.0

			#include "UnityCG.cginc"

			struct appdata_o
			{
				float4 vertex : POSITION;
				float3 normal : NORMAL;
			};

			struct v2f_o
			{
				float4 pos : SV_POSITION;
			};

			float4 _OutlineColor;
			float _OutlineWidth;

			v2f_o vertOutline(appdata_o v)
			{
				v2f_o o;
				float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
				float3 worldNormal = normalize(mul((float3x3)unity_ObjectToWorld, v.normal));
				float3 pos = worldPos + worldNormal * _OutlineWidth;
				o.pos = mul(UNITY_MATRIX_VP, float4(pos,1));
				return o;
			}

			fixed4 fragOutline(v2f_o i) : SV_Target
			{
				return float4(_OutlineColor.rgb, 1.0);
			}
			ENDCG
		}
	}
}