Shader "UI/Main Menu Artwork"
{
    Properties
    {
        [PerRendererData] _MainTex ("Artwork", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [Enum(Background,0,Cross,1)] _Layer ("Artwork layer", Float) = 0
        _Reveal ("Emergence", Range(0,1)) = 0
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="False" }
        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }
        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"

            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; float4 world : TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO };
            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _Color;
            float4 _ClipRect;
            float _Layer, _Reveal;

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                o.world = v.vertex;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            float hash(float2 p) { return frac(sin(dot(p, float2(127.1,311.7))) * 43758.5453); }
            float noise(float2 p)
            {
                float2 cell = floor(p), f = frac(p);
                f = f*f*(3-2*f);
                return lerp(lerp(hash(cell), hash(cell+float2(1,0)), f.x),
                    lerp(hash(cell+float2(0,1)), hash(cell+1), f.x), f.y);
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 c = tex2D(_MainTex, i.uv);
                if (_Layer < .5)
                {
                    float2 center = (i.uv-float2(.5,.51))*float2(1.777778,1);
                    float spread = length(center) + (noise(i.uv*8)-.5)*.15 + (noise(i.uv*31)-.5)*.035;
                    float emergence = smoothstep(spread-.23, spread+.23, lerp(-.3,1.35,_Reveal));
                    c.a *= emergence * smoothstep(0,.22,_Reveal);
                }
                else
                {
                    // Fade every part of the cross together while its RectTransform rises.
                    c.a *= _Reveal;

                    // Suppress isolated saturated spill on the PNG boundary, preserving the
                    // weathering inside the metal and its ordinary antialiased silhouette.
                    float2 dx = float2(_MainTex_TexelSize.x*2,0);
                    float2 dy = float2(0,_MainTex_TexelSize.y*2);
                    float neighbor = min(min(tex2D(_MainTex,i.uv+dx).a,tex2D(_MainTex,i.uv-dx).a),
                        min(tex2D(_MainTex,i.uv+dy).a,tex2D(_MainTex,i.uv-dy).a));
                    float edge = 1-smoothstep(.05,.9,neighbor);
                    float chroma = max(c.r,max(c.g,c.b))-min(c.r,min(c.g,c.b));
                    c.a *= 1-edge*smoothstep(.15,.45,chroma);
                }
                c *= i.color;
                #ifdef UNITY_UI_CLIP_RECT
                c.a *= UnityGet2DClipping(i.world.xy,_ClipRect);
                #endif
                #ifdef UNITY_UI_ALPHACLIP
                clip(c.a-.001);
                #endif
                return c;
            }
            ENDCG
        }
    }
}
