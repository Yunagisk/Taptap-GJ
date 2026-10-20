Shader "Taptap/Battle Actor Outline"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _ArtworkGamma ("Artwork Tone", Float) = 1
        _OutlineColor ("Outline Color", Color) = (.76,.70,.59,.9)
        _OutlineWidth ("Outline Width In Pixels", Range(0,4)) = 1.6
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "CanUseSpriteAtlas"="True" }
        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment OutlineFrag
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"
            #include "EnemyArtwork.cginc"
            float _ArtworkGamma;
            float _OutlineWidth;
            fixed4 _OutlineColor;

            fixed4 OutlineFrag(v2f input) : SV_Target
            {
                float2 uv = input.texcoord;
                float2 dx = ddx(uv) * _OutlineWidth;
                float2 dy = ddy(uv) * _OutlineWidth;
                fixed4 original = SampleSpriteTexture(uv);
                float outside = max(SampleSpriteTexture(uv + dx).a, SampleSpriteTexture(uv - dx).a);
                outside = max(outside, max(SampleSpriteTexture(uv + dy).a, SampleSpriteTexture(uv - dy).a));
                outside = max(outside, max(SampleSpriteTexture(uv + (dx + dy) * .7071).a,
                    SampleSpriteTexture(uv - (dx + dy) * .7071).a));
                outside = max(outside, max(SampleSpriteTexture(uv + (dx - dy) * .7071).a,
                    SampleSpriteTexture(uv - (dx - dy) * .7071).a));
                float edge = max(0, outside - original.a) * _OutlineColor.a * input.color.a;
                original.rgb = EnemyArtworkColor(original.rgb, _ArtworkGamma);
                fixed4 painted = original * input.color;
                painted.rgb *= painted.a;
                return fixed4(painted.rgb + _OutlineColor.rgb * edge, painted.a + edge);
            }
            ENDCG
        }
    }
}
