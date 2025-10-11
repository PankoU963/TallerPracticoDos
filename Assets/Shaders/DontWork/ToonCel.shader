Shader "Custom/ToonCel"
{
    Properties
    {
        _Color ("Base Color", Color) = (1,1,1,1)
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
        _RampTex ("Ramp (RGB)", 2D) = "white" {}
        [HDR]
        _RimColor ("Rim Color", Color) = (1,1,1,1)
        _RimPower ("Rim Power", Range(0.1,8)) = 3
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _OutlineWidth ("Outline Width", Range(0,0.1)) = 0.01
        _Glossiness ("Smoothness", Range(0,1)) = 0.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _RampTex;

        struct Input
        {
            float2 uv_MainTex;
            float3 viewDir;
            float3 worldNormal;
        };

        fixed4 _Color;
        half _Glossiness;
        fixed4 _RimColor;
        half _RimPower;

        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 albedo = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            // compute N dot L via Standard lighting, but do ramp using N dot V (approx)
            float ndotv = saturate(dot(normalize(IN.worldNormal), normalize(IN.viewDir)));

            // Sample ramp with ndotv as x coordinate (use 1D ramp stored horizontally)
            fixed ramp = tex2D(_RampTex, float2(ndotv, 0.5)).r;

            // Apply ramp to albedo
            o.Albedo = albedo.rgb * ramp;
            o.Metallic = 0;
            o.Smoothness = _Glossiness;
            o.Alpha = albedo.a;

            // Rim light (view-space fresnel-like)
            float rim = pow(1.0 - ndotv, _RimPower);
            o.Emission = _RimColor.rgb * rim;
        }
        ENDCG

        // Outline pass: render backfaces scaled along normals
        Pass
        {
            Name "OUTLINE"
            Tags { "LightMode" = "Always" }
            Cull Front
            ZWrite On
            ColorMask RGB

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
            };

            float4 _OutlineColor;
            float _OutlineWidth;

            v2f vert (appdata v)
            {
                v2f o;
                // transform normal and position to world space
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 worldNormal = normalize(mul((float3x3)unity_ObjectToWorld, v.normal));
                float3 pos = worldPos + worldNormal * _OutlineWidth;
                o.pos = mul(UNITY_MATRIX_VP, float4(pos, 1.0));
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return float4(_OutlineColor.rgb, 1.0);
            }
            ENDCG
        }

    }

    FallBack "Diffuse"
}
