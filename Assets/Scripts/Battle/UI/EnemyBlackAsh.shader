Shader "Taptap/Enemy Black Ash"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
        _Progress ("Disintegration", Range(0,1)) = 0
        _AshPass ("Dust Geometry", Float) = 0
        _Grid ("Grain Grid", Vector) = (120,120,0,0)
        _SpriteUVRect ("Sprite UV Rect", Vector) = (0,0,1,1)
        _LocalSize ("Local Size", Vector) = (1,1,0,0)
        _Pivot ("Normalized Pivot", Vector) = (0.5,0.5,0,0)
        _Opacity ("Opacity", Float) = 1
        _ArtworkGamma ("Reference Artwork Tone", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" "PreviewType"="Plane" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"
            #include "EnemyArtwork.cginc"

            float _Progress, _AshPass, _Opacity;
            float _ArtworkGamma;
            float4 _Grid, _SpriteUVRect, _LocalSize, _Pivot;
            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
                float2 center : TEXCOORD1;
                float2 origin : TEXCOORD2;
            };
            struct AshVaryings
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float2 spriteUV : TEXCOORD1;
                float opacity : TEXCOORD2;
                float2 grainUV : TEXCOORD3;
                fixed4 color : COLOR;
            };

            float Hash(float2 cell)
            {
                return frac(sin(dot(cell, float2(127.1, 311.7))) * 43758.5453);
            }
            float Release(float2 cell)
            {
                return .16 + .42 * (.6 * Hash(cell) + .4 * ((cell.x + .5) / _Grid.x));
            }

            AshVaryings vert(appdata input)
            {
                AshVaryings output;
                output.uv = input.uv;
                output.spriteUV = (input.uv - _SpriteUVRect.xy) / _SpriteUVRect.zw;
                output.color = input.color * _Color * _RendererColor;
                output.opacity = 1;
                output.grainUV = 0;
                float4 position = input.vertex;
                if (_AshPass > .5)
                {
                    float2 cell = floor(input.center * _Grid.xy);
                    float random = Hash(cell);
                    float lifetime = .34 + .10 * Hash(cell + 53.0);
                    float age = (_Progress - Release(cell)) / lifetime;
                    float t = saturate(age);
                    float2 center = input.origin;
                    output.grainUV = (output.spriteUV - input.center) * _Grid.xy + .5;
                    float angle = t * (random - .5) * 5.0;
                    float s = sin(angle), c = cos(angle);
                    float2 corner = (position.xy - center) * lerp(.45 + random * .30, .06, t);
                    corner = float2(corner.x * c - corner.y * s, corner.x * s + corner.y * c);
                    float2 wind = float2(.045 + .12 * random, .11 + .10 * Hash(cell + 19.0)) * _LocalSize.xy * t;
                    float phase = random * 6.2831853;
                    float2 curl = float2(sin(phase + t * 6.0) - sin(phase), cos(phase + t * 4.0) - cos(phase));
                    position.xy = center + corner + wind + curl * _LocalSize.xy * .013 * t;
                    output.opacity = step(0.0, age) * (1.0 - smoothstep(.35, 1.0, t)) * _Opacity;
                    output.color = 1;
                }
                else position = UnityFlipSprite(position.xyz, _Flip);
                output.vertex = UnityObjectToClipPos(position);
                return output;
            }

            fixed4 frag(AshVaryings input) : SV_Target
            {
                fixed4 texel = SampleSpriteTexture(input.uv);
                float3 soot = float3(.004, .004, .005);
                if (_AshPass > .5)
                {
                    float2 delta = (input.grainUV - .5) * 2.0;
                    float ragged = sin(input.grainUV.x * 13.0) * sin(input.grainUV.y * 11.0) * .12;
                    float grain = 1.0 - smoothstep(.55, .98, length(delta) + ragged);
                    float alpha = texel.a * input.opacity * grain;
                    return fixed4(soot * alpha, alpha);
                }
                float2 cell = floor(saturate(input.spriteUV) * _Grid.xy);
                texel.rgb = EnemyArtworkColor(texel.rgb, _ArtworkGamma);
                float keep = 1.0 - smoothstep(Release(cell) - .012, Release(cell) + .012, _Progress);
                float alpha = texel.a * input.color.a * keep;
                float3 color = lerp(texel.rgb * input.color.rgb, soot, smoothstep(0.0, .18, _Progress));
                return fixed4(color * alpha, alpha);
            }
            ENDCG
        }
    }
}
