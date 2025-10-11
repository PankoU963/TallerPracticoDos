Shader "Hidden/OutlineSimple"
{
    Properties
    {
        [HDR]
        _OutlineColor ("Outline Color", Color) = (0,0,0,1)
        _OutlineWidth ("Outline Width", Range(0,0.5)) = 0.02
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        Cull Front
        ZWrite On
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

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

            v2f vert(appdata v)
            {
                v2f o;
                // Extrude along object-space normal
                float3 offset = normalize(v.normal) * _OutlineWidth;
                float4 extruded = v.vertex + float4(offset, 0.0);
                o.pos = UnityObjectToClipPos(extruded);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return _OutlineColor;
            }
            ENDCG
        }
    }
}
