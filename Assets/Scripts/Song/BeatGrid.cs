using System;
using System.Collections.Generic;

namespace PianoGlass.Song
{
    /// <summary>One beat line on the highway.</summary>
    public readonly struct BeatLine
    {
        /// <summary>Song time of the beat, in seconds. Negative during the
        /// lead-in: those are the count-in beats.</summary>
        public readonly double Time;

        /// <summary>1..beatsPerBar -- what a player would count out loud.</summary>
        public readonly int BeatInBar;

        public BeatLine(double time, int beatInBar)
        {
            Time = time;
            BeatInBar = beatInBar;
        }

        /// <summary>Beat 1 of a bar is a bar line.</summary>
        public bool IsBarLine => BeatInBar == 1;

        /// <summary>Before the first note: part of the count-in.</summary>
        public bool IsCountIn => Time < 0.0;
    }

    /// <summary>
    /// Where the beats and bars of a song fall, on the same song-time axis as
    /// its notes. Pure, so the highway and the tests share one implementation.
    ///
    /// Beats are counted from song time 0 = beat 1 of bar 1, in both
    /// directions: the lead-in's beats are the same grid extended backwards,
    /// so the count-in lands on the song's own downbeat. A line's height on
    /// the wall is (Time - songTime) * speed -- exactly a note's bottom --
    /// so a note starting on a beat sits on its line all the way down.
    /// </summary>
    public static class BeatGrid
    {
        public static double SecondsPerBeat(float bpm) => bpm > 0f ? 60.0 / bpm : 0.0;

        /// <summary>1..beatsPerBar for beat index k (0 = the song's first beat;
        /// negative = lead-in).</summary>
        public static int BeatInBar(long k, int beatsPerBar)
        {
            int bpb = Math.Max(1, beatsPerBar);
            return (int)(((k % bpb) + bpb) % bpb) + 1;
        }

        /// <summary>
        /// Every beat from earliest to latest (inclusive, both in song
        /// seconds) that is on the wall at songTime: due no earlier than now
        /// and no later than leadTime from now. Clears into first. Returns
        /// the count.
        ///
        /// earliest is normally -leadInSeconds (so the count-in shows) and
        /// latest the song's Duration (so the final bar line shows, and the
        /// grid does not run on forever after the last note).
        /// </summary>
        public static int CollectVisible(
            float bpm, int beatsPerBar, double earliest, double latest,
            double songTime, double leadTime, List<BeatLine> into)
        {
            into.Clear();
            double spb = SecondsPerBeat(bpm);
            if (spb <= 0.0 || leadTime < 0.0) return 0;

            double from = Math.Max(songTime, earliest);
            double to = Math.Min(songTime + leadTime, latest);
            if (to < from) return 0;

            // A hair of tolerance so a beat exactly on either edge is kept,
            // not lost to rounding -- a line on the key line at the instant
            // its beat is due is the one that matters most.
            const double eps = 1e-9;
            long first = (long)Math.Ceiling(from / spb - eps);
            long last = (long)Math.Floor(to / spb + eps);

            for (long k = first; k <= last; k++)
                into.Add(new BeatLine(k * spb, BeatInBar(k, beatsPerBar)));
            return into.Count;
        }

        /// <summary>
        /// The count to show during the lead-in: the beat (1..beatsPerBar)
        /// most recently reached, or 0 when there is nothing to count --
        /// before the lead-in's first beat, or once the song has started.
        /// </summary>
        public static int CountInBeat(float bpm, int beatsPerBar, double earliest, double songTime)
        {
            double spb = SecondsPerBeat(bpm);
            if (spb <= 0.0 || songTime >= 0.0) return 0;

            const double eps = 1e-9;
            long firstBeat = (long)Math.Ceiling(earliest / spb - eps);
            long k = (long)Math.Floor(songTime / spb + eps);
            if (k < firstBeat) return 0;
            return BeatInBar(k, beatsPerBar);
        }
    }
}
