using System;

// Baked 30 FPS front/side/back artwork and its independently mounted weapon.
// Explicit contact samples keep damage and artwork aligned between 30 Hz ticks.
[Serializable]
public sealed class PlayerTurnSpriteMotion
{
    [Serializable] public struct Point { public float x, y; public Point(float x, float y) { this.x = x; this.y = y; } }
    [Serializable] public sealed class Frame
    {
        public float time;
        public int sprite;
        public Point grip;
        public float angle, idleWeight;
        public string view;
    }
    [Serializable] public sealed class Clip
    {
        public float duration, release, hit;
        public Frame[] frames;

        public int FrameIndex(float time)
        {
            int index = 0;
            while (index + 1 < frames.Length && frames[index + 1].time <= time + .000001f) index++;
            return index;
        }
        public float Retimed(float time, float playbackDuration, float playbackHit)
        {
            playbackDuration = Math.Max(.001f, playbackDuration);
            playbackHit = Math.Max(.0001f, Math.Min(playbackDuration - .0001f, playbackHit));
            return time <= playbackHit ? hit * time / playbackHit
                : hit + (duration - hit) * (time - playbackHit) / (playbackDuration - playbackHit);
        }
        public bool IsUsable(int count)
        {
            if (!Finite(duration) || !Finite(hit) || !Finite(release) || release <= 0f || release >= hit
                || hit <= 0f || duration <= hit || frames == null || frames.Length < 2) return false;
            float previous = -1f;
            bool contact = false;
            foreach (Frame frame in frames)
            {
                if (frame == null || frame.sprite < 0 || frame.sprite >= count || !Finite(frame.time)
                    || frame.time <= previous || frame.time > duration + .00001f
                    || !Finite(frame.grip.x) || !Finite(frame.grip.y) || !Finite(frame.angle)
                    || !Finite(frame.idleWeight) || frame.idleWeight < 0f || frame.idleWeight > 1.00001f) return false;
                if (Math.Abs(frame.time - hit) < .00001f) contact = true;
                previous = frame.time;
            }
            return contact && frames[0].time == 0f && Math.Abs(previous - duration) < .00001f
                && frames[0].sprite == 0 && frames[frames.Length - 1].sprite == 0;
        }
    }

    public int fps;
    public Clip thrust, swing;
    public bool IsUsable(int count) => fps == 30 && count > 0 && thrust != null && swing != null
        && thrust.IsUsable(count) && swing.IsUsable(count);

    // Sample the breathing clock at the same 30 Hz as the drawing. The planted
    // feet receive exactly zero displacement, including at loop boundaries.
    public static Point Breathe(Point point, float seconds, float amount)
    {
        float clock = (float)Math.Floor(Math.Max(0f, seconds) * 30f + .00001f) / 30f;
        float phase = clock * (float)(2.0 * Math.PI / 3.2);
        float breath = (float)Math.Sin(phase), cloth = (float)Math.Sin(phase - .55f);
        float upper = 1f - Smooth(1250f, 1660f, point.y);
        float cape = (1f - Smooth(540f, 690f, point.x)) * Smooth(900f, 1170f, point.y)
            * (1f - Smooth(1390f, 1550f, point.y));
        return new Point(point.x + (1.5f * breath * upper + 6f * cloth * cape) * amount,
            point.y - 4.5f * breath * upper * amount);
    }
    static float Smooth(float a, float b, float p)
    {
        float t = Math.Max(0f, Math.Min(1f, (p - a) / (b - a)));
        return t * t * (3f - 2f * t);
    }
    static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
