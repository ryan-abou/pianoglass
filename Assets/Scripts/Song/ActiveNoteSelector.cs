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
        /// How strongly a key due in <paramref name="timeUntilDue"/> seconds is
        /// lit, 0 to 1: nothing as it comes into the lookahead window, full as
        /// it is due. A key due in 20 ms and one due in a third of a second
        /// looked the same (Ryan, 2026-11-14); now the key fades in as its note
        /// comes, and the brightest key is the next one to play.
        /// </summary>
        public static float FadeIn(double timeUntilDue, float lookaheadSeconds)
        {
            if (lookaheadSeconds <= 0f) return 1f;
            double t = 1.0 - timeUntilDue / lookaheadSeconds;
            return t <= 0.0 ? 0f : t >= 1.0 ? 1f : (float)t;
        }

        /// <summary>
        /// Like <see cref="CollectDueSoonPitches"/>, but with how long until
        /// each pitch's soonest due note, for <see cref="FadeIn"/>. Reuses
        /// the dictionary: allocates nothing per frame.
        /// </summary>
        public static void CollectDueSoon(
            IReadOnlyList<SongNote> notes, double songTime, float lookaheadSeconds, Dictionary<int, double> untilDue,
            double from = double.NegativeInfinity, double until = double.PositiveInfinity,
            PracticeHand hand = PracticeHand.All)
        {
            untilDue.Clear();
            for (int i = 0; i < notes.Count; i++)
            {
                SongNote note = notes[i];
                if (!note.BelongsTo(hand) || note.StartTime < from - 1e-9 || note.StartTime >= until - 1e-9 ||
                    !IsDueSoon(note.StartTime, songTime, lookaheadSeconds))
                    continue;
                double t = note.StartTime - songTime;
                if (!untilDue.TryGetValue(note.Pitch, out double soonest) || t < soonest) untilDue[note.Pitch] = t;
            }
        }

        /// <summary>
        /// Fills `results` with every distinct pitch that has a note due soon
        /// at songTime. Takes the output set rather than returning a new one
        /// so PianoKeyHighlighter's per-frame call in ActiveKeysOnly/
        /// EdgeMarkers mode allocates nothing -- it reuses the same set every
        /// frame, the same reasoning as not spawning GameObjects every frame.
        /// <paramref name="from"/> and <paramref name="until"/> limit it to the
        /// notes starting in that span: a practice loop's bars.
        /// <paramref name="hand"/> limits it to the notes being practiced:
        /// the other hand's keys never light.
        /// </summary>
        public static void CollectDueSoonPitches(
            IReadOnlyList<SongNote> notes, double songTime, float lookaheadSeconds, HashSet<int> results,
            double from = double.NegativeInfinity, double until = double.PositiveInfinity,
            PracticeHand hand = PracticeHand.All)
        {
            results.Clear();
            for (int i = 0; i < notes.Count; i++)
            {
                SongNote note = notes[i];
                if (note.BelongsTo(hand) && note.StartTime >= from - 1e-9 && note.StartTime < until - 1e-9 &&
                    IsDueSoon(note.StartTime, songTime, lookaheadSeconds))
                    results.Add(note.Pitch);
            }
        }
    }
}
