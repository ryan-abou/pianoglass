using System.Collections.Generic;
using NUnit.Framework;
using PianoGlass.Song;

namespace PianoGlass.Tests.EditMode
{
    /// <summary>
    /// ActiveNoteSelector is the only place PianoKeyHighlighter's
    /// ActiveKeysOnly/EdgeMarkers modes decide "is this key relevant yet" --
    /// and from the headset there's no way to tell by eye whether a key lit
    /// up exactly 0.5 s before its note, one frame early, or one frame late.
    /// These tests check the window's edges directly.
    /// </summary>
    public class ActiveNoteSelectorTests
    {
        [Test]
        public void NoteExactlyAtTheLookaheadBoundaryIsDueSoon()
        {
            Assert.IsTrue(ActiveNoteSelector.IsDueSoon(noteStartTime: 1.0, songTime: 0.5, lookaheadSeconds: 0.5f));
        }

        [Test]
        public void NoteJustOutsideTheLookaheadIsNotDueSoonYet()
        {
            Assert.IsFalse(ActiveNoteSelector.IsDueSoon(noteStartTime: 1.0, songTime: 0.49, lookaheadSeconds: 0.5f));
        }

        [Test]
        public void NoteExactlyAtItsStartTimeIsDueSoon()
        {
            // The window is half-open on the "already due" end too -- the
            // instant a note becomes due, its key should still be lit until
            // ActiveNoteSelector/Update's next read, even before MIDI can
            // possibly have reported it held.
            Assert.IsTrue(ActiveNoteSelector.IsDueSoon(noteStartTime: 1.0, songTime: 1.0, lookaheadSeconds: 0.5f));
        }

        [Test]
        public void NoteAlreadyUnderWayIsNotDueSoon()
        {
            // Due-soon is strictly upcoming. Once a note has started,
            // PianoKeyHighlighter relies on MidiInput.IsHeld, not this
            // lookahead, to keep a key lit -- see the class doc comment.
            Assert.IsFalse(ActiveNoteSelector.IsDueSoon(noteStartTime: 1.0, songTime: 1.0001, lookaheadSeconds: 0.5f));
        }

        [Test]
        public void NoteFarInTheFutureIsNotDueSoon()
        {
            Assert.IsFalse(ActiveNoteSelector.IsDueSoon(noteStartTime: 10.0, songTime: 0.0, lookaheadSeconds: 0.5f));
        }

        [Test]
        public void ZeroLookaheadOnlyMatchesTheInstantANoteIsDue()
        {
            Assert.IsTrue(ActiveNoteSelector.IsDueSoon(noteStartTime: 2.0, songTime: 2.0, lookaheadSeconds: 0f));
            Assert.IsFalse(ActiveNoteSelector.IsDueSoon(noteStartTime: 2.0, songTime: 1.999, lookaheadSeconds: 0f));
        }

        [Test]
        public void CollectDueSoonPitchesReturnsOnlyPitchesWithANoteInTheWindow()
        {
            var notes = new List<SongNote>
            {
                new SongNote(pitch: 60, startTime: 1.0, duration: 0.5),  // due soon at songTime 0.6
                new SongNote(pitch: 62, startTime: 5.0, duration: 0.5),  // far off
                new SongNote(pitch: 60, startTime: 5.0, duration: 0.5),  // same pitch, also far off
            };
            var result = new HashSet<int>();

            ActiveNoteSelector.CollectDueSoonPitches(notes, songTime: 0.6, lookaheadSeconds: 0.5f, result);

            Assert.AreEqual(1, result.Count);
            Assert.IsTrue(result.Contains(60));
        }

        [Test]
        public void CollectDueSoonPitchesClearsWhateverWasThereBefore()
        {
            var result = new HashSet<int> { 99 };

            ActiveNoteSelector.CollectDueSoonPitches(new List<SongNote>(), songTime: 0.0, lookaheadSeconds: 0.5f, result);

            Assert.IsEmpty(result);
        }

        [Test]
        public void CollectDueSoonPitchesDeduplicatesRepeatedPitches()
        {
            var notes = new List<SongNote>
            {
                new SongNote(pitch: 60, startTime: 1.0, duration: 0.25),
                new SongNote(pitch: 60, startTime: 1.2, duration: 0.25), // same pitch, also due soon
            };
            var result = new HashSet<int>();

            ActiveNoteSelector.CollectDueSoonPitches(notes, songTime: 0.9, lookaheadSeconds: 0.5f, result);

            Assert.AreEqual(1, result.Count);
        }
    }
}
