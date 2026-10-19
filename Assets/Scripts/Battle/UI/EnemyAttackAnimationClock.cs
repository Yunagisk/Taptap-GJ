using System;

// The contact pose must be displayed even when a slow frame crosses the whole attack.
public sealed class EnemyAttackAnimationClock
{
    readonly int frameCount, contactFrame;
    readonly float framesPerSecond;
    bool hitDelivered;

    public float Duration => frameCount / framesPerSecond;
    public float ContactTime => contactFrame / framesPerSecond;

    public EnemyAttackAnimationClock(int count, float fps, int hitFrame)
    {
        frameCount = Math.Max(1, count);
        framesPerSecond = Math.Max(1f, fps);
        contactFrame = Math.Max(0, Math.Min(frameCount - 1, hitFrame));
    }

    public readonly struct Sample
    {
        public readonly int Frame;
        public readonly float Time;
        public readonly bool DeliverHit, Complete;
        public Sample(int frame, float time, bool deliverHit, bool complete)
        { Frame = frame; Time = time; DeliverHit = deliverHit; Complete = complete; }
    }

    public Sample At(float elapsed)
    {
        elapsed = Math.Max(0f, elapsed);
        if (!hitDelivered && elapsed >= ContactTime)
        {
            hitDelivered = true;
            return new Sample(contactFrame, ContactTime, true, false);
        }
        return new Sample(Math.Min(frameCount - 1, (int)(elapsed * framesPerSecond)),
            Math.Min(elapsed, Duration), false, elapsed >= Duration);
    }
}
