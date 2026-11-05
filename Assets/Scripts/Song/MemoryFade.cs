namespace PianoGlass.Song
{
    /// <summary>
    /// Memory fade (F36): a practice loop's notes fade pass by pass, until the
    /// player is playing the bars from memory. Pure, so the steps are tested.
    ///
    /// The opacity is a ceiling on everything that would show the note
    /// (Ryan, 2026-11-05): a note still turns mint or slate once it is judged,
    /// at the opacity it already had, so a half-faded note shows half-visible
    /// mint and a hidden note stays hidden. See OutcomeTint.FadedSlab.
    /// </summary>
    public static class MemoryFade
    {
        private static readonly float[] Steps = { 1f, 0.6f, 0.3f, 0f };

        /// <summary>How visible a note is on this 1-based pass: 100%, 60%,
        /// 30%, then hidden for every pass after.</summary>
        public static float Opacity(int pass)
        {
            if (pass <= 1) return Steps[0];
            return Steps[System.Math.Min(pass - 1, Steps.Length - 1)];
        }
    }
}
