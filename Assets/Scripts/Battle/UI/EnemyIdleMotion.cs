using System;

// Deterministic deformation of one drawing; coordinates are normalized, top-down.
public static class EnemyIdleMotion
{
    public struct Point
    {
        public readonly float x, y;
        public Point(float x, float y) { this.x = x; this.y = y; }
    }

    public static Point Sample(float x, float y, float seconds, float cycleSeconds)
    {
        double clock = Math.Floor(Math.Max(0f, seconds) * 30.0 + .00001) / 30.0;
        double phase = clock * Math.PI * 2.0 / Math.Max(.1f, cycleSeconds);
        float breath = (float)Math.Sin(phase);
        float upper = 1f - Smooth(.56f, .65f, y);
        float tail = Smooth(.58f, .74f, x) * Smooth(.35f, .48f, y)
            * (1f - Smooth(.82f, .9f, y));
        float feathers = (float)Math.Sin(phase - .45);
        return new Point(x + .001f * breath * upper + .003f * feathers * tail,
            y - .004f * breath * upper + .002f * feathers * tail);
    }

    static float Smooth(float a, float b, float p)
    {
        float t = Math.Max(0f, Math.Min(1f, (p - a) / (b - a)));
        return t * t * (3f - 2f * t);
    }
}
