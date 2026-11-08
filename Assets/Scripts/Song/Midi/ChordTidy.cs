using System.Collections.Generic;

namespace PianoGlass.Song
{
    public sealed class ChordTidyResult
    {
        public readonly List<SongNote> Notes;
        /// <summary>Notes that moved to start with their chord.</summary>
        public readonly int Moved;
        /// <summary>Copies of a key at one moment that became one note.</summary>
        public readonly int Merged;

        public ChordTidyResult(List<SongNote> notes, int moved, int merged)
        {
            Notes = notes;
            Moved = moved;
            Merged = merged;
        }
    }

    /// <summary>
    /// Cleans up the notes of a file once, after hands are decided and ticks
    /// have become seconds. Real files are messy, and the scoring only treats
    /// notes as one chord when they start at exactly the same time.
    ///
    ///   1. A note starting within 40 ms of a chord's first note joins that
    ///      chord and moves to start with it. Measured from the first note,
    ///      so a fast run can't chain into one giant chord. Both hands are
    ///      looked at together, and the times are the file's own seconds, so
    ///      the tempo setting doesn't change what counts as a chord.
    ///   2. The same key at the same moment becomes one note with the longest
    ///      length. Copies from two hands become one shared note, so neither
    ///      hand loses it.
    ///
    /// Why 40 ms: one chord in a recording lands a few tens of milliseconds
    /// apart, and a real 32nd note at 120 BPM is 62.5 ms from the next.
    /// </summary>
    public static class ChordTidy
    {
        public const double ChordWindowSeconds = 0.040;

        private const double SameTime = 1e-9;

        public static ChordTidyResult Run(IReadOnlyList<SongNote> notes)
        {
            var order = new List<int>(notes.Count);
            for (int i = 0; i < notes.Count; i++) order.Add(i);
            order.Sort((a, b) =>
            {
                int c = notes[a].StartTime.CompareTo(notes[b].StartTime);
                return c != 0 ? c : a.CompareTo(b);
            });

            var tidy = new List<SongNote>(notes.Count);
            var copyOfPitch = new Dictionary<int, int>();
            int moved = 0, merged = 0;
            double chordStart = double.NegativeInfinity;

            foreach (int index in order)
            {
                SongNote note = notes[index];

                // "Within 40 ms" includes 40 itself, whatever rounding did to it.
                if (note.StartTime - chordStart > ChordWindowSeconds + SameTime)
                {
                    chordStart = note.StartTime;
                    copyOfPitch.Clear();
                }
                else if (note.StartTime - chordStart > SameTime)
                {
                    moved++;
                }

                if (copyOfPitch.TryGetValue(note.Pitch, out int copy))
                {
                    SongNote kept = tidy[copy];
                    double longest = System.Math.Max(kept.Duration, note.Duration);
                    tidy[copy] = new SongNote(note.Pitch, chordStart, longest, Combine(kept.Hand, note.Hand));
                    merged++;
                    continue;
                }

                copyOfPitch[note.Pitch] = tidy.Count;
                tidy.Add(new SongNote(note.Pitch, chordStart, note.Duration, note.Hand));
            }

            return new ChordTidyResult(tidy, moved, merged);
        }

        private static NoteHand Combine(NoteHand a, NoteHand b)
        {
            if (a == b) return a;
            if (a == NoteHand.None) return b;
            if (b == NoteHand.None) return a;
            return NoteHand.Shared;
        }
    }
}
