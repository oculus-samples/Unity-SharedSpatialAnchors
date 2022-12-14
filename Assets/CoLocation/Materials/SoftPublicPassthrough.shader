Shader "MR/SoftPublicPassthrough"
{
    Properties
    {
        _Alpha ("Alpha", Range(0, 1)) = 1
        _Darken ("Darken", Range(0, 1)) = 0
        _Feather ("Feather", Range(0, 2)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Overlay" }
        LOD 100

		Blend SrcAlpha OneMinusSrcAlpha
        BlendOp RevSub
        ZTest Always
        ZWrite On
        Stencil {
            Ref 2
            Comp NotEqual
            Pass Zero
        }

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
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            float4 _MainTex_ST;
            float _Alpha;
            float _Darken;
            float _Feather;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 feather = float2(_Feather, _Feather);
                float2 uvD = max(float2(0, 0), (abs(float2(1, 1) - i.uv * 2) - (1 - feather)) / feather);
                float finalAlpha = _Alpha * saturate(1 - (pow(uvD.x, 4) + pow(uvD.y, 6)));
                float finalDarken = _Darken * finalAlpha;
                return fixed4(finalDarken, finalDarken, finalDarken, finalAlpha);
            }
            ENDCG
        }
    }
}