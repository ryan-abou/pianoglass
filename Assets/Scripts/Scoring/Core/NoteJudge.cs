using System;

namespace PianoGlass.Scoring
{
    /// <summary>Correct is wait mode's only hit (F22): the right key, with no
    /// timing to rate. Last, so older attempt files keep their numbers.</summary>
    public enum Judgement { Perfect, Great, Good, Miss, Correct }

    /// <summary>
    /// How far from a note's time a press may be, in real milliseconds either
    /// side, for each rating. Configurable because the right values for
    /// people learning piano are not known yet -- the per-note errors in the
    /// attempt files are what will settle them.
    /// </summary>
    [Serializable]
    public struct TimingWindows
    {
        public float perfectMs;
        public float greatMs;
        public float goodMs;

        public TimingWindows(float perfectMs, float greatMs, float goodMs)
        {
            this.perfectMs = perfectMs;
            this.greatMs = greatMs;
            this.goodMs = goodMs;
        }

        /// <summary>Learner-friendly starting point: looser than a rhythm
        /// game's expert windows, to be tightened once real errors are known.</summary>
        public static TimingWindows Default => new TimingWindows(40f, 80f, 150f);
    }

    /// <summary>
    /// One press against one note: the rating for a timing error. Pure, no
    /// state. Errors are in REAL milliseconds -- at half-speed practice a
    /// press 40 ms late in real time is only 20 ms of song time, and it is
    /// the player's real timing being judged.
    /// </summary>
    public static class NoteJudge
    {
        /// <summary>Signed real-time error: positive means late.</summary>
        public static double ErrorMs(double pressSongTime, double noteSongTime, double tempo) =>
            (pressSongTime - noteSongTime) * 1000.0 / Math.Max(tempo, 1e-6);

        /// <summary>A real-millisecond window as song seconds at this tempo.</summary>
        public static double WindowSongSeconds(double windowMs, double tempo) =>
            windowMs / 1000.0 * Math.Max(tempo, 1e-6);

        /// <summary>Window edges are inclusive: exactly 40 ms is still PERFECT.</summary>
        public static Judgement Judge(double errorMs, TimingWindows w)
        {
            double a = Math.Abs(errorMs);
            if (a <= w.perfectMs) return Judgement.Perfect;
            if (a <= w.greatMs) return Judgement.Great;
            if (a <= w.goodMs) return Judgement.Good;
            return Judgement.Miss;
        }
    }
}
