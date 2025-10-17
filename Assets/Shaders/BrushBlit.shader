Shader "Hidden/BrushBlit"
{
    Properties
    {
        _MainTex ("MainTex", 2D) = "white" {}
        _BrushTex ("Brush", 2D) = "white" {}
        _BrushCenter ("BrushCenter", Vector) = (0.5,0.5,0,0)
        _BrushSize ("BrushSize", Float) = 0.05
        _BrushStrength ("BrushStrength", Float) = 0.1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Pass
        {
            ZTest Always Cull Off ZWrite Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _BrushTex;
            float4 _BrushCenter;
            float _BrushSize;
            float _BrushStrength;

            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float2 uv : TEXCOORD0; float4 vertex : SV_POSITION; };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float baseVal = tex2D(_MainTex, i.uv).r;
                float2 local = (i.uv - _BrushCenter.xy) / _BrushSize + 0.5;
                float brush = tex2D(_BrushTex, local).r;
                // clamp to 0..1: if outside, brush=0
                brush *= step(0.0, local.x) * step(local.x, 1.0) * step(0.0, local.y) * step(local.y, 1.0);
                float outVal = saturate(baseVal + brush * _BrushStrength);
                return float4(outVal, outVal, outVal, 1);
            }
            ENDCG
        }
    }
}
