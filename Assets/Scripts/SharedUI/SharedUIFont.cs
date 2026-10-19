using System.Collections.Generic;
using TMPro;
using UnityEngine;

// TMP assets already contain the authored Chinese glyph atlas. Keep those assets
// shared across screens instead of discarding it and rasterizing a new atlas.
public static class SharedUIFont
{
    static readonly HashSet<TMP_FontAsset> retained = new HashSet<TMP_FontAsset>();

    public static TMP_FontAsset Get(TMP_FontAsset font)
    {
        if (font != null) retained.Add(font);
        return font;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetSession() => retained.Clear();
}
