using System.Collections.Generic;
using NUnit.Framework;
using PianoGlass.Song;

namespace PianoGlass.Tests.EditMode
{
    /// <summary>
    /// Twinkle's numbers throughout: 120 BPM (0.5 s a beat), 4/4, a 3 s
    /// lead-in (6 beats), 16 s long, highway lead time 3 s.
    /// </summary>
    public class BeatGridTests
    {
        private const float Bpm = 120f;
        private const int Bpb = 4;
        private const double LeadIn = 3.0, Duration = 16.0, Lead = 3.0;

        private readonly List<BeatLine> _lines = new List<BeatLine>();

        private int Collect(double songTime) =>
            BeatGrid.CollectVisible(Bpm, Bpb, -LeadIn, Duration, songTime, Lead, _lines);

        [Test]
        public void BeatInBarCountsOneToFourInBothDirections()
        {
            Assert.AreEqual(1, BeatGrid.BeatInBar(0, 4));
            Assert.AreEqual(4, BeatGrid.BeatInBar(3, 4));
            Assert.AreEqual(1, BeatGrid.BeatInBar(4, 4));
            Assert.AreEqual(4, BeatGrid.BeatInBar(-1, 4));
            Assert.AreEqual(1, BeatGrid.BeatInBar(-4, 4));
            Assert.AreEqual(3, BeatGrid.BeatInBar(-6, 4));
        }

        [Test]
        public void AtLeadInStartTheWallShowsTheCountInAndTheFirstDownbeat()
        {
            // songTime -3: window [-3, 0] holds the 6 count-in beats and the
            // song's own downbeat on the very top of the wall.
            Assert.AreEqual(7, Collect(-3.0));
            Assert.AreEqual(-3.0, _lines[0].Time, 1e-12);
            Assert.AreEqual(0.0, _lines[6].Time, 1e-12);
            Assert.IsTrue(_lines[0].IsCountIn);
            Assert.IsFalse(_lines[6].IsCountIn);
            Assert.IsTrue(_lines[6].IsBarLine);
        }

        [Test]
        public void CountInBarLineIsOneFullBarBeforeTheSong()
        {
            Collect(-3.0);
            BeatLine bar = _lines.Find(l => l.IsCountIn && l.IsBarLine);
            Assert.AreEqual(-2.0, bar.Time, 1e-12); // 4 beats x 0.5 s
        }

        [Test]
        public void MidSongWindowHasSevenBeatsAndTwoBarLines()
        {
            // Window [5, 8]: beats at 5.0, 5.5 ... 8.0; bars start every 2 s.
            Assert.AreEqual(7, Collect(5.0));
            Assert.AreEqual(2, _lines.FindAll(l => l.IsBarLine).Count); // 6.0, 8.0
            Assert.IsTrue(_lines.TrueForAll(l => !l.IsCountIn));
        }

        [Test]
        public void ABeatExactlyDueIsStillOnTheKeyLine()
        {
            Collect(4.0);
            Assert.AreEqual(4.0, _lines[0].Time, 1e-12);
        }

        [Test]
        public void GridStopsAtTheFinalBarLine()
        {
            Assert.AreEqual(3, Collect(15.0)); // 15.0, 15.5, 16.0
            Assert.AreEqual(16.0, _lines[2].Time, 1e-12);
            Assert.IsTrue(_lines[2].IsBarLine);
            Assert.AreEqual(0, Collect(16.5));
        }

        [Test]
        public void NothingBeforeTheLeadInStarts()
        {
            // A clock somehow earlier than -leadIn draws no extra beats.
            Collect(-5.0);
            Assert.IsTrue(_lines.TrueForAll(l => l.Time >= -LeadIn));
        }

        [Test]
        public void BadTempoDrawsNothing()
        {
            Assert.AreEqual(0, BeatGrid.CollectVisible(0f, 4, -3, 16, 0, 3, _lines));
            Assert.AreEqual(0, BeatGrid.CountInBeat(0f, 4, -3, -1));
        }

        [TestCase(-3.0, 3)]
        [TestCase(-2.6, 3)]
        [TestCase(-2.5, 4)]
        [TestCase(-2.0, 1)]
        [TestCase(-1.9, 1)]
        [TestCase(-1.5, 2)]
        [TestCase(-1.0, 3)]
        [TestCase(-0.5, 4)]
        [TestCase(-0.01, 4)]
        [TestCase(0.0, 0)]
        [TestCase(2.0, 0)]
        [TestCase(-3.5, 0)]
        public void CountInBeatCountsIntoTheDownbeat(double songTime, int expected)
        {
            Assert.AreEqual(expected, BeatGrid.CountInBeat(Bpm, Bpb, -LeadIn, songTime));
        }
    }
}
