Shader "MiliraXian/ArcaneEnergy"
{
    Properties
    {
        _MainTex ("Energy mask", 2D) = "white" {}
        _Color ("Energy", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "Queue"="Transparent+600" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off
        ZWrite Off
        ZTest LEqual
        Blend One One
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            sampler2D _MainTex;
            float4 _Color;
            struct appdata { float4 vertex : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct v2f { float4 position : SV_POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            v2f vert(appdata v)
            {
                v2f o;
                o.position = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }
            float4 frag(v2f i) : SV_Target
            {
                float mask = tex2D(_MainTex, i.uv).a * i.color.a;
                return float4(i.color.rgb * mask, mask);
            }
            ENDCG
        }
    }
}
