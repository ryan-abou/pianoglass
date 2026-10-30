using System;

namespace PianoGlass.Song
{
    /// <summary>
    /// Turns the runtime's per-frame display-latency readings into the one
    /// steady value SongClock draws with. Pure, so it is tested headlessly;
    /// OvrDisplayLatency feeds it one raw reading per frame.
    ///
    /// On device the raw reading is noisy: most frames 15-44 ms, some 0, one
    /// -253 s. Clamping those (what SongClock alone did) still feeds a 0 or a
    /// 100 ms into one frame's drawing, so the whole highway jumps. Here an
    /// implausible reading is rejected outright and the last good value is
    /// held; plausible ones are smoothed with a ~1 s time constant so the
    /// highway does not jitter with frame timing.
    /// </summary>
    public class DisplayLatencyFilter
    {
        /// <summary>A reading at or below this is "the runtime had nothing",
        /// not a real latency -- no headset displays a frame in under 2 ms.</summary>
        public const double MinPlausibleSeconds = 0.002;

        /// <summary>Same ceiling SongClock clamps to.</summary>
        public const double MaxPlausibleSeconds = SongClock.MaxDisplayLatencySeconds;

        private readonly double _timeConstantSeconds;

        public DisplayLatencyFilter(double timeConstantSeconds = 1.0)
        {
            _timeConstantSeconds = Math.Max(0.0, timeConstantSeconds);
        }

        /// <summary>Smoothed latency in seconds; 0 until the first plausible
        /// reading, then always the last smoothed value.</summary>
        public double Value { get; private set; }

        /// <summary>True once any plausible reading has been accepted.</summary>
        public bool HasValue { get; private set; }

        public int AcceptedCount { get; private set; }
        public int RejectedCount { get; private set; }

        /// <summary>Most recent rejected raw reading, for the log.</summary>
        public double LastRejected { get; private set; }

        public static bool IsPlausible(double seconds) =>
            !double.IsNaN(seconds) && seconds > MinPlausibleSeconds && seconds <= MaxPlausibleSeconds;

        /// <summary>Feed one frame's raw reading. deltaSeconds is the real
        /// time since the previous frame. Returns the new Value.</summary>
        public double Add(double rawSeconds, double deltaSeconds)
        {
            if (!IsPlausible(rawSeconds))
            {
                RejectedCount++;
                LastRejected = rawSeconds;
                return Value; // hold, don't drag toward a bad reading
            }

            AcceptedCount++;
            if (!HasValue || _timeConstantSeconds <= 0.0)
            {
                // Seed directly: ramping up from 0 over a second would draw
                // the first second of notes uncompensated.
                Value = rawSeconds;
                HasValue = true;
                return Value;
            }

            // Exponential moving average with a time constant, not a per-frame
            // weight, so the smoothing is the same at 72 Hz or after a hitch.
            double alpha = 1.0 - Math.Exp(-Math.Max(0.0, deltaSeconds) / _timeConstantSeconds);
            Value += (rawSeconds - Value) * alpha;
            return Value;
        }

        /// <summary>Zero the counters (after each log line); Value is kept.</summary>
        public void ResetCounts()
        {
            AcceptedCount = 0;
            RejectedCount = 0;
        }
    }
}
