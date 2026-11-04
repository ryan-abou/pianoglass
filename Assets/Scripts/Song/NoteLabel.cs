namespace PianoGlass.Song
{
    /// <summary>
    /// The note-name label on a highway slab (F14): which name a pitch gets,
    /// and whether a slab is tall enough on the wall to carry one. Pure, so
    /// NoteHighway and the tests share one rule.
    ///
    /// Sharps only, never flats: one fixed name per key is easier to learn to
    /// read off the wall, and the key overlay has no notion of key signature
    /// to pick between them. Octave numbers are left off -- the slab already
    /// sits on its key, so "C4" says nothing "C" doesn't.
    /// </summary>
    public static class NoteLabel
    {
        /// <summary>MUSIC SHARP SIGN. LiberationSans has it, but only through
        /// TMP's dynamic fallback atlas; NoteHighway checks at runtime and
        /// uses <see cref="AsciiSharp"/> if the font cannot draw it.</summary>
        public const char SharpGlyph = '♯';
        public const char AsciiSharp = '#';

        private static readonly string[] Letters =
            { "C", "C", "D", "D", "E", "F", "F", "G", "G", "A", "A", "B" };
        private static readonly bool[] Sharp =
            { false, true, false, true, false, false, true, false, true, false, true, false };

        /// <summary>"C", "F♯" (or "F#" when <paramref name="sharpGlyph"/> is
        /// false) for a MIDI pitch. Any int is accepted, negatives included.</summary>
        public static string Name(int midiPitch, bool sharpGlyph = true)
        {
            int pc = ((midiPitch % 12) + 12) % 12;
            if (!Sharp[pc]) return Letters[pc];
            return Letters[pc] + (sharpGlyph ? SharpGlyph : AsciiSharp);
        }

        /// <summary>True when a slab of <paramref name="slabHeight"/> metres
        /// (along the wall) has room for a label <paramref name="labelHeight"/>
        /// tall with <paramref name="padding"/> above and below it. A short
        /// note, or a held note shrinking into the key line, loses its label
        /// rather than having it hang off the slab.</summary>
        public static bool Fits(float slabHeight, float labelHeight, float padding)
        {
            if (slabHeight <= 0f) return false;
            return slabHeight >= labelHeight + 2f * (padding < 0f ? 0f : padding);
        }

        /// <summary>Distance from the slab's bottom edge to the label's
        /// centre, along the wall: the label sits at the bottom, the end that
        /// reaches the key line, which is where the eye is when it is time to
        /// play.</summary>
        public static float CentreAboveBottom(float labelHeight, float padding) =>
            (padding < 0f ? 0f : padding) + labelHeight * 0.5f;
    }
}
