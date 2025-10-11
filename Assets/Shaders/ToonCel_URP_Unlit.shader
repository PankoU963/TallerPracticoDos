Shader "Custom/ToonCel_BuiltIn"
{
    Properties
    {
        _Color ("Base Color", Color) = (1,1,1,1)
        [NoScaleOffset]_MainTex ("Albedo (RGB)", 2D) = "white" {}
        _RampTex ("Ramp (RGB)", 2D) = "white" {}
        [HDR]_RimColor ("Rim Color", Color) = (1,1,1,1)
        _RimPower ("Rim Power", Range(0.1,8)) = 3
        _Glossiness ("Smoothness", Range(0,1)) = 0.0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 200

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 worldNormal : NORMAL;
                float3 worldPos : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            sampler2D _RampTex;
            float4 _Color;
            float4 _RimColor;
            float _RimPower;
            float _Glossiness;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 n = normalize(i.worldNormal);
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);

                float ndotv = saturate(dot(n, normalize(viewDir)));

                fixed4 albedo = tex2D(_MainTex, i.uv) * _Color;
                float ramp = tex2D(_RampTex, float2(ndotv, 0.5)).r;

                float3 shaded = albedo.rgb * ramp;

                float rim = pow(1.0 - ndotv, _RimPower);
                float3 emission = _RimColor.rgb * rim;

                float glossFactor = saturate(_Glossiness);
                float3 outCol = lerp(shaded, albedo.rgb, glossFactor) + emission;

                // Support built-in main light (directional and point fallback)
                float3 lightDir = (_WorldSpaceLightPos0.w == 0) ? normalize(_WorldSpaceLightPos0.xyz) : normalize(_WorldSpaceLightPos0.xyz - i.worldPos);
                float diff = saturate(dot(n, lightDir));
                outCol *= diff;

                return half4(outCol, albedo.a);
            }

            ENDCG
        }

        // Shadow caster pass (compatible with built-in pipeline)
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster
            #include "UnityCG.cginc"

            struct appdata_base { float4 vertex : POSITION; };

            struct v2f_shadow
            {
                V2F_SHADOW_CASTER;
            };

            v2f_shadow vert(appdata_base v)
            {
                v2f_shadow o;
                TRANSFER_SHADOW_CASTER(o);
                return o;
            }

            float4 frag(v2f_shadow i) : COLOR
            {
                SHADOW_CASTER_FRAGMENT(i);
            }
            ENDCG
        }
    }

    FallBack "Unlit/Color"
}
