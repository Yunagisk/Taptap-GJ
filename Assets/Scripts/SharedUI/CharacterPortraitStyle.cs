using UnityEngine;
using UnityEngine.UI;

// Shared by dialogue, event browsers and their prefab builders.
public static class CharacterPortraitStyle
{
    public static void Apply(Image target, Sprite sprite, bool isProtagonist = false)
    {
        if (target == null) return;
        if (target.sprite != sprite) target.sprite = sprite;
        // Multiplication by black removes the painted detail while preserving alpha.
        target.color = isProtagonist ? Color.white : Color.black;
    }
}
