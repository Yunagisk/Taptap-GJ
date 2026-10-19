using System;
using TMPro;
using UnityEngine;

public sealed class MapTimeDialView : MonoBehaviour
{
    public MapTimeDialGraphic dial;
    public TMP_Text periodLabel;
    static WorldState lastWorld;
    static int lastVisibleTime;
    WorldState world;
    readonly MapTimeDialMotion motion = new MapTimeDialMotion();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetSession() { lastWorld = null; lastVisibleTime = 0; }

    void OnEnable() => world = null;

    void Update()
    {
        var state = GameRoot.I != null ? GameRoot.I.state : null;
        if (state == null || state.world == null) return;
        double now = UnityEngine.Time.unscaledTimeAsDouble;
        int time = Math.Max(0, state.world.time);
        bool changed = false;
        if (!ReferenceEquals(world, state.world))
        {
            world = state.world;
            // A fresh/load game opens at its actual time; a returning map can
            // animate time spent in combat or in the village while it was hidden.
            motion.Snap(ReferenceEquals(lastWorld, world) ? lastVisibleTime : time, now);
            changed = true;
        }
        changed |= motion.SetTime(time, now);
        if (changed && periodLabel != null) periodLabel.text = MapTimeDialMotion.FormatLabel(time);
        if (dial != null) dial.SetRotation((float)MapTimeDialMotion.Wrap(motion.Angle(now)));
        lastWorld = world;
        lastVisibleTime = time;
    }
}
