Shader "Oculus/VirtualKeyboard/VirtualKeyboardCursor"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Cursor Color", Color) = (0.1921568, 0.4862745, 0.9490196, 1)
        _InnerRadius ("Inner Radius", Range(0, 1)) = 0.4
        _OuterRadius ("Outer Radius", Range(0, 1)) = 0.7
        _fadeOff ("Fade Off", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags
		{
			"Queue" = "Transparent"
			"IgnoreProjector" = "True"
			"RenderType" = "Opaque"
		}

		Cull Off
        ZWrite On
        Blend SrcAlpha OneMinusSrcAlpha
        AlphaTest Greater 0

        Blend SrcAlpha OneMinusSrcAlpha // Traditional transparency

        Pass
        {
            AlphaTest Greater 0.5

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

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float _InnerRadius;
            float _OuterRadius;
            float _fadeOff;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                const float4 transparentColor = float4(_Color.x, _Color.y, _Color.z, 0);
                const float edgeSmoothDistance = 0.005f;

                const fixed2 pixelPoint = fixed2(i.uv.x, i.uv.y);
                const float xDist = pow(pixelPoint.x - 0.5, 2);
                const float yDist = pow(pixelPoint.y - 0.5, 2);
                const float dist = sqrt(xDist + yDist) / 0.5;


                // Hard transparency
                if (dist < _InnerRadius) {
                    return transparentColor;
                }
                if (dist > _OuterRadius) {
                    return transparentColor;
                }

                float weight = 0;

                // handle fade
                if (_fadeOff > 0) {
                    weight = (dist) / (1 - (1 - _OuterRadius));
                    // apply fade scale
                    weight = clamp(weight - (1 - _fadeOff), 0, 1);
                }

                // edge smoothing
                if (_InnerRadius > edgeSmoothDistance && dist - _InnerRadius < edgeSmoothDistance) {
                    weight = lerp(weight, 1, 1 - ((dist - _InnerRadius) / edgeSmoothDistance));
                } else if (dist >= _OuterRadius - edgeSmoothDistance && dist < _OuterRadius) {
                    weight = lerp(1, weight, ((_OuterRadius - dist) / edgeSmoothDistance));
                }

                // Color
                fixed4 col = lerp(_Color, transparentColor, weight);



                return col;
            }
            ENDCG
        }
    }
}
