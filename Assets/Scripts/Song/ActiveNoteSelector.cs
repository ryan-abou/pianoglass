using System.Collections.Generic;

namespace PianoGlass.Song
{
    /// <summary>
    /// "Which key needs to be shown right now" for PianoKeyHighlighter's
    /// ActiveKeysOnly and EdgeMarkers display modes, pulled out the same way
    /// SlabGeometry was pulled out of NoteHighway: so it can be tested without
    /// a MonoBehaviour, a scene, or a MIDI connection, and so the highlighter
    /// and the tests can never drift apart on what "due soon" means.
    /// </summary>
    public static class ActiveNoteSelector
    {
        /// <summary>
        /// True once a note is within lookaheadSeconds of its StartTime and
        /// not yet due -- the window PianoKeyHighlighter uses to show a key
        /// before the player needs it, half-open on both ends so a note
        /// exactly lookaheadSeconds away counts and one exactly due (songTime
        /// == StartTime) still counts too.
        ///
        /// A note already under way (songTime past StartTime) deliberately
        /// falls back out of this: once a note has started, whether its key
        /// is shown comes from whether the real key is actually held (see
        /// PianoKeyHighlighter.Update), not from this lookahead.
        /// </summary>
        public static bool IsDueSoon(double noteStartTime, double songTime, float lookaheadSeconds)
        {
            double timeUntilDue = noteStartTime - songTime;
            return timeUntilDue >= 0.0 && timeUntilDue <= lookaheadSeconds;
        }

        /// <summary>
        /// Fills `results` with every distinct pitch that has a note due soon
        /// at songTime. Takes the output set rather than returning a new one
        /// so PianoKeyHighlighter's per-frame call in ActiveKeysOnly/
        /// EdgeMarkers mode allocates nothing -- it reuses the same set every
        /// frame, the same reasoning as not spawning GameObjects every frame.
        /// </summary>
        public static void CollectDueSoonPitches(
            IEnumerable<SongNote> notes, double songTime, float lookaheadSeconds, HashSet<int> results)
        {
            results.Clear();
            foreach (SongNote note in notes)
                if (IsDueSoon(note.StartTime, songTime, lookaheadSeconds))
                    results.Add(note.Pitch);
        }
    }
}
