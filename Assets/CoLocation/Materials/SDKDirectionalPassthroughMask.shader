Shader "Unlit/MRPlatform/DirectionalPassthroughMask"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Alpha ("Alpha", Range(0, 1)) = 1
        _Darken ("Darken", Range(0, 1)) = 0

        [Header(MASK)]
        _HeadPos ("Head Pos", Vector) = (0, 0, 0, 1)
        _MaskDirection ("Mask Direction", Vector) = (1, 0, 0, 1)
        _MaskActivationRadians ("Mask Activation in Radians", Float) = 6.29
        _MaskFeatherRadians ("Mask Feather in Radians", Float) = 0.3

        _ShieldDirection ("Shield Direction", Vector) = (1, 0, 0, 1)
        _ShieldActivationRadians ("Shield Activation in Radians", Float) = 3
        _ShieldFeatherRadians ("Shield Feather in Radians", Float) = 2.5

        _FloorDirection ("Floor Direction", Vector) = (0, -1, 0, 1)
        _FloorActivationRadians ("Floor Activation in Radians", Float) = 1.6
        _FloorFeatherRadians ("Floor Feather in Radians", Float) = 1.3

        _YHeight("Y Height", Float) = 0
        _YPercent("Y Percent", Float) = 0
        _ForcePassthrough("Force Passthrough", Float) = 0
        _FlatNoPassthroughContribution("Flat No Passthrough Contribution", Float) = 0
        _FlatPassthroughContribution("Flat Passthrough Contribution", Float) = 0
        _ActivationDepthStrength("Activation Depth Strength", Float) = 0
        _ActivationDepthLucidContribution("Activation Depth Lucid Contribution", Float) = 0
        _FeatherMidpoint("Feather Midpoint", Float) = 0.25
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Overlay" }
        LOD 100

        Blend SrcAlpha OneMinusSrcAlpha, One One
        BlendOp Add, RevSub
        ZTest Always
        ZWrite Off

        Cull Front

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
                float4 posWorld : TEXCOORD2;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float _Alpha;
            float _Darken;

            uniform float4 _HeadPos;
            uniform float4 _MaskDirection;
            uniform float _MaskActivationRadians;
            uniform float _MaskFeatherRadians;

            uniform float _YHeight;
            uniform float _YPercent;

            uniform float _ForcePassthrough;

            uniform float _FlatNoPassthroughContribution;
            uniform float _FlatPassthroughContribution;
            uniform float _ActivationDepthStrength;
            uniform float _ActivationDepthLucidContribution;
            uniform float _FeatherMidpoint;

            uniform float _ShieldActivationRadians;
            uniform float _ShieldFeatherRadians;
            uniform float4 _ShieldDirection;

            uniform float _FloorActivationRadians;
            uniform float _FloorFeatherRadians;
            uniform float4 _FloorDirection;

            float Refeather( float value , float featherSize, float scale ) {
            //  ████▓▓▓▓▒▒▒▒░░░░    ▐ this gradient from 0 to 1 has a feather size of 1, it spans the entire scale
            //  ██▓▓▒▒░░            ▐ the result of multiplying by two ( divide by feather distance of 0.5 )
            //  ██████▓▓▒▒░░        ▐ center the gradient - the result of adding 0.25 = (1 - 0.5) / 2
	            // return 0.5 * (scale - featherSize) + (value / featherSize);
	            return _FeatherMidpoint * (scale - featherSize) + (value / featherSize);
            }

            // we want this value to go from 0-PI w/ a feather, meaning we have to increase the value
            float RadialFeather( float activationAngle , float pixelAngle, float featherSize, float scale ) {
              //float featherMidpointMultiplier = _FeatherMidpoint * lerp(pow(activationAngle / scale, 1), 3, 1);
              float feather = (activationAngle * (1 + featherSize / 3.1415926) - pixelAngle) / featherSize;
              //feather *= _FeatherMidpoint * lerp(pow(saturate(activationAngle / scale), .5), 0.5, 5);
              return pow(saturate(feather), 2);
              //return _FeatherMidpoint * (scale - featherSize) + (value / featherSize);
            }

            float Remap(float value, float inputFrom, float inputTo, float outputFrom, float outputTo)
            {
                return (value - inputFrom) / (inputTo - inputFrom) * (outputTo - outputFrom) + outputFrom;
            }

            float Remap01(float value, float inputFrom, float inputTo)
            {
                return Remap(value, inputFrom, inputTo, 0, 1);
            }

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                o.posWorld = v.vertex;

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // sample the texture
                fixed4 col = tex2D(_MainTex, i.uv);

                // Add mask
                float3 headToPixelVec = normalize(i.posWorld.xyz - _HeadPos.xyz);
                float3 headToMaskCenter = normalize(_MaskDirection.xyz);
                float headRad = acos(dot(headToMaskCenter, headToPixelVec));

                float activation = saturate(RadialFeather(_MaskActivationRadians, headRad, _MaskFeatherRadians, 3.1415926));
                //float activation = saturate(max(0, (_MaskActivationRadians - headRad) / _MaskFeatherRadians));
                //lucidColor.a *= saturate(flashlightHead);

                // y-fade
                activation = max(activation, saturate(((_YPercent * 1.5) - Remap01(i.posWorld.y, _YHeight, -_YHeight)) / 0.5)) ;

                // shield passthrough
                float3 headToShieldRad = acos(dot(normalize(_ShieldDirection.xyz), headToPixelVec));
                float passthroughActivation = max(activation, saturate(RadialFeather(_ShieldActivationRadians, headToShieldRad, _ShieldFeatherRadians, 3.1415926)));

                // floor passthrough
                float3 headToFloorRad = acos(dot((_FloorDirection.xyz), headToPixelVec));
                passthroughActivation = max(passthroughActivation, saturate(RadialFeather(_FloorActivationRadians, headToFloorRad, _FloorFeatherRadians, 3.1415926)));

                float passthroughPercent = passthroughActivation;//sin(passthroughActivation * (3.1415926 / 2));

                // flat passthrough
                passthroughPercent = lerp(passthroughPercent, 0, _FlatNoPassthroughContribution);
                passthroughPercent = lerp(passthroughPercent, 1, _FlatPassthroughContribution);

                float globalAlpha = lerp(_Alpha, 1, _ForcePassthrough + _ActivationDepthStrength);
                float c = 1 - _Darken;
                return fixed4(c, c, c, globalAlpha) * passthroughPercent;
            }
            ENDCG
        }
    }
}