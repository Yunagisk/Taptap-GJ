using System;

public enum EnemyArtworkProfile { Crow, Rat, Wraith, Sloth }
public enum EnemyArtworkPose { Idle, Attack, Hurt, Death }

// Coordinates cover the painted silhouette, bottom-up. One texture keeps ink and palette stable.
public static class EnemyArtworkMotion
{
    public readonly struct Point
    {
        public readonly float x, y;
        public Point(float x, float y) { this.x = x; this.y = y; }
    }

    public static Point Sample(EnemyArtworkProfile profile, EnemyArtworkPose pose, float x, float y,
        float time, float cycle = 3f, float contact = .43f)
    {
        if (profile == EnemyArtworkProfile.Sloth) return Sloth(pose, x, y, time, cycle, contact);
        if (pose == EnemyArtworkPose.Death) return Death(profile, x, y, time);
        float phase = time * (float)(Math.PI * 2) / Math.Max(.1f, cycle);
        float breath = (float)Math.Sin(phase);
        float head = (1f - Smooth(.28f, .49f, x)) * Smooth(.25f, .48f, y);
        float tail = Smooth(.65f, .84f, x);
        float upper = Smooth(.05f, .30f, y);
        float claw = (1f - Smooth(.15f, .39f, x)) * Bell(y, .65f, .20f);
        float dx = 0, dy = 0;
        if (pose == EnemyArtworkPose.Idle)
        {
            if (profile == EnemyArtworkProfile.Rat)
            {
                float chest = Bell(x, .40f, .30f) * upper;
                dx = .005f * breath * chest + .012f * (float)Math.Sin(phase * .5f) * tail * upper;
                dy = .008f * breath * chest + .009f * (float)Math.Sin(phase + x * 3f) * tail * upper;
                // Ears breathe with the skull, paws remain planted.
                dx += .004f * (float)Math.Sin(phase + .6f) * head * Smooth(.65f, .9f, y);
            }
            else
            {
                dx = .009f * (float)Math.Sin(phase) + .014f * (float)Math.Sin(phase + y * 4f) * (1f - y);
                dy = .014f * breath + .007f * (float)Math.Sin(phase + x * 4f) * (1f - y);
                dx -= .010f * (float)Math.Sin(phase + .4f) * claw;
            }
        }
        else if (pose == EnemyArtworkPose.Attack)
        {
            float t = Clamp(time), wind = EnemyAttackMotion.Windup(t, contact);
            float strike = EnemyAttackMotion.Advance(t, contact);
            if (profile == EnemyArtworkProfile.Rat)
            {
                dx = .018f * wind * upper - .050f * strike * head - .008f * strike * upper;
                dy = -.018f * wind * upper + .012f * strike * head - .006f * strike * upper;
                dx += .018f * strike * tail * upper;
                // Front paws reach during the bite while the back pushes off.
                dx -= .024f * strike * Bell(x, .27f, .19f) * (1f - Smooth(.12f, .35f, y));
                dy += .010f * strike * tail * upper;
            }
            else
            {
                dx = .018f * wind * upper - .105f * strike * claw - .010f * strike * upper;
                dy = .010f * wind * upper - .060f * strike * claw;
                dx += .014f * strike * (1f - y) * Smooth(.45f, .8f, x);
                dy += .012f * strike * (1f - y);
            }
        }
        else if (pose == EnemyArtworkPose.Hurt)
        {
            float t = Clamp(time), recoil = t < .23f ? Smooth(0, .23f, t) : 1f - Smooth(.23f, 1f, t);
            dx = recoil * (profile == EnemyArtworkProfile.Rat ? .065f : .085f) * upper;
            dy = -recoil * (profile == EnemyArtworkProfile.Rat ? .035f : .045f) * upper;
            if (profile == EnemyArtworkProfile.Wraith)
                dx += (.52f - x) * .16f * recoil * (1f - y);
        }
        return new Point(x + dx, y + dy);
    }

    static Point Sloth(EnemyArtworkPose pose, float x, float y, float time, float cycle, float contact)
    {
        // Feet stay planted while the heavy chest, bowed head and raised forepaw move separately.
        float upper = Smooth(.055f, .24f, y);
        float chest = Bell(x, .56f, .36f) * upper;
        float head = (1f - Smooth(.24f, .48f, x)) * Smooth(.46f, .68f, y);
        float paw = Bell(x, .16f, .19f) * Bell(y, .31f, .27f) * upper;
        float dx = 0, dy = 0;
        if (pose == EnemyArtworkPose.Idle)
        {
            float phase = time * (float)(Math.PI * 2) / Math.Max(.1f, cycle);
            float breath = (float)Math.Sin(phase);
            float nod = (float)Math.Sin(phase + .35f);
            dx = .014f * breath * (x - .54f) * chest - .004f * nod * head;
            dy = .012f * breath * chest - .007f * nod * head;
            dx += .003f * (float)Math.Sin(phase - .45f) * paw;
            dy += .005f * (float)Math.Sin(phase - .45f) * paw;
        }
        else if (pose == EnemyArtworkPose.Attack)
        {
            float t = Clamp(time);
            float wind = EnemyAttackMotion.Windup(t, contact);
            float strike = EnemyAttackMotion.Advance(t, contact);
            dx = .014f * wind * upper + .025f * wind * paw - .012f * strike * upper - .095f * strike * paw;
            dy = -.012f * wind * chest + .055f * wind * paw - .036f * strike * paw - .012f * strike * head;
        }
        else if (pose == EnemyArtworkPose.Hurt)
        {
            float t = Clamp(time);
            float recoil = t <= .22f ? Smooth(0, .22f, t) : 1f - Smooth(.22f, 1, t);
            dx = .045f * recoil * upper + .018f * recoil * head;
            dy = -.025f * recoil * chest - .028f * recoil * head + .018f * recoil * paw;
        }
        else if (pose == EnemyArtworkPose.Death)
        {
            float slump = Smooth(0, .90f, time), bow = Smooth(.16f, 1f, time);
            dx = -.052f * bow * head + (.52f - x) * .065f * slump * (1f - upper);
            dy = -y * (.46f * slump + .10f * bow * (1f - Smooth(.24f, .48f, x)));
        }
        return new Point(x + dx, y + dy);
    }

    static Point Death(EnemyArtworkProfile profile, float x, float y, float t)
    {
        float p = Smooth(0, 1, t);
        if (profile == EnemyArtworkProfile.Rat)
        {
            float head = (1f - Smooth(.25f, .49f, x)) * Smooth(.22f, .52f, y);
            float paws = 1f - Smooth(.08f, .32f, y);
            float tail = Smooth(.68f, .87f, x);
            // Keep positive area all the way to the paws: clipping a translated mesh at y=0
            // would make the lower half appear to sink through the floor.
            float endY = y * (.54f - .16f * head - .04f * tail);
            float endX = x + (.45f - x) * .11f * paws - .035f * head + .024f * tail;
            return new Point(x + (endX - x) * p, y + (endY - y) * p);
        }
        // Hood bows and the robe sinks into a compact heap before turning into ash.
        float fall = Smooth(0, .92f, t), curl = Smooth(.18f, 1f, t);
        float finalY = y * (.39f - .09f * Smooth(.45f, .82f, y));
        float finalX = .52f + (x - .52f) * (1f - .24f * curl) - .09f * curl * Smooth(.45f, .82f, y);
        return new Point(finalX, y + (finalY - y) * fall);
    }

    static float Bell(float x, float center, float width)
    { float a = (x - center) / width; return (float)Math.Exp(-a * a * 2f); }
    static float Clamp(float t) => Math.Max(0, Math.Min(1, t));
    public static float Smooth(float a, float b, float t)
    { t = Clamp((t - a) / (b - a)); return t * t * (3 - 2 * t); }
}
