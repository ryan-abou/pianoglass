namespace PianoGlass.Song
{
    /// <summary>
    /// 3, 2, 1 before a paused song goes on (#44, Ryan 2026-11-22), so the
    /// hands are back on the keys when the notes move again. Only for a
    /// resume from a pause part-way through: a start from the top has its
    /// lead-in already. While it runs the song stays frozen and presses are
    /// not scored (the clock is still paused); pausing again, or the keyboard
    /// coming out, cancels it. Pure and tested; SongTransport runs it.
    ///
    /// Without the metronome each number is a second. With it (round 13) it
    /// is a count-in: each number is one beat of the song, with a click, and
    /// the song comes back in on its own next beat. The song is paused
    /// between beats, so it goes on a little before that beat: part-way
    /// through the "1" (<see cref="Plan"/>).
    /// </summary>
    public sealed class ResumeCountdown
    {
        public const int From = 3;
        public const double PlainStepSeconds = 1.0;

        private double _startedAt, _delay, _step, _total;

        public bool Running { get; private set; }

        /// <summary>Seconds per number.</summary>
        public double StepSeconds => _step;

        /// <summary>A plain countdown: a second a number.</summary>
        public void Start(double now) => Start(now, 0.0, PlainStepSeconds, From * PlainStepSeconds);

        /// <param name="delay">Before the first step; "3" shows through it
        /// (the metronome's output latency, so the first click is on time).</param>
        /// <param name="step">Seconds per number.</param>
        /// <param name="total">From the end of the delay until the song plays.</param>
        public void Start(double now, double delay, double step, double total)
        {
            Running = true;
            _startedAt = now;
            _delay = System.Math.Max(0.0, delay);
            _step = step > 0.0 ? step : PlainStepSeconds;
            _total = System.Math.Max(0.0, total);
        }

        public void Cancel() => Running = false;

        /// <summary>The number to show now: 3, 2 or 1, or 0 when not running.</summary>
        public int Number(double now)
        {
            if (!Running) return 0;
            double t = now - _startedAt - _delay;
            if (t < 0.0) return From;
            int n = From - (int)System.Math.Floor(t / _step);
            return n < 1 ? 1 : n > From ? From : n;
        }

        /// <summary>True once, on the frame it reaches the end: play now.</summary>
        public bool Finished(double now)
        {
            if (!Running || now - _startedAt < _delay + _total) return false;
            Running = false;
            return true;
        }

        /// <summary>
        /// The count-in for a song paused at <paramref name="pausedAt"/> whose
        /// next beat is at <paramref name="nextBeat"/> (song seconds, one beat
        /// <paramref name="beatSongSeconds"/> long), at this tempo: seconds
        /// per number, and from the first number until the song plays, so it
        /// reaches that beat exactly when a fourth number would start.
        /// </summary>
        public static (double step, double total) Plan(double pausedAt, double nextBeat, double beatSongSeconds, double tempo)
        {
            if (tempo <= 0.0 || beatSongSeconds <= 0.0) return (PlainStepSeconds, From * PlainStepSeconds);
            double step = beatSongSeconds / tempo;
            double untilBeat = System.Math.Max(0.0, nextBeat - pausedAt) / tempo;
            return (step, From * step - untilBeat);
        }
    }
}
