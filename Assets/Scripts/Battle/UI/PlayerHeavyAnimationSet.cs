using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Battle/Player Heavy Animation Set")]
public sealed class PlayerHeavyAnimationSet : ScriptableObject
{
    [Serializable] public sealed class Weapon
    {
        public string id;
        public Sprite sprite, flames, shaft;
        public float scale = 1f, duration = 1.44f;
        public Vector2 trailHeel, trailTip;
        public bool mirroredAtRest, turnOverhead;
        public float projectedWidth = 1f;

        public float FacingAt(float sourceTime)
        {
            double turn = turnOverhead
                // Keep the impact face through the landing hold; unwind only
                // while the weapon is being drawn back into its ready pose.
                ? Smooth(.50f, .72f, sourceTime) * (1.0 - Smooth(1.20f, 1.50f, sourceTime)) : 0.0;
            double width = Math.Cos(Math.PI * turn);
            // A thin edge remains visible at the middle of the axial turn.
            if (Math.Abs(width) < .025) width = .025;
            return (float)width * (mirroredAtRest ? -1f : 1f) * projectedWidth;
        }

        static double Smooth(float start, float end, float time)
        {
            double t = Math.Max(0.0, Math.Min(1.0, (time - start) / (end - start)));
            return t * t * t * (t * (t * 6.0 - 15.0) + 10.0);
        }
    }
    public TextAsset motion;
    public Sprite[] bodyFrames, handFrames;
    public Weapon[] weapons;
    public Weapon Find(string id)
    {
        if (weapons != null) foreach (var weapon in weapons)
            if (weapon != null && weapon.id == id && weapon.sprite != null) return weapon;
        return null;
    }
}
