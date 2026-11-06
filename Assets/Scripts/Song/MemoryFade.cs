namespace PianoGlass.Song
{
    /// <summary>
    /// Memory fade (F36): notes fade as the player shows they know them, and
    /// come back when they do not (Ryan, 2026-11-05). Pure, so the whole system
    /// is tested. One of these lives for one loop, starting at 100%; another
    /// belongs to the song, moves once per play-through and is saved, so a song
    /// already memorised stays faint. A "pass" below is either.
    ///
    /// The ladder: 100%, 60%, 30%, 15%, then a glimpse: each note shows at 15%
    /// when it appears and fades out on its way down. Notes never go without
    /// showing at all.
    ///
    /// After each pass, by the share of keys that were right (not timing:
    /// a late note was still remembered):
    ///
    ///   90% or better    one step fainter, unless the pass needed help
    ///   70% to 89%       stays
    ///   40% to 69%       one step back
    ///   under 40%        two steps back, and so does a pass with nothing
    ///                    played: someone who stopped may well be lost
    ///
    /// Help during a pass: a whole pass is a long time to be lost in, and in
    /// wait mode a player could otherwise sit forever at a note they cannot
    /// see. So every three mistakes in a row (a miss or a wrong key), or
    /// StuckSeconds held at one note, shows the notes one step clearer for the
    /// rest of that pass.
    ///
    /// The opacity is a ceiling on everything that shows the note: it still
    /// turns mint or slate once judged, at the opacity it already had. See
    /// OutcomeTint.FadedSlab.
    /// </summary>
    public sealed class MemoryFade
    {
        public const int Glimpse = 4;
        public const float GlimpseOpacity = 0.15f;
        /// <summary>Real seconds at GlimpseOpacity, then to nothing over GlimpseFadeSeconds.</summary>
        public const double GlimpseHoldSeconds = 0.6, GlimpseFadeSeconds = 0.5;

        public const double FainterFrom = 90.0, BackBelow = 70.0, FarBackBelow = 40.0;
        public const int MistakesForHelp = 3;
        public const float StuckSeconds = 5f;

        private static readonly float[] Steps = { 1f, 0.6f, 0.3f, GlimpseOpacity };

        /// <summary>Where the ladder stands, 0 (100%) to Glimpse.</summary>
        public int Step { get; private set; }

        /// <summary>Steps of help given during this pass.</summary>
        public int Help { get; private set; }

        private int _mistakesInARow;

        /// <param name="step">Where to start: a saved song's step. Clamped.</param>
        public MemoryFade(int step = 0)
        {
            Step = System.Math.Max(0, System.Math.Min(Glimpse, step));
        }

        /// <summary>The step actually showing, help included.</summary>
        public int ShownStep => System.Math.Max(0, Step - Help);

        public bool AtFullest => Step == 0;
        public bool AtFaintest => Step == Glimpse;

        /// <summary>How visible a note is that has been on the highway this many
        /// real seconds. Only a glimpse changes over a note's life.</summary>
        public float Opacity(double secondsVisible)
        {
            if (ShownStep < Glimpse) return Steps[ShownStep];
            double fading = (secondsVisible - GlimpseHoldSeconds) / GlimpseFadeSeconds;
            if (fading <= 0.0) return GlimpseOpacity;
            return fading >= 1.0 ? 0f : GlimpseOpacity * (float)(1.0 - fading);
        }

        /// <summary>What anything that does not fall can show: a lit key, or the
        /// panel naming a note. Nothing at a glimpse: it would give the note
        /// away long after the slab has gone.</summary>
        public float SteadyOpacity => ShownStep < Glimpse ? Steps[ShownStep] : 0f;

        public void OnNoteRight() => _mistakesInARow = 0;

        public void OnMistake()
        {
            if (++_mistakesInARow < MistakesForHelp) return;
            _mistakesInARow = 0;
            AskForHelp();
        }

        /// <summary>One step clearer for the rest of this pass.</summary>
        public void AskForHelp()
        {
            if (ShownStep > 0) Help++;
        }

        /// <summary>A pass given up on: its help goes, the ladder stays.</summary>
        public void ClearHelp()
        {
            Help = 0;
            _mistakesInARow = 0;
        }

        /// <summary>The step the next pass would use, changing nothing.</summary>
        public int StepAfter(double keysRightPercent, bool played)
        {
            if (!played || keysRightPercent < FarBackBelow) return System.Math.Max(0, Step - 2);
            if (keysRightPercent < BackBelow) return System.Math.Max(0, Step - 1);
            if (keysRightPercent >= FainterFrom && Help == 0) return System.Math.Min(Glimpse, Step + 1);
            return Step;
        }

        /// <summary>A pass ended: move on the ladder and drop this pass's help.</summary>
        public void EndPass(double keysRightPercent, bool played)
        {
            Step = StepAfter(keysRightPercent, played);
            Help = 0;
            _mistakesInARow = 0;
        }

        /// <summary>"notes 60%", or "notes glimpse".</summary>
        public static string Label(int step) => step >= Glimpse ? "notes glimpse" : $"notes {Steps[step] * 100f:0}%";

        /// <summary>What a pass does to the ladder, for the panel.</summary>
        public string Describe(int after)
        {
            if (after > Step) return $"{Label(after)}, fainter";
            if (after < Step) return $"{Label(after)}, back";
            return AtFaintest ? $"{Label(after)} stays" : $"{Label(after)} stays, {FainterFrom:0}% goes fainter";
        }
    }
}
