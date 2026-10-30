using System;
using System.Collections.Generic;

namespace PianoGlass.Song
{
    /// <summary>One beat line on the highway.</summary>
    public readonly struct BeatLine
    {
        /// <summary>Song time of the beat, in seconds. Negative during the
        /// lead-in.</summary>
        public readonly double Time;

        /// <summary>1..beatsPerBar -- what a player would count out loud.</summary>
        public readonly int BeatInBar;

        /// <summary>1-based bar this beat belongs to, matching SongData.BarOf.
        /// Zero or negative during the lead-in, where there is no bar to name.</summary>
        public readonly int Bar;

        public BeatLine(double time, int beatInBar, int bar)
        {
            Time = time;
            BeatInBar = beatInBar;
            Bar = bar;
        }

        /// <summary>Beat 1 of a bar is a bar line.</summary>
        public bool IsBarLine => BeatInBar == 1;
    }

    /// <summary>
    /// Where the beats and bars of a song fall, on the same song-time axis as
    /// its notes. Pure, so the highway and the tests share one implementation.
    ///
    /// Beats are counted from song time 0 = beat 1 of bar 1, in both
    /// directions: the lead-in's beats are the same grid extended backwards,
    /// so the wall is already moving in time with the song before the first
    /// note. A line's height on the wall is (Time - songTime) * speed --
    /// exactly a note's bottom -- so a note starting on a beat sits on its
    /// line all the way down.
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

        /// <summary>1-based bar for beat index k: beats 0..bpb-1 are bar 1,
        /// the lead-in's last bar is 0.</summary>
        public static int BarOf(long k, int beatsPerBar)
        {
            int bpb = Math.Max(1, beatsPerBar);
            return (int)Math.Floor((double)k / bpb) + 1;
        }

        /// <summary>
        /// Every beat from earliest to latest (inclusive, both in song
        /// seconds) that is on the wall at songTime: due no earlier than now
        /// and no later than leadTime from now. Clears into first. Returns
        /// the count.
        ///
        /// earliest is normally -leadInSeconds (so the lead-in has lines
        /// moving down the wall) and latest the song's Duration (so the final
        /// bar line shows, and the grid does not run on forever after the
        /// last note).
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
                into.Add(new BeatLine(k * spb, BeatInBar(k, beatsPerBar), BarOf(k, beatsPerBar)));
            return into.Count;
        }

        /// <summary>
        /// The notes that get a lane line on the highway: every C in
        /// [lowestNote, highestNote], one line per octave on the C's left
        /// edge -- the B|C boundary, which is where the reference video puts
        /// them. Clears into first. Returns the count.
        /// </summary>
        public static int LaneNotes(int lowestNote, int highestNote, List<int> into)
        {
            into.Clear();
            int firstC = lowestNote + ((12 - lowestNote % 12) % 12);
            for (int n = firstC; n <= highestNote; n += 12)
                into.Add(n);
            return into.Count;
        }
    }
}
