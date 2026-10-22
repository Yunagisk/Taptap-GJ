using System;

// A short, single advance. Stay at impact briefly, then ease all the way back before idle.
public static class EnemyAttackMotion
{
    public static float Advance(float time, float contact)
    {
        contact = Math.Max(.05f, Math.Min(.90f, contact));
        float holdEnd = contact + Math.Min(.055f, (1f - contact) * .15f);
        return time <= contact ? Ease(contact * .32f, contact, time)
            : 1f - Ease(holdEnd, .96f, time);
    }

    public static float Windup(float time, float contact)
    {
        contact = Math.Max(.05f, Math.Min(.90f, contact));
        return Ease(0, contact * .38f, time) * (1f - Ease(contact * .38f, contact * .90f, time));
    }

    static float Ease(float start, float end, float time)
    {
        float t = Math.Max(0f, Math.Min(1f, (time - start) / (end - start)));
        return t * t * t * (t * (t * 6f - 15f) + 10f);
    }
}
