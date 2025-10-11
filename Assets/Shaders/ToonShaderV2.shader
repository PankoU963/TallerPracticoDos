Shader "Unlit/ToonShaderV2"
{
    Properties
    {
        // Textures
        [NoScaleOffset]_MainTex ("Albedo (RGB) - Main Texture", 2D) = "white" {}
        _RampTex ("Ramp Texture (optional)", 2D) = "white" {}

        // Base color and exposure
        [HDR]_Color ("Base Color (HDR)", Color) = (1,1,1,1)
        _Brightness ("Ambient Brightness", Range(0,1)) = 0.3
        _Strength ("Diffuse Strength", Range(0,1)) = 0.5

        // Toon control
        _Steps ("Toon Steps", Range(1,16)) = 3
        _StepSmooth ("Step Smooth (0=hard,1=smooth)", Range(0,1)) = 0.2

        // Specular
        _SpecularStrength ("Specular Strength", Range(0,1)) = 0.5
        _Glossiness ("Glossiness (Specular Power)", Range(1,128)) = 16

        // Rim / Fresnel
        [HDR]_RimColor ("Rim Color", Color) = (1,1,1,1)
        _RimExponent ("Rim Exponent", Range(0.1,100)) = 2.0
        _RimThreshold ("Rim Threshold", Range(0,1)) = 0.7
        _RimSmooth ("Rim Smooth", Range(0,0.5)) = 0.03
        _RimMix ("Rim Mix (view -> light)", Range(0,1)) = 0.0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 100

        // Pass for Toon shader
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
                half3 worldNormal : NORMAL;
                float3 worldPos : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Brightness;
            float _Strength;
            float4 _Color;
            float _Steps;
            float _StepSmooth;
            float _SpecularStrength;
            float _Glossiness;
            float4 _RimColor;
            float _RimExponent;
            float _RimThreshold;
            float _RimSmooth;
            float _RimMix;
            sampler2D _RampTex;

            // ------------------------------------------------------------------
            // Quantization helper
            // Maps a continuous NdotL in [0..1] to discrete bands in [0..1].
            // steps: number of toon levels (>=1).
            // Returns a value in [0..1] representing the band level.
            // ------------------------------------------------------------------
            float ToonQuant(float NdotL, float steps)
            {
                // Ensure at least 1 step to avoid NaNs.
                steps = max(1.0, steps);

                // Round steps to integer for stable behavior when the material slider
                // is changed in the Inspector.
                float s = round(steps);
                if (s <= 1.0)
                {
                    // With a single step everything is treated as fully lit.
                    return 1.0;
                }

                // Clamp input and quantize into s discrete levels. We divide by (s-1)
                // so the top level reaches 1.0.
                float v = saturate(NdotL);
                float q = floor(v * s) / (s - 1.0);
                return saturate(q);
            }

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
                // sample the texture
                fixed4 col = tex2D(_MainTex, i.uv);
                // Compute normal and view/light directions
                float3 normal = normalize(i.worldNormal);
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);

                // Light direction: supports directional and positional lights
                float3 lightDir = (_WorldSpaceLightPos0.w == 0) ? normalize(_WorldSpaceLightPos0.xyz) : normalize(_WorldSpaceLightPos0.xyz - i.worldPos);
                float NdotL = saturate(dot(normal, lightDir));

                // Toon diffuse: use ramp texture if provided, otherwise quantize with steps
                float toonDiffuse = 0.0;
                // Check if ramp texture is not white (user can supply a ramp). We always sample it; artists can set to white.
                float rampSample = tex2D(_RampTex, float2(NdotL, 0.5)).r;
                // If ramp texture is default white (1.0) the quantization will be used instead
                if (rampSample < 0.999)
                {
                    toonDiffuse = rampSample;
                }
                else
                {
                    float quant = ToonQuant(NdotL, _Steps);
                    // Blend between the quantized value and the continuous NdotL by _StepSmooth
                    toonDiffuse = lerp(quant, NdotL, saturate(1.0 - _StepSmooth));
                }

                // small smoothing of final toon value to avoid hard aliasing
                toonDiffuse = smoothstep(0.0, 0.02 + _StepSmooth * 0.1, toonDiffuse);


                // Specular (half-vector) approximation — use _Glossiness (separate from rim)
                float3 halfVec = normalize(lightDir + viewDir);
                float NdotH = saturate(dot(normal, halfVec));
                float specPow = pow(NdotH, _Glossiness);
                float specSmooth = smoothstep(0.0, 0.2, specPow);

                // Rim term (view-based fresnel affecting silhouette borders)
                float viewDot = saturate(dot(normal, viewDir));
                float fresnel = 1.0 - viewDot; // 0 at facing, 1 at grazing
                // Mask the fresnel so rim only appears near the silhouette edges (view-based)
                float rimMask = smoothstep(_RimThreshold - _RimSmooth, _RimThreshold + _RimSmooth, fresnel);
                float rim_view = pow(rimMask, _RimExponent);
                // Light-based rim (depends on NdotL, independent of view)
                float rim_light = pow(saturate(1.0 - NdotL), _RimExponent);
                // Mix between view-based and light-based rim
                float rim = lerp(rim_view, rim_light, saturate(_RimMix));
                fixed3 rimCol = _RimColor.rgb * rim;

                // Compose final color
                fixed3 baseCol = col.rgb * _Color.rgb;
                fixed3 ambient = _Brightness * baseCol;
                fixed3 diffuse = toonDiffuse * baseCol * _Strength;
                fixed3 specular = specSmooth * _SpecularStrength * _Color.rgb;

                fixed3 finalColor = ambient + diffuse + specular + rimCol;
                return fixed4(finalColor, col.a);
            }
            ENDCG
        }

        // Pass for Casting Shadows
        Pass
        {
            Name "CastShadow"
            Tags { "LightMode" = "ShadowCaster" }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_shadowcaster
            #include "UnityCG.cginc"

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
}