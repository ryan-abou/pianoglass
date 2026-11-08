using NUnit.Framework;
using PianoGlass.Song;

namespace PianoGlass.Tests.EditMode
{
    public class HandAssignerTests
    {
        private static TrackHandInfo T(string name, double pitch = 60.0) => new TrackHandInfo(name, pitch);

        [TestCase("Left Hand", NoteHand.Left)]
        [TestCase("LEFT", NoteHand.Left)]
        [TestCase("Piano bass", NoteHand.Left)]
        [TestCase("LH", NoteHand.Left)]
        [TestCase("Piano_lh", NoteHand.Left)]
        [TestCase("Right Hand", NoteHand.Right)]
        [TestCase("treble staff", NoteHand.Right)]
        [TestCase("rh", NoteHand.Right)]
        [TestCase("Piano RH 1", NoteHand.Right)]
        [TestCase("Rhythm", NoteHand.None)]
        [TestCase("Bohemian", NoteHand.None)]
        [TestCase("Piano", NoteHand.None)]
        [TestCase("", NoteHand.None)]
        [TestCase(null, NoteHand.None)]
        [TestCase("left and right", NoteHand.None)]
        public void ANameSaysAHandOrNothing(string name, NoteHand expected)
        {
            Assert.AreEqual(expected, HandAssigner.HandFromName(name));
        }

        [Test]
        public void NamedTracksAreUsedEvenAgainstThePitches()
        {
            // The "right" track is the lower one: the name still wins.
            HandAssignment a = HandAssigner.Assign(new[] { T("Right", 40), T("Left", 80) });
            Assert.AreEqual(HandsStatus.Named, a.Status);
            Assert.AreEqual(NoteHand.Right, a.Hands[0]);
            Assert.AreEqual(NoteHand.Left, a.Hands[1]);
        }

        [Test]
        public void TwoUnnamedTracksAreGuessedFromAveragePitch()
        {
            HandAssignment a = HandAssigner.Assign(new[] { T("Piano 1", 50), T("Piano 2", 70) });
            Assert.AreEqual(HandsStatus.Guessed, a.Status);
            Assert.AreEqual(NoteHand.Left, a.Hands[0]);
            Assert.AreEqual(NoteHand.Right, a.Hands[1]);
        }

        [Test]
        public void RhythmIsNotARightHandSoTwoSuchTracksAreGuessed()
        {
            HandAssignment a = HandAssigner.Assign(new[] { T("Rhythm", 72), T("Rhythm 2", 48) });
            Assert.AreEqual(HandsStatus.Guessed, a.Status);
            Assert.AreEqual(NoteHand.Right, a.Hands[0]);
        }

        [Test]
        public void ANameIsNeverMixedWithAGuess()
        {
            // One name only: the name isn't used, even where it disagrees.
            HandAssignment a = HandAssigner.Assign(new[] { T("Left Hand", 80), T("Piano", 40) });
            Assert.AreEqual(HandsStatus.Guessed, a.Status);
            Assert.AreEqual(NoteHand.Right, a.Hands[0], "the higher track, whatever its name says");
        }

        [Test]
        public void TwoTracksNamedForOneHandAreGuessed()
        {
            HandAssignment a = HandAssigner.Assign(new[] { T("Right 1", 80), T("Right 2", 40) });
            Assert.AreEqual(HandsStatus.Guessed, a.Status);
        }

        [Test]
        public void OneTrackIsUnknownEvenWithAName()
        {
            HandAssignment a = HandAssigner.Assign(new[] { T("Right Hand") });
            Assert.AreEqual(HandsStatus.Unknown, a.Status);
            Assert.AreEqual(NoteHand.None, a.Hands[0]);
        }

        [Test]
        public void ThreeNamedTracksAreNamed()
        {
            HandAssignment a = HandAssigner.Assign(new[] { T("RH upper"), T("RH lower"), T("LH") });
            Assert.AreEqual(HandsStatus.Named, a.Status);
            Assert.AreEqual(NoteHand.Right, a.Hands[1]);
            Assert.AreEqual(NoteHand.Left, a.Hands[2]);
        }

        [Test]
        public void ThreeTracksWithOneUnnamedAreUnknown()
        {
            HandAssignment a = HandAssigner.Assign(new[] { T("Right"), T("Left"), T("Strings") });
            Assert.AreEqual(HandsStatus.Unknown, a.Status);
            foreach (NoteHand hand in a.Hands) Assert.AreEqual(NoteHand.None, hand);
        }

        [Test]
        public void NoTracksIsUnknown()
        {
            Assert.AreEqual(HandsStatus.Unknown, HandAssigner.Assign(new TrackHandInfo[0]).Status);
        }

        [Test]
        public void SwapFlipsNamedHands()
        {
            HandAssignment a = HandAssigner.Assign(new[] { T("Right"), T("Left") }, swap: true);
            Assert.AreEqual(HandsStatus.Named, a.Status);
            Assert.AreEqual(NoteHand.Left, a.Hands[0]);
            Assert.AreEqual(NoteHand.Right, a.Hands[1]);
        }

        [Test]
        public void SwapFlipsGuessedHands()
        {
            HandAssignment a = HandAssigner.Assign(new[] { T("A", 40), T("B", 80) }, swap: true);
            Assert.AreEqual(HandsStatus.Guessed, a.Status);
            Assert.AreEqual(NoteHand.Right, a.Hands[0]);
            Assert.AreEqual(NoteHand.Left, a.Hands[1]);
        }

        [Test]
        public void SwapLeavesUnknownAlone()
        {
            HandAssignment a = HandAssigner.Assign(new[] { T("Piano") }, swap: true);
            Assert.AreEqual(HandsStatus.Unknown, a.Status);
            Assert.AreEqual(NoteHand.None, a.Hands[0]);
        }

        [Test]
        public void SwapLeavesSharedShared()
        {
            Assert.AreEqual(NoteHand.Shared, HandAssigner.Swap(NoteHand.Shared));
            Assert.AreEqual(NoteHand.None, HandAssigner.Swap(NoteHand.None));
        }
    }
}
