using System;

namespace PianoGlass.Song
{
    /// <summary>
    /// Manual tempo (F26): 5% steps between 25% and 200%. Pure, so the
    /// clamping and the PlayerPrefs sanitising are tested headlessly. Works
    /// in whole percent internally so repeated steps never drift off the
    /// 5% grid (0.05 is not exact in binary floating point).
    ///
    /// Above 100% the highway gives less real warning (3 s of song time is
    /// 1.5 s real at 200%) and judging windows stay in real milliseconds, so
    /// nothing else depends on the range. Auto-tempo (Batch C) stops at 100%;
    /// that cap is its own rule, not this one.
    /// </summary>
    public static class TempoStep
    {
        public const int MinPercent = 25;
        public const int MaxPercent = 200;
        public const int StepPercent = 5;

        /// <summary>The multiplier, as a whole percent on the 5% grid and in range.</summary>
        public static int ToPercent(float multiplier)
        {
            if (float.IsNaN(multiplier) || float.IsInfinity(multiplier)) return 100;
            double steps = Math.Round(multiplier * 100.0 / StepPercent, MidpointRounding.AwayFromZero);
            if (steps < MinPercent / StepPercent) return MinPercent;
            if (steps > MaxPercent / StepPercent) return MaxPercent;
            return (int)steps * StepPercent;
        }

        /// <summary>One step slower (<paramref name="direction"/> &lt; 0) or
        /// faster (&gt; 0) from <paramref name="current"/>, clamped.</summary>
        public static float Step(float current, int direction)
        {
            int percent = ToPercent(current) + Math.Sign(direction) * StepPercent;
            if (percent < MinPercent) percent = MinPercent;
            if (percent > MaxPercent) percent = MaxPercent;
            return percent / 100f;
        }

        /// <summary>A stored value (PlayerPrefs) made safe to apply: off-grid,
        /// out of range or garbage all come back as a valid tempo.</summary>
        public static float Sanitize(float stored) => ToPercent(stored) / 100f;

        /// <summary>"Tempo 85%" -- what the status panel always shows.</summary>
        public static string Label(float multiplier) => $"Tempo {ToPercent(multiplier)}%";
    }
}
