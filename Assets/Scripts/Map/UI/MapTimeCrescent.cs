using UnityEngine;

// Difference of two equal circles: a symmetric, standard upright crescent.
public static class MapTimeCrescent
{
    public const int Steps = 64;
    public const float CutOffset = 0.4f;
    public const float Orbit = 0.59f;
    public const float Size = 0.13f;

    public static Vector2 Outer(float t)
    {
        float tip = Mathf.Acos(CutOffset * 0.5f);
        float angle = tip + (Mathf.PI * 2 - tip * 2) * t;
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
    }

    public static Vector2 Inner(float t)
    {
        float tip = Mathf.Acos(CutOffset * 0.5f);
        float angle = Mathf.PI - tip + tip * 2 * t;
        return new Vector2(CutOffset + Mathf.Cos(angle), Mathf.Sin(angle));
    }

    public static Vector2 Rotate(Vector2 point, float degrees)
    {
        float a = degrees * Mathf.Deg2Rad, c = Mathf.Cos(a), s = Mathf.Sin(a);
        return new Vector2(point.x * c - point.y * s, point.x * s + point.y * c);
    }
}
