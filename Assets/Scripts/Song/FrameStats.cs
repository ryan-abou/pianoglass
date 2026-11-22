namespace PianoGlass.Song
{
    /// <summary>
    /// Frame times while a song plays, for one line in the log at the end
    /// (#38). The headset's own VrApi frame rate lines live only in logcat,
    /// which a release build can't keep (round 11 lost them), so the app
    /// counts for itself. Pure and tested; FrameRateLog feeds it.
    /// </summary>
    public sealed class FrameStats
    {
        /// <summary>A frame longer than this is slow: at 72 Hz a frame is
        /// 13.9 ms, so 20 ms means the headset showed one frame twice.</summary>
        public const double SlowMs = 20.0;

        /// <summary>A gap longer than this is the app being paused (headset
        /// off, Meta menu), not a slow frame, and is left out.</summary>
        public const double PausedMs = 500.0;

        public int Frames { get; private set; }
        public int Slow { get; private set; }
        public double WorstMs { get; private set; }
        public double TotalSeconds { get; private set; }

        public double AverageFps => TotalSeconds > 0.0 ? Frames / TotalSeconds : 0.0;

        public void Add(double seconds)
        {
            double ms = seconds * 1000.0;
            if (ms <= 0.0 || ms > PausedMs) return;
            Frames++;
            TotalSeconds += seconds;
            if (ms > SlowMs) Slow++;
            if (ms > WorstMs) WorstMs = ms;
        }

        public void Clear()
        {
            Frames = 0;
            Slow = 0;
            WorstMs = 0.0;
            TotalSeconds = 0.0;
        }

        public string Summary(string what) =>
            $"{what}: {AverageFps:0.0} fps over {Frames} frames, {Slow} slow (over {SlowMs:0} ms), worst {WorstMs:0.0} ms";
    }
}
