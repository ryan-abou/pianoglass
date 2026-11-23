namespace PianoGlass.Song
{
    /// <summary>
    /// The count before a paused song goes on (#44, Ryan 2026-11-22), so the
    /// hands are back on the keys when the notes move again. Only for a
    /// resume from a pause part-way through: a start from the top has its
    /// lead-in already. While it runs the song stays frozen and presses are
    /// not scored (the clock is still paused); pausing again, or the keyboard
    /// coming out, cancels it. Pure and tested; SongTransport runs it.
    ///
    /// Without the metronome it is 3, 2, 1, a second each, and the song goes
    /// on from exactly where it stopped.
    ///
    /// With the metronome it is a count-in (round 15, Ryan's design): the
    /// song backs up to the start of the beat it was paused in (never
    /// forward, so no note is skipped), and the count is a whole number of
    /// beats before that beat, at least 3 beats and at least 3 seconds.
    /// Every beat shows one thing with its own click: a dot filling in for
    /// the early beats, then 3, 2, 1. The 1 ends exactly when the song plays,
    /// on its beat, and that beat's own click is the "play now". Over 5
    /// seconds only below 36 BPM, where 3 beats already take longer: the 3,
    /// 2, 1 is kept then. While the first beat shows, the highway glides back
    /// to the beat (<see cref="GlideProgress"/>).
    /// </summary>
    public sealed class ResumeCountdown
    {
        public const int From = 3;
        public const double PlainStepSeconds = 1.0;
        public const double MinSeconds = 3.0, MaxSeconds = 5.0;
        /// <summary>The glide back is this long, or a beat if that is shorter.</summary>
        public const double MaxGlideSeconds = 0.4;

        private const double Tolerance = 1e-9;

        private double _startedAt, _delay, _step;
        private int _beats;

        public bool Running { get; private set; }

        /// <summary>Seconds per beat (per number, without the metronome).</summary>
        public double StepSeconds => _step;
        /// <summary>Beats in the count: dots, then 3, 2, 1.</summary>
        public int Beats => _beats;
        /// <summary>Dots before the 3; none when the count is 3 beats.</summary>
        public int Dots => System.Math.Max(0, _beats - From);
        public double GlideSeconds => System.Math.Min(MaxGlideSeconds, _step);

        /// <summary>A plain countdown: a second a number.</summary>
        public void Start(double now) => Start(now, 0.0, PlainStepSeconds, From);

        /// <param name="delay">Before the first beat; the first beat shows
        /// through it (the metronome's output latency, so each beat appears
        /// as its click is heard).</param>
        /// <param name="step">Seconds per beat.</param>
        /// <param name="beats">How many beats; the song plays when they are over.</param>
        public void Start(double now, double delay, double step, int beats)
        {
            Running = true;
            _startedAt = now;
            _delay = System.Math.Max(0.0, delay);
            _step = step > 0.0 ? step : PlainStepSeconds;
            _beats = System.Math.Max(1, beats);
        }

        public void Cancel() => Running = false;

        /// <summary>Which beat is showing, 0 first, or -1 when not running.</summary>
        public int BeatIndex(double now)
        {
            if (!Running) return -1;
            double t = now - _startedAt - _delay;
            if (t < 0.0) return 0;
            int i = (int)System.Math.Floor(t / _step + Tolerance);
            return i >= _beats ? _beats - 1 : i;
        }

        /// <summary>The number to show now: 3, 2 or 1, or 0 on a dot beat or when not running.</summary>
        public int Number(double now)
        {
            int i = BeatIndex(now);
            if (i < 0) return 0;
            int n = _beats - i;
            return n <= From ? n : 0;
        }

        /// <summary>Dots filled in now: one more each dot beat, all of them
        /// once the numbers start.</summary>
        public int FilledDots(double now)
        {
            int i = BeatIndex(now);
            return i < 0 ? 0 : System.Math.Min(i + 1, Dots);
        }

        /// <summary>Seconds since the beat showing began (for its pulse).</summary>
        public double SinceBeat(double now)
        {
            int i = BeatIndex(now);
            if (i < 0) return 0.0;
            return System.Math.Max(0.0, now - _startedAt - _delay - i * _step);
        }

        /// <summary>How far the highway is through its glide back, 0 to 1,
        /// eased so it starts and stops softly. 1 once it is over.</summary>
        public double GlideProgress(double now)
        {
            if (!Running) return 1.0;
            double g = GlideSeconds;
            if (g <= 0.0) return 1.0;
            double x = (now - _startedAt) / g;
            if (x <= 0.0) return 0.0;
            if (x >= 1.0) return 1.0;
            return x * x * (3.0 - 2.0 * x);
        }

        /// <summary>Seconds from the start until the song plays.</summary>
        public double TotalSeconds => _delay + _beats * _step;

        /// <summary>True once, on the frame it reaches the end: play now.</summary>
        public bool Finished(double now)
        {
            if (!Running || now - _startedAt < TotalSeconds - Tolerance) return false;
            Running = false;
            return true;
        }

        /// <summary>A metronome count-in, worked out for one pause.</summary>
        public readonly struct CountIn
        {
            /// <summary>Song time the song backs up to and plays from: a beat.</summary>
            public readonly double RewindTo;
            /// <summary>Real seconds per beat.</summary>
            public readonly double Step;
            public readonly int Beats;
            /// <summary>Whether the beat it comes back in on starts a bar (an accented click).</summary>
            public readonly bool OnBarLine;

            public CountIn(double rewindTo, double step, int beats, bool onBarLine)
            {
                RewindTo = rewindTo;
                Step = step;
                Beats = beats;
                OnBarLine = onBarLine;
            }

            public double Seconds => Step * Beats;
        }

        /// <summary>How many beats a count-in at <paramref name="step"/> real
        /// seconds a beat has: at least 3, and at least 3 seconds.</summary>
        public static int BeatsFor(double step)
        {
            if (step <= 0.0) return From;
            return System.Math.Max(From, (int)System.Math.Ceiling(MinSeconds / step - Tolerance));
        }

        /// <summary>
        /// The count-in for a song paused at <paramref name="pausedAt"/>, at
        /// this tempo: back to the start of the beat it was paused in (the
        /// same spot if it was paused right on a beat), counted in with that
        /// beat's length.
        /// </summary>
        public static CountIn Plan(TempoMap map, double pausedAt, double tempo)
        {
            long k = map.FirstBeatAtOrAfter(pausedAt);
            if (map.Beat(k).Time > pausedAt + 1e-6) k--;
            BeatLine beat = map.Beat(k);
            double beatSongSeconds = map.Beat(k + 1).Time - beat.Time;
            double step = tempo > 0.0 && beatSongSeconds > 0.0 ? beatSongSeconds / tempo : PlainStepSeconds;
            return new CountIn(beat.Time, step, BeatsFor(step), beat.IsBarLine);
        }
    }
}
