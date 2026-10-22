#ifndef TAPTAP_ENEMY_ARTWORK_INCLUDED
#define TAPTAP_ENEMY_ARTWORK_INCLUDED
float3 EnemyArtworkColor(float3 color, float artworkGamma)
{
    if (abs(artworkGamma - 1.0) < .0001) return color;
    #ifndef UNITY_COLORSPACE_GAMMA
    color = LinearToGammaSpace(color);
    #endif
    color = pow(max(color, 0.0), float3(artworkGamma, artworkGamma, artworkGamma));
    // Generated frames exaggerate fresh scarlet. Match the reference's muted dried blood.
    float blood = smoothstep(.10, .24, color.r - max(color.g, color.b));
    float3 dried = min(color, float3(.34, .22, .20));
    float gray = dot(dried, float3(.299, .587, .114));
    color = lerp(color, lerp(gray.xxx, dried, .70), blood);
    #ifndef UNITY_COLORSPACE_GAMMA
    color = GammaToLinearSpace(color);
    #endif
    return color;
}
#endif
