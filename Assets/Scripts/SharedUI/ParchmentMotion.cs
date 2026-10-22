using System;

// Independent of Canvas and timeScale, so opening has a firm input deadline.
public sealed class ParchmentMotion
{
    public const double OpenSeconds = 0.8;
    public const double CloseSeconds = 0.5;
    public const double TravelPortion = 0.8125; // Paper reaches the left at 0.65 s.
    double phase;
    public bool WantsOpen { get; private set; }
    public float Phase => (float)phase;
    public bool IsVisible => WantsOpen || phase > 0;
    public bool CanInteract => WantsOpen && phase >= 1;

    public void Request(bool open) { WantsOpen = open; }
    public void Reset() { phase = 0; WantsOpen = false; }

    public void Step(double unscaledDelta)
    {
        if (double.IsNaN(unscaledDelta) || double.IsInfinity(unscaledDelta) || unscaledDelta <= 0) return;
        phase = Clamp(phase + (WantsOpen ? unscaledDelta / OpenSeconds : -unscaledDelta / CloseSeconds));
        if (phase > 1 - 1e-7) phase = 1;
        if (phase < 1e-7) phase = 0;
    }

    public static float Travel(float value)
    {
        double t = Clamp(value / TravelPortion);
        return (float)(t * (2 - t));
    }

    public static float Settle(float value) => Smooth((value - (float)TravelPortion) / (1 - (float)TravelPortion));
    public static float RollWidth(float value) => 1 - Settle(value);
    public static float RollAlpha(float value) => 1 - Smooth((Settle(value) - .35f) / .65f);
    public static float Backdrop(float value) => Smooth(value / .35f);
    public static float Bend(float y, float value) =>
        (float)(Math.Sin(y * Math.PI * 2 + value * 2) * Math.Sin(value * Math.PI) * .035);

    public readonly struct Pose
    {
        public readonly float front, width, height, paperFill, inkInset, alpha;
        public Pose(float front, float width, float height, float paperFill, float inkInset, float alpha)
        {
            this.front = front; this.width = width; this.height = height;
            this.paperFill = paperFill; this.inkInset = inkInset; this.alpha = alpha;
        }
    }

    // Coordinates are centered on the paper, with inkInset measured from its left.
    public static Pose Evaluate(float phase, float paperWidth, float paperHeight)
    {
        float baseWidth = paperHeight * .105f;
        float width = baseWidth * RollWidth(phase);
        float front = (float)((paperWidth * .5f + baseWidth * .6f) * (1 - Travel(phase))
            + (-paperWidth * .5f + paperWidth * .018f) * Travel(phase));
        float fill = phase >= 1 ? 1 : 1 - (float)Clamp((front + width * .15f + paperWidth * .5f) / Math.Max(1, paperWidth));
        float inset = phase >= 1 ? 0 : (float)Clamp((front + width * .52f + paperWidth * .006f + paperWidth * .5f) / Math.Max(1, paperWidth)) * paperWidth;
        return new Pose(front, width, paperHeight * .968f, fill, inset, RollAlpha(phase));
    }
    public static float Smooth(float value)
    {
        double t = Clamp(value);
        return (float)(t * t * (3 - 2 * t));
    }
    static double Clamp(double value) => Math.Max(0, Math.Min(1, value));
}
