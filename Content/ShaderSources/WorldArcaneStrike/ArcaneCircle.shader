Shader "MiliraXian/ArcaneCircle"
{
    Properties
    {
        _MainTex ("Arcane strokes", 2D) = "white" {}
        _Color ("Light", Color) = (1,1,1,1)
        _Reveal ("Construction sweep", Range(0,1)) = 1
        _Outline ("Ground contrast", Range(0,1)) = 0.65
    }
    SubShader
    {
        Tags { "Queue"="Transparent+40" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float4 _Color;
            float _Reveal;
            float _Outline;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; };
            struct v2f { float4 position : SV_POSITION; float2 uv : TEXCOORD0; };
            v2f vert(appdata v)
            {
                v2f o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }
            float4 frag(v2f i) : SV_Target
            {
                clip(_Reveal - 0.0001);
                float2 p = i.uv - 0.5;
                float angle = frac(atan2(p.x, p.y) / 6.2831853 + 1.0);
                float sweep = _Reveal >= 0.9999 ? 1.0 : saturate((_Reveal - angle) * 100.0);
                float mask = tex2D(_MainTex, i.uv).a;
                float2 edge = _MainTex_TexelSize.xy * 2.5;
                float silhouette = max(mask, tex2D(_MainTex, i.uv + float2(edge.x, 0)).a);
                silhouette = max(silhouette, tex2D(_MainTex, i.uv - float2(edge.x, 0)).a);
                silhouette = max(silhouette, tex2D(_MainTex, i.uv + float2(0, edge.y)).a);
                silhouette = max(silhouette, tex2D(_MainTex, i.uv - float2(0, edge.y)).a);
                float ink = saturate(mask * _Color.a * sweep);
                float shadow = saturate((silhouette - mask) * _Outline * _Color.a * sweep);
                float tip = _Reveal < 0.9999 ? saturate(1.0 - abs(_Reveal - angle) * 160.0) : 0;
                float3 color = _Color.rgb * ink * (1 + tip * 0.3);
                color += float3(0.012, 0.018, 0.035) * shadow * (1-ink);
                return float4(color, ink + shadow * (1-ink));
            }
            ENDCG
        }
    }
}
