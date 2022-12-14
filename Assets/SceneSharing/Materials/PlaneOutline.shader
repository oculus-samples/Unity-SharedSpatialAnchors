/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * Licensed under the Oculus SDK License Agreement (the "License");
 * you may not use the Oculus SDK except in compliance with the License,
 * which is provided at the time of installation or download, or which
 * otherwise accompanies this software in either electronic or hard copy form.
 *
 * You may obtain a copy of the License at
 *
 * https://developer.oculus.com/licenses/oculussdk/
 *
 * Unless required by applicable law or agreed to in writing, the Oculus SDK
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

Shader "SSA/PlaneOutline" {
    Properties
    {
        _Thickness("Thickness", float) = 1
        _Color("Color", Color) = (1,1,1,1)
        _MainTex("Texture", 2D) = "white" {}
        _EdgeColor("Edge Color", Color) = (1,1,1,1)
        _EffectPosition("Effect Position", Vector) = (0,1000,0,1)
        _EffectRadius("Effect Radius", float) = 1
        _EffectIntensity("Effect Intensity", float) = 1
        _EdgeTimeline("Edge Anim Timeline", float) = 1
        _CeilingHeight("CeilingHeight", float) = 1
        [IntRange] _StencilRef("Stencil Reference Value", Range(0, 255)) = 0
    }
        SubShader
    {
        Stencil{
                Ref[_StencilRef]
                Comp NotEqual
        }
        Tags { "Queue" = "Transparent" }
        LOD 100
        BlendOp Add, Max
        Blend One Zero, One One

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                //float4 color : TEXCOORD1;
                //float4 vertWorld : TEXCOORD2;
                //UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            float4 _EdgeColor;
            float4 _EffectPosition;
            float _EffectRadius;
            float _EffectIntensity;
            float _EdgeTimeline;
            float _CeilingHeight;
            sampler2D _MainTex;
            uniform half4 _Color;
            float _Thickness;

            v2f vert(appdata v)
            {
                v2f o;
                float expand = 1.1f;
                v.vertex.xyz *= expand;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = (v.uv - 0.5f) * expand + 0.5f;
                return o;
            }

            fixed4 frag(v2f i) : COLOR
            {
                // distance from the center of the quad.
                float2 fromCenter = abs(i.uv - 0.5f);
                // Signed distance from the horizontal & vertical edges.
                float2 fromEdge = fromCenter - 0.5f;

                // Use screenspace derivatives to convert to pixel distances.
                fromEdge.x /= length(float2(ddx(i.uv.x), ddy(i.uv.x)));
                fromEdge.y /= length(float2(ddx(i.uv.y), ddy(i.uv.y)));

                // Compute a nicely rounded distance from the edge.
                float distance = abs(min(max(fromEdge.x,fromEdge.y), 0.0f) + length(max(fromEdge, 0.0f)));

                // Sample our texture for the interior.
                fixed4 col = tex2D(_MainTex, i.uv) * _EdgeColor;
                // Clip out the part of the texture outside our original 0...1 UV space.
                col.a *= step(max(fromCenter.x, fromCenter.y), 0.5f);

                // Blend in our outline within a controllable thickness of the edge.
                col = lerp(col, _Color, saturate(_Thickness - distance));
                col.a = 0;

                return col;
            }
            ENDCG
        }
    }
}
