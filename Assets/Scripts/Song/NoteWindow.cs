using System.Collections.Generic;

namespace PianoGlass.Song
{
    /// <summary>
    /// Finds the notes that can be on the highway right now without looking
    /// at every note in the song, so a long song costs no more per frame than
    /// a short one. Pure and tested; NoteHighway uses it to pick which notes
    /// get a slab this frame.
    /// </summary>
    public sealed class NoteWindow
    {
        private readonly int[] _order;
        private readonly double[] _starts;
        // The latest end of any note up to this one in start order. It only
        // ever goes up, so it can be searched even though the ends can't.
        private readonly double[] _latestEnd;

        public NoteWindow(IReadOnlyList<SongNote> notes)
        {
            int count = notes.Count;
            _order = new int[count];
            for (int i = 0; i < count; i++) _order[i] = i;
            System.Array.Sort(_order, (a, b) =>
            {
                int c = notes[a].StartTime.CompareTo(notes[b].StartTime);
                return c != 0 ? c : a.CompareTo(b);
            });

            _starts = new double[count];
            _latestEnd = new double[count];
            double latest = double.NegativeInfinity;
            for (int i = 0; i < count; i++)
            {
                SongNote note = notes[_order[i]];
                latest = System.Math.Max(latest, note.EndTime);
                _starts[i] = note.StartTime;
                _latestEnd[i] = latest;
            }
        }

        /// <summary>The index in the song's notes of the i-th note by start time.</summary>
        public int NoteAt(int i) => _order[i];

        /// <summary>Which notes could be showing at this song time: from
        /// <paramref name="from"/> up to but not including <paramref name="to"/>.
        /// Every note that is showing is in the range. A few inside it may have
        /// ended, so each one still gets its own check.</summary>
        public void Range(double songTime, double leadTime, out int from, out int to)
        {
            from = FirstWhere(_latestEnd, songTime);
            to = FirstAfter(_starts, songTime + leadTime);
        }

        private static int FirstWhere(double[] rising, double atLeast)
        {
            int lo = 0, hi = rising.Length;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (rising[mid] >= atLeast) hi = mid;
                else lo = mid + 1;
            }
            return lo;
        }

        private static int FirstAfter(double[] rising, double value)
        {
            int lo = 0, hi = rising.Length;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (rising[mid] > value) hi = mid;
                else lo = mid + 1;
            }
            return lo;
        }
    }
}
