using System.Collections.Generic;
using NUnit.Framework;
using PianoGlass.Song;

namespace PianoGlass.Tests.EditMode
{
    public class ChordTidyTests
    {
        private const double Eps = 1e-9;
        private const int C4 = 60, E4 = 64, G4 = 67, B4 = 71;

        private static SongNote N(int pitch, double start, double length = 0.5, NoteHand hand = NoteHand.Right) =>
            new SongNote(pitch, start, length, hand);

        private static int CountForHand(List<SongNote> notes, PracticeHand hand)
        {
            int count = 0;
            foreach (SongNote n in notes) if (n.BelongsTo(hand)) count++;
            return count;
        }

        [Test]
        public void NotesThirtyNineMillisecondsApartBecomeOneChord()
        {
            ChordTidyResult r = ChordTidy.Run(new[] { N(C4, 1.0), N(E4, 1.039) });
            Assert.AreEqual(2, r.Notes.Count);
            Assert.AreEqual(1.0, r.Notes[0].StartTime, Eps);
            Assert.AreEqual(1.0, r.Notes[1].StartTime, Eps, "starts exactly with the first note");
            Assert.AreEqual(1, r.Moved);
            Assert.AreEqual(0, r.Merged);
        }

        [Test]
        public void NotesFortyOneMillisecondsApartStaySeparate()
        {
            ChordTidyResult r = ChordTidy.Run(new[] { N(C4, 1.0), N(E4, 1.041) });
            Assert.AreEqual(1.0, r.Notes[0].StartTime, Eps);
            Assert.AreEqual(1.041, r.Notes[1].StartTime, Eps);
            Assert.AreEqual(0, r.Moved);
        }

        [Test]
        public void ExactlyFortyMillisecondsCounts()
        {
            ChordTidyResult r = ChordTidy.Run(new[] { N(C4, 1.0), N(E4, 1.04) });
            Assert.AreEqual(1.0, r.Notes[1].StartTime, Eps);
        }

        [Test]
        public void AMovedNoteKeepsItsLength()
        {
            ChordTidyResult r = ChordTidy.Run(new[] { N(C4, 1.0, 0.5), N(E4, 1.02, 0.7) });
            Assert.AreEqual(0.7, r.Notes[1].Duration, Eps);
        }

        [Test]
        public void AFastRunCannotChainIntoOneChord()
        {
            ChordTidyResult r = ChordTidy.Run(new[]
            {
                N(C4, 0.00), N(E4, 0.03), N(G4, 0.06), N(B4, 0.09),
            });

            Assert.AreEqual(0.00, r.Notes[0].StartTime, Eps);
            Assert.AreEqual(0.00, r.Notes[1].StartTime, Eps, "30 ms from the first note");
            Assert.AreEqual(0.06, r.Notes[2].StartTime, Eps, "60 ms from the first note: a new chord");
            Assert.AreEqual(0.06, r.Notes[3].StartTime, Eps);
            Assert.AreEqual(2, r.Moved);
        }

        [Test]
        public void AChordSplitBetweenTheHandsIsOneChord()
        {
            ChordTidyResult r = ChordTidy.Run(new[]
            {
                N(C4, 2.0, hand: NoteHand.Left),
                N(G4, 2.02, hand: NoteHand.Right),
            });
            Assert.AreEqual(r.Notes[0].StartTime, r.Notes[1].StartTime, 0.0);
            Assert.AreEqual(NoteHand.Left, r.Notes[0].Hand);
            Assert.AreEqual(NoteHand.Right, r.Notes[1].Hand);
        }

        [Test]
        public void TheSameKeyTwiceBecomesOneNoteWithTheLongerLength()
        {
            ChordTidyResult r = ChordTidy.Run(new[] { N(C4, 1.0, 0.25), N(C4, 1.0, 0.75) });
            Assert.AreEqual(1, r.Notes.Count);
            Assert.AreEqual(0.75, r.Notes[0].Duration, Eps);
            Assert.AreEqual(NoteHand.Right, r.Notes[0].Hand);
            Assert.AreEqual(1, r.Merged);
        }

        [Test]
        public void AKeyThatMovesOntoItsCopyAlsoMerges()
        {
            ChordTidyResult r = ChordTidy.Run(new[] { N(C4, 1.0, 0.25), N(C4, 1.01, 0.75) });
            Assert.AreEqual(1, r.Notes.Count);
            Assert.AreEqual(1.0, r.Notes[0].StartTime, Eps);
            Assert.AreEqual(0.75, r.Notes[0].Duration, Eps);
            Assert.AreEqual(1, r.Moved);
            Assert.AreEqual(1, r.Merged);
        }

        [Test]
        public void CopiesFromTwoHandsBecomeOneSharedNote()
        {
            var input = new[]
            {
                N(C4, 1.0, 0.25, NoteHand.Left),
                N(C4, 1.0, 0.5, NoteHand.Right),
                N(E4, 2.0, 0.5, NoteHand.Left),
                N(G4, 2.0, 0.5, NoteHand.Right),
            };

            ChordTidyResult r = ChordTidy.Run(input);

            Assert.AreEqual(3, r.Notes.Count);
            Assert.AreEqual(NoteHand.Shared, r.Notes[0].Hand);
            Assert.AreEqual(0.5, r.Notes[0].Duration, Eps);
            Assert.AreEqual(2, CountForHand(r.Notes, PracticeHand.Left), "the left hand keeps both its notes");
            Assert.AreEqual(2, CountForHand(r.Notes, PracticeHand.Right), "the right hand keeps both its notes");
        }

        [Test]
        public void CopiesFromAHandsUnknownSongAreJustOneNote()
        {
            ChordTidyResult r = ChordTidy.Run(new[]
            {
                N(C4, 1.0, 0.25, NoteHand.None), N(C4, 1.0, 0.5, NoteHand.None),
            });
            Assert.AreEqual(1, r.Notes.Count);
            Assert.AreEqual(NoteHand.None, r.Notes[0].Hand);
        }

        [Test]
        public void TheSameKeyAtDifferentMomentsStaysTwoNotes()
        {
            ChordTidyResult r = ChordTidy.Run(new[] { N(C4, 1.0), N(C4, 1.5) });
            Assert.AreEqual(2, r.Notes.Count);
            Assert.AreEqual(0, r.Merged);
        }

        [Test]
        public void AFileWithNoCloseNotesComesOutExactlyAsItWentIn()
        {
            var input = new[]
            {
                N(C4, 0.0, 0.5, NoteHand.Right),
                N(E4, 0.0, 0.25, NoteHand.Left),
                N(G4, 0.5, 0.5, NoteHand.Right),
                N(C4, 1.0, 0.123, NoteHand.Left),
                N(B4, 1.25, 1.0, NoteHand.Right),
            };

            ChordTidyResult r = ChordTidy.Run(input);

            Assert.AreEqual(0, r.Moved);
            Assert.AreEqual(0, r.Merged);
            Assert.AreEqual(input.Length, r.Notes.Count);
            for (int i = 0; i < input.Length; i++)
            {
                Assert.AreEqual(input[i].Pitch, r.Notes[i].Pitch);
                Assert.AreEqual(input[i].StartTime, r.Notes[i].StartTime, 0.0);
                Assert.AreEqual(input[i].Duration, r.Notes[i].Duration, 0.0);
                Assert.AreEqual(input[i].Hand, r.Notes[i].Hand);
            }
        }

        [Test]
        public void NoNotesIsFine()
        {
            Assert.AreEqual(0, ChordTidy.Run(new SongNote[0]).Notes.Count);
        }
    }
}
