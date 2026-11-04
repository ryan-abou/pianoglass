using System;

namespace PianoGlass.Song
{
    public enum ChordAction { None, Restart, Slower, Faster, ToggleMetronome }

    /// <summary>
    /// The piano-key transport chords, all built on the bottom key (A0) so
    /// none is reachable by accident and no song note is spent on them:
    ///
    ///   A0 + A#0            restart from the top and play (either key second)
    ///   hold A0, press B0   tempo -5%
    ///   hold A0, press C1   tempo +5%
    ///   hold A0, press C#1  metronome on/off (Ryan, 2026-11-03)
    ///
    /// A0 alone does nothing. For tempo the step key must be the one pressed
    /// while A0 is already down, so every press of B0 or C1 is one step.
    /// Whether an action is allowed now (calibrated; stopped or paused for
    /// tempo) is SongTransport's call; this only says which chord was played.
    /// </summary>
    public static class TransportChord
    {
        public const int A0 = 21, ASharp0 = 22, B0 = 23, C1 = 24, CSharp1 = 25;

        /// <param name="pressed">The key that just went down.</param>
        /// <param name="isHeld">Whether a key is down now. Only ever asked
        /// about the other key of the chord, never the pressed one.</param>
        public static ChordAction OnNoteOn(int pressed, Func<int, bool> isHeld,
                                           int hold = A0, int restart = ASharp0, int slower = B0, int faster = C1,
                                           int metronome = CSharp1)
        {
            if (isHeld == null) return ChordAction.None;
            if (pressed == hold) return isHeld(restart) ? ChordAction.Restart : ChordAction.None;
            if (pressed == restart) return isHeld(hold) ? ChordAction.Restart : ChordAction.None;
            if (pressed == slower) return isHeld(hold) ? ChordAction.Slower : ChordAction.None;
            if (pressed == faster) return isHeld(hold) ? ChordAction.Faster : ChordAction.None;
            if (pressed == metronome) return isHeld(hold) ? ChordAction.ToggleMetronome : ChordAction.None;
            return ChordAction.None;
        }
    }
}
