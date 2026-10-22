using System;
using System.Collections.Generic;

// Continuous skinning of the registered ready drawing. All coordinates are in
// its 2048px authoring canvas (top-left origin), independent of render FPS.
public static class PlayerThrustRig
{
    public const float Canvas = 2048f, RootX = 1040f, RootY = 1760f;
    public const float DefaultDuration = .75f, DefaultHitTime = .26f;
    // The drawn palm axis points down/right (72 degrees in canvas coordinates).
    // Rotate the fist as well as moving the hilt; layering alone cannot fix it.
    public const float AuthoredGripAxisAngle = 72f;
    public const float ReadyWeaponAngle = 72f;
    const float Degrees = (float)(Math.PI / 180.0);
    static readonly Point Hip = new Point(751f, 1262f);
    static readonly Point Neck = new Point(773f, 908f);
    static readonly Point Shoulder = new Point(849.8f, 965.6f);
    public static Point RestShoulder => Shoulder;
    static readonly Point Elbow = new Point(853f, 1100f);
    static readonly Point Wrist = new Point(957f, 1042f);
    public static readonly Point ReadyGrip = new Point(997f, 1033f);

    public struct Point
    {
        public float x, y;
        public Point(float x, float y) { this.x = x; this.y = y; }
        public static Point operator +(Point a, Point b) => new Point(a.x + b.x, a.y + b.y);
        public static Point operator -(Point a, Point b) => new Point(a.x - b.x, a.y - b.y);
        public static Point operator *(Point a, float s) => new Point(a.x * s, a.y * s);
    }

    public struct Affine
    {
        public float a, b, c, d, x, y;
        public Point Apply(Point p) => new Point(a * p.x + b * p.y + x, c * p.x + d * p.y + y);
    }

    public struct Binding
    {
        public Point rest;
        public float head, arm, forearm, hand, lowerBody, hip, knee, frontLeg, cape;
    }

    public struct Pose
    {
        public Affine torso, head, upperArm, forearm, hand;
        public Point grip, hipDelta;
        public float weaponAngle, kneeBend, capeX, capeY;
    }

    public sealed class Geometry
    {
        public Binding[] bindings;
        public int[] triangles;
    }

    public static Binding BindBody(float x, float y)
    {
        Binding result = Bind(x, y);
        result.arm = 0f;
        return result;
    }

    public static Binding BindArm(float x, float y)
    {
        Binding result = Bind(x, y);
        result.arm = 1f;
        result.head = result.lowerBody = result.cape = 0f;
        return result;
    }

    public static Geometry CreateGeometry(bool arm)
    {
        if (!arm)
        {
            const int count = 64;
            var bindings = new Binding[(count + 1) * (count + 1)];
            var triangles = new int[count * count * 6];
            for (int y = 0; y <= count; y++)
                for (int x = 0; x <= count; x++)
                    bindings[y * (count + 1) + x] = BindBody(x * Canvas / count, y * Canvas / count);
            int next = 0;
            for (int y = 0; y < count; y++) for (int x = 0; x < count; x++)
            {
                int a = y * (count + 1) + x, b = a + 1, c = a + count + 1;
                triangles[next++] = a; triangles[next++] = b; triangles[next++] = c;
                triangles[next++] = b; triangles[next++] = c + 1; triangles[next++] = c;
            }
            return new Geometry { bindings = bindings, triangles = triangles };
        }

        // Source UV silhouette of the original bare arm/bracer/fist. A separate
        // mesh can rotate freely without dragging the tunic or changing faces.
        var vertices = new List<Binding>();
        var indices = new List<int>();
        AppendArmPolygon(new[] {
            new Point(828, 962), new Point(870, 962), new Point(892, 1002),
            new Point(891, 1089), new Point(878, 1136), new Point(835, 1131),
            new Point(824, 1081)
        }, 0, vertices, indices);
        AppendArmPolygon(new[] {
            new Point(849, 1062), new Point(900, 1034), new Point(943, 1008),
            new Point(951, 1020), new Point(979, 1078), new Point(927, 1114),
            new Point(874, 1141), new Point(837, 1123), new Point(828, 1085)
        }, 1, vertices, indices);
        AppendArmPolygon(new[] {
            new Point(939, 1005), new Point(966, 981), new Point(1004, 978),
            new Point(1025, 991), new Point(1043, 1019), new Point(1042, 1057),
            new Point(1024, 1075), new Point(980, 1086), new Point(957, 1074),
            new Point(947, 1047)
        }, 2, vertices, indices);
        return new Geometry { bindings = vertices.ToArray(), triangles = indices.ToArray() };
    }

    static void AppendArmPolygon(Point[] outline, int bone, List<Binding> vertices, List<int> indices)
    {
        var remaining = new List<int>();
        for (int i = 0; i < outline.Length; i++) remaining.Add(i);
        while (remaining.Count > 3)
        {
            bool found = false;
            for (int i = 0; i < remaining.Count; i++)
            {
                int previous = remaining[(i + remaining.Count - 1) % remaining.Count];
                int current = remaining[i], next = remaining[(i + 1) % remaining.Count];
                Point a = outline[previous], b = outline[current], c = outline[next];
                if (Cross(b - a, c - a) <= 0f) continue;
                bool inside = false;
                foreach (int candidate in remaining)
                {
                    if (candidate == previous || candidate == current || candidate == next) continue;
                    Point p = outline[candidate];
                    if (Cross(b - a, p - a) >= 0f && Cross(c - b, p - b) >= 0f && Cross(a - c, p - c) >= 0f)
                    { inside = true; break; }
                }
                if (inside) continue;
                Subdivide(a, b, c, bone, vertices, indices);
                remaining.RemoveAt(i); found = true; break;
            }
            if (!found) throw new InvalidOperationException("Invalid arm source outline");
        }
        Subdivide(outline[remaining[0]], outline[remaining[1]], outline[remaining[2]], bone, vertices, indices);
    }

    static void Subdivide(Point a, Point b, Point c, int bone, List<Binding> vertices, List<int> triangles)
    {
        const int steps = 1;
        int first = vertices.Count;
        for (int i = 0; i <= steps; i++) for (int j = 0; j <= steps - i; j++)
        {
            Point p = a + (b - a) * (i / (float)steps) + (c - a) * (j / (float)steps);
            Binding binding = BindArm(p.x, p.y);
            binding.forearm = bone == 0 ? 0f : 1f;
            binding.hand = bone == 2 ? 1f : 0f;
            vertices.Add(binding);
        }
        for (int i = 0; i < steps; i++) for (int j = 0; j < steps - i; j++)
        {
            int p = first + i * (steps + 1) - i * (i - 1) / 2 + j;
            int q = first + (i + 1) * (steps + 1) - i * (i + 1) / 2 + j;
            triangles.Add(p); triangles.Add(q); triangles.Add(p + 1);
            if (j < steps - i - 1) { triangles.Add(q); triangles.Add(q + 1); triangles.Add(p + 1); }
        }
    }

    static float Cross(Point a, Point b) => a.x * b.y - a.y * b.x;

    public static Binding Bind(float x, float y)
    {
        Point p = new Point(x, y);
        float foreT;
        float upperDistance = SegmentDistance(p, Shoulder, Elbow, out _);
        float foreDistance = SegmentDistance(p, Elbow, ReadyGrip, out foreT);
        float foreWeight = Smooth(.1f, .9f, upperDistance / Math.Max(.001f, upperDistance + foreDistance));
        float arm = (1f - Smooth(55f, 92f, Math.Min(upperDistance, foreDistance)))
            * Smooth(798f, 843f, x) * Smooth(918f, 957f, y)
            * (1f - Smooth(1143f, 1200f, y))
            * Smooth(26f, 75f, Length(p - Shoulder));
        float hip = 1f - Smooth(1260f, 1665f, y);
        return new Binding
        {
            rest = p,
            head = 1f - Smooth(866f, 970f, y),
            arm = arm,
            forearm = foreWeight,
            hand = Smooth(.62f, .9f, foreT) * Smooth(.55f, .9f, foreWeight),
            lowerBody = Smooth(1255f, 1370f, y),
            hip = hip,
            knee = 4f * hip * (1f - hip),
            frontLeg = Smooth(723f, 821f, x),
            cape = (1f - Smooth(626f, 725f, x)) * Smooth(885f, 1320f, y)
                * (1f - Smooth(1330f, 1425f, y))
        };
    }

    public static Pose Sample(float seconds, float duration = DefaultDuration, float hitSeconds = -1f)
        => SampleMotion(seconds, duration, hitSeconds, 0f, false);

    public static Pose SampleWithIdle(float seconds, float duration, float hitSeconds, float idleSeconds)
        => SampleMotion(seconds, duration, hitSeconds, idleSeconds, true);

    public static Pose SampleIdle(float seconds) => BuildPose(0f, 0f, 0f, 0f, seconds, 1f);

    static Pose SampleMotion(float seconds, float duration, float hitSeconds, float idleSeconds, bool idle)
    {
        duration = Math.Max(.001f, duration);
        float hit = hitSeconds > 0f ? Clamp(hitSeconds, .0001f, duration - .0001f)
            : DefaultHitTime * duration / DefaultDuration;
        float t = seconds <= hit ? DefaultHitTime * seconds / hit
            : DefaultHitTime + (DefaultDuration - DefaultHitTime) * (seconds - hit) / (duration - hit);
        t = Clamp(t, 0f, DefaultDuration);
        float thrust = Thrust(t);
        float wind = t < .16f ? Ease(t / .16f)
            : t < DefaultHitTime ? 1f - Ease((t - .16f) / .10f) : 0f;
        float cape = Thrust(Clamp(t - .025f, 0f, DefaultDuration));
        cape *= 1f - Smooth(.62f, DefaultDuration, t);
        // Push from the hips just before the arm releases, rather than letting
        // the hand pull an otherwise upright body toward the target.
        float drive = t < .145f ? 0f : t < .25f ? Square((t - .145f) / .105f)
            : t <= .31f ? 1f : 1f - Ease((t - .31f) / .33f);
        float idleBlend = idle ? 1f - Smooth(0f, .10f, t) * (1f - Smooth(.64f, DefaultDuration, t)) : 0f;
        return BuildPose(wind, thrust, drive, cape, idleSeconds, idleBlend);
    }

    static Pose BuildPose(float wind, float thrust, float drive, float cape, float idleSeconds, float idleBlend)
    {
        float phase = idleSeconds * (float)(2.0 * Math.PI / 3.2);
        float breath = (float)Math.Sin(phase) * idleBlend;
        float cloth = (float)Math.Sin(phase - .55f) * idleBlend;
        float torsoAngle = (-8f * wind + 17f * drive + .5f * breath) * Degrees;
        Point hipDelta = new Point(-28f * wind + 58f * drive + 1.5f * breath,
            38f * wind + 20f * drive - 4.5f * breath);
        Affine torso = Rotate(Hip, Hip + hipDelta, torsoAngle);
        Point currentShoulder = torso.Apply(Shoulder);
        Point currentGrip = torso.Apply(ReadyGrip) + new Point(-55f * wind, 45f * wind);
        float upperLength = Length(Elbow - Shoulder);
        float foreLength = Length(Wrist - Elbow);
        float weaponAngle = ReadyWeaponAngle - 14f * wind - ReadyWeaponAngle * thrust + .65f * breath;
        Point handOffset = Rotate(new Point(), new Point(),
            (180f - weaponAngle - AuthoredGripAxisAngle) * Degrees).Apply(ReadyGrip - Wrist);
        // Reach with the wrist, then add the rotated fist. Adding hand length
        // to arm length in a straight line used to stretch the bracer at impact.
        Point extended = currentShoulder + new Point((upperLength + foreLength) * .975f, -8f) + handOffset;
        currentGrip = Lerp(currentGrip, extended, thrust);
        return SolvePose(torso, torsoAngle, hipDelta, currentGrip, weaponAngle,
            32f * wind + 24f * drive + 1.2f * breath, -70f * cape + 10f * cloth, -24f * cape + 2f * cloth);
    }

    // Shared hand/arm solve for other one-handed motions. The palm/hilt axis and
    // the fixed-foot body deformation stay identical across weapon families.
    public static Pose FromShoulder(float angleDegrees, Point hipDelta, Point gripOffset,
        float weaponAngle, float kneeBend, float capeX, float capeY)
    {
        float angle = angleDegrees * Degrees;
        Affine torso = Rotate(Hip, Hip + hipDelta, angle);
        return SolvePose(torso, angle, hipDelta, torso.Apply(Shoulder) + gripOffset,
            weaponAngle, kneeBend, capeX, capeY);
    }

    // Pose a swing through the shoulder and elbow, with a small authored wrist
    // offset. Derive the hilt from the fist instead of forcing the wrist to
    // follow an independently rotated weapon. Bone lengths stay rigid.
    public static Pose FromArmRotations(float angleDegrees, Point hipDelta,
        float upperDegrees, float foreDegrees, float wristDegrees,
        float kneeBend, float capeX, float capeY)
    {
        float angle = angleDegrees * Degrees;
        Affine torso = Rotate(Hip, Hip + hipDelta, angle);
        Point shoulder = torso.Apply(Shoulder);
        Point elbow = shoulder + Rotate(new Point(), new Point(), upperDegrees * Degrees).Apply(Elbow - Shoulder);
        Point wrist = elbow + Rotate(new Point(), new Point(), foreDegrees * Degrees).Apply(Wrist - Elbow);
        float handDegrees = foreDegrees + wristDegrees;
        Affine hand = Rotate(Wrist, wrist, handDegrees * Degrees);
        return new Pose
        {
            torso = torso,
            head = Rotate(Neck, torso.Apply(Neck), angle * .35f),
            upperArm = Bone(Shoulder, Elbow, shoulder, elbow),
            forearm = Bone(Elbow, Wrist, elbow, wrist),
            hand = hand,
            grip = hand.Apply(ReadyGrip),
            hipDelta = hipDelta,
            weaponAngle = 180f - AuthoredGripAxisAngle - handDegrees,
            kneeBend = kneeBend,
            capeX = capeX,
            capeY = capeY
        };
    }

    static Pose SolvePose(Affine torso, float torsoAngle, Point hipDelta, Point currentGrip,
        float weaponAngle, float kneeBend, float capeX, float capeY)
    {
        Point currentShoulder = torso.Apply(Shoulder);
        float upperLength = Length(Elbow - Shoulder), foreLength = Length(Wrist - Elbow);
        float handAngle = (180f - weaponAngle - AuthoredGripAxisAngle) * Degrees;
        Point handOffset = Rotate(new Point(), new Point(), handAngle).Apply(ReadyGrip - Wrist);
        Point currentWrist = currentGrip - handOffset;
        Point currentElbow = SolveElbow(currentShoulder, currentWrist, upperLength, foreLength);
        return new Pose
        {
            torso = torso,
            head = Rotate(Neck, torso.Apply(Neck), torsoAngle * .35f),
            upperArm = Bone(Shoulder, Elbow, currentShoulder, currentElbow),
            forearm = Bone(Elbow, Wrist, currentElbow, currentWrist),
            hand = Rotate(Wrist, currentWrist, handAngle),
            grip = currentGrip,
            hipDelta = hipDelta,
            weaponAngle = weaponAngle,
            kneeBend = kneeBend,
            capeX = capeX,
            capeY = capeY
        };
    }

    public static Point Deform(Binding binding, Pose pose)
    {
        Point p = binding.rest;
        Point upper = pose.torso.Apply(p);
        Point leg = p + pose.hipDelta * binding.hip;
        leg.x += pose.kneeBend * binding.knee * (.35f + .65f * binding.frontLeg);
        Point result = Lerp(upper, leg, binding.lowerBody);
        result = Lerp(result, pose.head.Apply(p), binding.head);
        Point arm = Lerp(pose.upperArm.Apply(p), pose.forearm.Apply(p), binding.forearm);
        arm = Lerp(arm, pose.hand.Apply(p), binding.hand);
        result = Lerp(result, arm, binding.arm);
        result.x += pose.capeX * binding.cape;
        result.y += pose.capeY * binding.cape;
        return result;
    }

    static float Thrust(float t)
    {
        if (t <= .16f || t >= .64f) return 0f;
        if (t < DefaultHitTime)
        {
            float release = (t - .16f) / .10f;
            // Accelerate into contact instead of easing to a stop before impact.
            return release * release;
        }
        if (t <= .31f) return 1f;
        return 1f - Ease((t - .31f) / .33f);
    }

    static Affine Rotate(Point source, Point destination, float angle)
    {
        float c = (float)Math.Cos(angle), s = (float)Math.Sin(angle);
        return new Affine { a = c, b = -s, c = s, d = c,
            x = destination.x - c * source.x + s * source.y,
            y = destination.y - s * source.x - c * source.y };
    }

    static Affine Bone(Point a, Point b, Point targetA, Point targetB)
    {
        Point rest = b - a, current = targetB - targetA;
        float rl = Length(rest), cl = Length(current);
        float rx = rest.x / rl, ry = rest.y / rl;
        float tx = current.x / cl, ty = current.y / cl, scale = cl / rl;
        float m00 = tx * scale * rx + ty * ry;
        float m01 = tx * scale * ry - ty * rx;
        float m10 = ty * scale * rx - tx * ry;
        float m11 = ty * scale * ry + tx * rx;
        return new Affine { a = m00, b = m01, c = m10, d = m11,
            x = targetA.x - m00 * a.x - m01 * a.y,
            y = targetA.y - m10 * a.x - m11 * a.y };
    }

    static Point SolveElbow(Point shoulder, Point hand, float upper, float lower)
    {
        Point delta = hand - shoulder;
        float distance = Clamp(Length(delta), Math.Abs(upper - lower) + .01f, upper + lower - .01f);
        float baseAngle = (float)Math.Atan2(delta.y, delta.x);
        float offset = (float)Math.Acos(Clamp((upper * upper + distance * distance - lower * lower) / (2f * upper * distance), -1f, 1f));
        return shoulder + new Point((float)Math.Cos(baseAngle + offset), (float)Math.Sin(baseAngle + offset)) * upper;
    }

    static float SegmentDistance(Point p, Point a, Point b, out float t)
    {
        Point d = b - a;
        t = ((p.x - a.x) * d.x + (p.y - a.y) * d.y) / (d.x * d.x + d.y * d.y);
        return Length(p - Lerp(a, b, Clamp(t, 0f, 1f)));
    }
    static float Length(Point p) => (float)Math.Sqrt(p.x * p.x + p.y * p.y);
    static float Square(float x) => x * x;
    static Point Lerp(Point a, Point b, float t) => a + (b - a) * t;
    static float Clamp(float x, float min, float max) => Math.Max(min, Math.Min(max, x));
    static float Smooth(float min, float max, float x) => Ease((x - min) / (max - min));
    static float Ease(float x) { x = Clamp(x, 0f, 1f); return x * x * x * (x * (x * 6f - 15f) + 10f); }
}
