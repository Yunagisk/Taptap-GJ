Shader "Taptap/Enemy Artwork"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _ArtworkGamma ("Reference Artwork Tone", Float) = 1
        [HideInInspector] _RendererColor ("Renderer Color", Color) = (1,1,1,1)
        [HideInInspector] _Flip ("Flip", Vector) = (1,1,1,1)
        [HideInInspector] _AlphaTex ("External Alpha", 2D) = "white" {}
        [HideInInspector] _EnableExternalAlpha ("Enable External Alpha", Float) = 0
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
            #pragma vertex SpriteVert
            #pragma fragment ArtworkFrag
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            #include "UnitySprites.cginc"
            #include "EnemyArtwork.cginc"
            float _ArtworkGamma;
            fixed4 ArtworkFrag(v2f input) : SV_Target
            {
                fixed4 texel = SampleSpriteTexture(input.texcoord);
                texel.rgb = EnemyArtworkColor(texel.rgb, _ArtworkGamma);
                fixed4 color = texel * input.color;
                color.rgb *= color.a;
                return color;
            }
            ENDCG
        }
    }
}
