using System;

// JSON timing and registration are shared by the exported preview and battle.
// Coordinates are local Unity units; moving the artwork never moves the actor.
[Serializable]
public sealed class PlayerAttackMotion
{
    [Serializable] public struct Point { public float x, y; }
    [Serializable] public sealed class Pose
    {
        public int frame;
        public float time;
        public Point weaponPositionUnity;
        public float weaponRotationZ;
        public float weaponScale;
    }
    [Serializable] public sealed class Hit { public float time; }

    public float duration;
    public bool rootMotion;
    public Pose[] frames;
    public Hit[] hitEvents;

    public bool IsUsable(int spriteCount)
    {
        if (!Finite(duration) || duration <= 0f || rootMotion
            || frames == null || frames.Length == 0
            || hitEvents == null || hitEvents.Length != 1
            || hitEvents[0] == null || !Finite(hitEvents[0].time)
            || hitEvents[0].time <= 0f || hitEvents[0].time >= duration)
            return false;

        float previous = -1f;
        for (int i = 0; i < frames.Length; i++)
        {
            Pose pose = frames[i];
            if (pose == null || pose.frame < 0 || pose.frame >= spriteCount
                || !Finite(pose.time) || pose.time <= previous || pose.time >= duration
                || !Finite(pose.weaponPositionUnity.x) || !Finite(pose.weaponPositionUnity.y)
                || !Finite(pose.weaponRotationZ) || !Finite(pose.weaponScale)
                || pose.weaponScale <= 0f)
                return false;
            previous = pose.time;
        }
        return frames[0].time == 0f;
    }

    public int PoseIndexAt(float time)
    {
        int index = 0;
        while (index + 1 < frames.Length && frames[index + 1].time <= time)
            index++;
        return index;
    }

    static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}
