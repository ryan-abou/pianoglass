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
        private readonly List<int> _lanes = new List<int>();

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

        [TestCase(0, 1)]
        [TestCase(3, 1)]
        [TestCase(4, 2)]
        [TestCase(28, 8)]
        [TestCase(-1, 0)]
        [TestCase(-4, 0)]
        [TestCase(-5, -1)]
        public void BarOfCountsFromOneAtTheSongsFirstBeat(long k, int expected)
        {
            Assert.AreEqual(expected, BeatGrid.BarOf(k, 4));
        }

        [Test]
        public void BarNumbersMatchSongDataBarOf()
        {
            // The tag on the wall and the bar the results blame must agree.
            SongData song = SongData.TwinkleTwinkleLittleStar();
            for (double t = 0.0; t <= Duration - 2.0; t += 0.5)
            {
                Collect(t);
                Assert.AreEqual(song.BarOf(_lines[0].Time), _lines[0].Bar, $"t={t}");
            }
        }

        [Test]
        public void AtLeadInStartTheWallShowsTheLeadInAndTheFirstDownbeat()
        {
            // songTime -3: window [-3, 0] holds the 6 lead-in beats and the
            // song's own downbeat on the very top of the wall.
            Assert.AreEqual(7, Collect(-3.0));
            Assert.AreEqual(-3.0, _lines[0].Time, 1e-12);
            Assert.AreEqual(0.0, _lines[6].Time, 1e-12);
            Assert.IsTrue(_lines[6].IsBarLine);
            Assert.AreEqual(1, _lines[6].Bar);
            Assert.IsTrue(_lines[0].Bar <= 0);
        }

        [Test]
        public void MidSongWindowHasSevenBeatsAndTwoBarLines()
        {
            // Window [5, 8]: beats at 5.0, 5.5 ... 8.0; bars start every 2 s.
            Assert.AreEqual(7, Collect(5.0));
            List<BeatLine> bars = _lines.FindAll(l => l.IsBarLine);
            Assert.AreEqual(2, bars.Count); // 6.0, 8.0
            Assert.AreEqual(4, bars[0].Bar);
            Assert.AreEqual(5, bars[1].Bar);
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
        }

        [Test]
        public void EightyEightKeysGetALaneAtEveryC()
        {
            // A0 (21) .. C8 (108): C1 (24) through C8, eight lines.
            Assert.AreEqual(8, BeatGrid.LaneNotes(21, 108, _lanes));
            Assert.AreEqual(24, _lanes[0]);
            Assert.AreEqual(108, _lanes[7]);
        }

        [Test]
        public void AKeyboardStartingOnCHasALaneAtItsLeftEnd()
        {
            // 61 keys, C2 (36) .. C7 (96), like the reference video's Casio.
            Assert.AreEqual(6, BeatGrid.LaneNotes(36, 96, _lanes));
            Assert.AreEqual(36, _lanes[0]);
        }

        [Test]
        public void ARangeWithNoCHasNoLanes()
        {
            Assert.AreEqual(0, BeatGrid.LaneNotes(62, 71, _lanes)); // D4..B4
        }
    }
}
