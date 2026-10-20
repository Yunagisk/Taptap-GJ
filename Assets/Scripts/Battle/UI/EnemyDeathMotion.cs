using System;

// A one-way collapse in normalized artwork bounds. No alternating poses or bounce.
public static class EnemyDeathMotion
{
    public struct Point
    {
        public readonly float x, y;
        public Point(float x, float y) { this.x = x; this.y = y; }
    }

    public static Point Sample(float x, float y, float progress)
    {
        float body = Smooth(0f, 1f, progress);
        float head = Smooth(0f, .85f, progress);
        float legs = Smooth(.04f, .74f, progress);
        float headWeight = (1f - Smooth(.22f, .58f, x)) * Smooth(.28f, .64f, y);
        float legWeight = Smooth(.12f, .27f, x) * (1f - Smooth(.62f, .8f, x))
            * (1f - Smooth(.22f, .4f, y));
        float bodyY = y + (Math.Max(0f, y * .58f - .18f) - y) * body;
        bodyY += (y * (1f - .8f * legs) - bodyY) * legWeight;
        float foldedX = x + (.45f - x) * .42f * legWeight * legs;
        foldedX += .018f * Smooth(.72f, 1f, x) * body;
        // Rotate the head about the neck instead of flattening the beak and crest.
        float angle = -.8726646f * head, scale = 1f - .2f * head;
        float sin = (float)Math.Sin(angle), cos = (float)Math.Cos(angle);
        float dx = x - .30f, dy = y - .55f;
        float headX = .30f - .04f * head + (dx * cos - dy * sin) * scale;
        float headY = Math.Max(0f, .55f - .65f * head + (dx * sin + dy * cos) * scale);
        return new Point(foldedX + (headX - foldedX) * headWeight,
            bodyY + (headY - bodyY) * headWeight);
    }

    public static float Smooth(float min, float max, float value)
    {
        float t = Math.Max(0f, Math.Min(1f, (value - min) / (max - min)));
        return t * t * (3f - 2f * t);
    }
}
