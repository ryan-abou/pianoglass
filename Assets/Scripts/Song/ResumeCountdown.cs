namespace PianoGlass.Song
{
    /// <summary>
    /// 3, 2, 1 before a paused song goes on (#44, Ryan 2026-11-22), so the
    /// hands are back on the keys when the notes move again. Only for a
    /// resume from a pause part-way through: a start from the top has its
    /// lead-in already. While it runs the song stays frozen and presses are
    /// not scored (the clock is still paused); pausing again, or the keyboard
    /// coming out, cancels it. Pure and tested; SongTransport runs it.
    /// </summary>
    public sealed class ResumeCountdown
    {
        public const int From = 3;
        public const double StepSeconds = 1.0;

        private double _startedAt;

        public bool Running { get; private set; }

        public void Start(double now)
        {
            Running = true;
            _startedAt = now;
        }

        public void Cancel() => Running = false;

        /// <summary>The number to show now: 3, 2 or 1, or 0 when not running.</summary>
        public int Number(double now)
        {
            if (!Running) return 0;
            int n = From - (int)System.Math.Floor((now - _startedAt) / StepSeconds);
            return n < 1 ? 0 : n > From ? From : n;
        }

        /// <summary>True once, on the frame it reaches the end: play now.</summary>
        public bool Finished(double now)
        {
            if (!Running || now - _startedAt < From * StepSeconds) return false;
            Running = false;
            return true;
        }
    }
}
