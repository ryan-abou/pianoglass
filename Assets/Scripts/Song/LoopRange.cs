namespace PianoGlass.Song
{
    /// <summary>
    /// A practice loop over a run of bars (F33). Pure, so the rule for which
    /// bars and the times they cover are tested headlessly.
    ///
    /// The clock wraps from the end of the last bar back to one bar before
    /// the first: every pass gets a bar of count-in, the same way the song
    /// itself gets a lead-in. Only notes inside the range are drawn or scored.
    /// </summary>
    public readonly struct LoopRange
    {
        public readonly int FromBar, ToBar;
        /// <summary>Song time of the first bar's start.</summary>
        public readonly double NotesFrom;
        /// <summary>Song time of the last bar's end: where the clock wraps.</summary>
        public readonly double NotesUntil;
        /// <summary>Song time the clock wraps back to, one bar before NotesFrom.</summary>
        public readonly double WrapTo;

        private LoopRange(int fromBar, int toBar, double notesFrom, double notesUntil, double wrapTo)
        {
            FromBar = fromBar;
            ToBar = toBar;
            NotesFrom = notesFrom;
            NotesUntil = notesUntil;
            WrapTo = wrapTo;
        }

        /// <summary>Song seconds from one wrap to the next.</summary>
        public double PassLength => NotesUntil - WrapTo;

        public bool IsSingleBar => FromBar == ToBar;

        /// <summary>"bar 3" or "bars 2-4", for the panel.</summary>
        public string Label => IsSingleBar ? $"bar {FromBar}" : $"bars {FromBar}–{ToBar}";

        /// <summary>The bars given, clamped to the song and put in order.</summary>
        public static LoopRange Between(SongData song, int fromBar, int toBar)
        {
            int last = System.Math.Max(1, song.BarCount);
            int from = System.Math.Max(1, System.Math.Min(System.Math.Min(fromBar, toBar), last));
            int to = System.Math.Max(from, System.Math.Min(System.Math.Max(fromBar, toBar), last));
            double start = song.BarStartTime(from);
            return new LoopRange(from, to, start, song.BarStartTime(to + 1), start - song.SecondsPerBar);
        }

        /// <summary>The worst bar and one either side, clamped to the song.</summary>
        public static (int from, int to) AroundBar(int bar, int barCount)
        {
            int last = System.Math.Max(1, barCount);
            int b = System.Math.Max(1, System.Math.Min(bar, last));
            return (System.Math.Max(1, b - 1), System.Math.Min(last, b + 1));
        }

        /// <summary>Whether a note starting at this song time is in the loop.
        /// A hair of tolerance, so rounding cannot move a note on a bar line
        /// into the wrong bar.</summary>
        public bool Holds(double noteStartTime) =>
            noteStartTime >= NotesFrom - 1e-9 && noteStartTime < NotesUntil - 1e-9;
    }
}
