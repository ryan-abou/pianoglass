namespace PianoGlass.Song
{
    /// <summary>
    /// How much of a wait-mode pass came from memory. Wait mode has no clock to
    /// beat, so a player could reach every note by trying keys or by waiting for
    /// it to be shown, and right keys alone would not tell that apart from
    /// knowing it. Memory fade therefore moves on this instead: a stop (one note
    /// or chord) counts only if every key was right first time and it never had
    /// to be shown.
    /// </summary>
    public sealed class WaitRecall
    {
        public int Clean { get; private set; }
        public int Total { get; private set; }

        /// <summary>Share of stops played from memory, 0 to 100.</summary>
        public double Percent => Total > 0 ? 100.0 * Clean / Total : 0.0;

        private double _stop;
        private bool _open;
        private bool _neededHelp;

        /// <summary>The clock is holding at this note. True when that is a new
        /// stop, which closes the last one.</summary>
        public bool AtStop(double dueTime)
        {
            if (_open && System.Math.Abs(dueTime - _stop) < 1e-6) return false;
            Finish();
            _stop = dueTime;
            _open = true;
            _neededHelp = false;
            return true;
        }

        /// <summary>A wrong key, or the note had to be shown or brought back.</summary>
        public void NeededHelp()
        {
            if (_open) _neededHelp = true;
        }

        /// <summary>Close the stop in progress: the pass has ended.</summary>
        public void Finish()
        {
            if (!_open) return;
            Total++;
            if (!_neededHelp) Clean++;
            _open = false;
        }

        public void Reset()
        {
            Clean = Total = 0;
            _open = false;
            _neededHelp = false;
        }
    }
}
