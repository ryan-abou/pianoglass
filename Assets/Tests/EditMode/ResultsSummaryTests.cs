using System.Collections.Generic;
using NUnit.Framework;
using PianoGlass.Scoring;
using PianoGlass.Song;
using UnityEngine;

namespace PianoGlass.Tests.EditMode
{
    /// <summary>The results card's content (F30) and bar heat strip (F32),
    /// from attempt records alone.</summary>
    public class ResultsSummaryTests
    {
        private const string Twinkle = "Twinkle, Twinkle, Little Star";

        private static AttemptRecord Rec(double accuracy, string started, double tempo = 1.0,
                                         string outcome = AttemptRecord.Completed, string mode = AttemptRecord.Continuous,
                                         string song = Twinkle)
        {
            return new AttemptRecord
            {
                song = song, accuracy = accuracy, startedUtc = started, tempo = tempo,
                outcome = outcome, mode = mode, notesHit = 10,
            };
        }

        private static BarStats Bar(int bar, double accuracy, int notes = 4) =>
            new BarStats { bar = bar, accuracy = accuracy, notes = notes };

        // ------------------------------------------------------------------
        // Headline
        // ------------------------------------------------------------------

        [Test]
        public void HeadlineIsRoundedPercentWithAVerdict()
        {
            ResultsSummary s = ResultsSummary.From(Rec(91.6, "2026-11-03T10:00:00Z"));
            Assert.AreEqual("92%", s.AccuracyText);
            Assert.AreEqual("Great", s.Verdict);
        }

        [TestCase(100, "Excellent")]
        [TestCase(95, "Excellent")]
        [TestCase(94.9, "Great")]
        [TestCase(85, "Great")]
        [TestCase(70, "Good")]
        [TestCase(50, "Getting there")]
        [TestCase(10, "Keep practising")]
        public void VerdictThresholds(double accuracy, string verdict)
        {
            Assert.AreEqual(verdict, ResultsSummary.VerdictFor(accuracy));
        }

        // ------------------------------------------------------------------
        // Change vs last
        // ------------------------------------------------------------------

        [Test]
        public void ChangeIsAgainstTheLatestEarlierAttemptAtTheSameTempoAndMode()
        {
            AttemptRecord now = Rec(90, "2026-11-03T12:00:00Z", tempo: 0.85);
            var previous = new List<AttemptRecord>
            {
                Rec(70, "2026-11-03T09:00:00Z", tempo: 0.85),
                Rec(84, "2026-11-03T11:00:00Z", tempo: 0.85),   // latest comparable
                Rec(99, "2026-11-03T11:30:00Z", tempo: 1.0),    // other tempo
                Rec(10, "2026-11-03T11:40:00Z", tempo: 0.85, mode: AttemptRecord.Wait),
                Rec(20, "2026-11-03T11:50:00Z", tempo: 0.85, outcome: AttemptRecord.Abandoned),
                Rec(30, "2026-11-03T11:55:00Z", tempo: 0.85, song: "Other"),
                Rec(40, "2026-11-03T13:00:00Z", tempo: 0.85),   // later than now
            };

            ResultsSummary s = ResultsSummary.From(now, previous);

            Assert.AreEqual(6.0, s.Change.Value, 1e-9);
            Assert.AreEqual("▲ 6% vs last", s.ChangeText);
        }

        [Test]
        public void ADropShowsADownArrow()
        {
            ResultsSummary s = ResultsSummary.From(Rec(80, "2026-11-03T12:00:00Z"),
                new[] { Rec(85, "2026-11-03T11:00:00Z") });
            Assert.AreEqual("▼ 5% vs last", s.ChangeText);
        }

        [Test]
        public void NoComparableAttemptSaysSo()
        {
            ResultsSummary s = ResultsSummary.From(Rec(80, "2026-11-03T12:00:00Z", tempo: 0.5),
                new[] { Rec(85, "2026-11-03T11:00:00Z", tempo: 1.0) });
            Assert.IsNull(s.Change);
            Assert.AreEqual("first at this tempo", s.ChangeText);
        }

        [Test]
        public void TheCurrentAttemptInThePreviousListIsSkipped()
        {
            AttemptRecord now = Rec(80, "2026-11-03T12:00:00Z");
            Assert.IsNull(ResultsSummary.From(now, new[] { now }).Change);
        }

        [Test]
        public void ALoopOnlyComparesWithTheSameLoop()
        {
            AttemptRecord now = Rec(80, "2026-11-03T12:00:00Z");
            now.loopFromBar = 2; now.loopToBar = 4;
            AttemptRecord full = Rec(60, "2026-11-03T11:00:00Z");
            AttemptRecord sameLoop = Rec(70, "2026-11-03T10:00:00Z");
            sameLoop.loopFromBar = 2; sameLoop.loopToBar = 4;

            ResultsSummary s = ResultsSummary.From(now, new[] { full, sameLoop });
            Assert.AreEqual(10.0, s.Change.Value, 1e-9);
        }

        // ------------------------------------------------------------------
        // Timing and wrong notes
        // ------------------------------------------------------------------

        [Test]
        public void TimingIsArrowAndMilliseconds()
        {
            Assert.AreEqual("▼ 37 ms late", ResultsSummary.TimingFor(37.2, 10));
            Assert.AreEqual("▲ 18 ms early", ResultsSummary.TimingFor(-18, 10));
            Assert.AreEqual("on time (±3 ms)", ResultsSummary.TimingFor(-3.4, 10));
            Assert.AreEqual("", ResultsSummary.TimingFor(0, 0), "nothing pressed, nothing to say");
        }

        [Test]
        public void WrongNotesCountWrongPitchAndExtra()
        {
            AttemptRecord r = Rec(80, "2026-11-03T12:00:00Z");
            r.wrongPitch = 2; r.extra = 1;
            Assert.AreEqual(3, ResultsSummary.From(r).WrongNotes);
            Assert.AreEqual("3 wrong notes", ResultsSummary.From(r).WrongNotesText);
            r.wrongPitch = 0; r.extra = 0;
            Assert.AreEqual("no wrong notes", ResultsSummary.From(r).WrongNotesText);
            r.extra = 1;
            Assert.AreEqual("1 wrong note", ResultsSummary.From(r).WrongNotesText);
        }

        [Test]
        public void ContextNamesSongAndTempo()
        {
            Assert.AreEqual(Twinkle + " · Tempo 85%",
                ResultsSummary.From(Rec(80, "2026-11-03T12:00:00Z", tempo: 0.85)).ContextText);
        }

        // ------------------------------------------------------------------
        // Bar heat strip
        // ------------------------------------------------------------------

        [Test]
        public void OneCellPerBarInOrderWithTheWorstMarked()
        {
            AttemptRecord r = Rec(80, "2026-11-03T12:00:00Z");
            r.bars = new List<BarStats> { Bar(3, 50), Bar(1, 100), Bar(2, 75), Bar(4, 50) };

            ResultsSummary s = ResultsSummary.From(r);

            Assert.AreEqual(4, s.Bars.Count);
            Assert.AreEqual(new[] { 1, 2, 3, 4 }, new[] { s.Bars[0].Bar, s.Bars[1].Bar, s.Bars[2].Bar, s.Bars[3].Bar });
            Assert.AreEqual(3, s.WorstBar, "earliest of the tied worst");
            Assert.IsTrue(s.Bars[2].IsWorst);
            Assert.IsFalse(s.Bars[3].IsWorst);
            Assert.AreEqual("weakest: bar 3 (50%)", s.WorstBarText);
        }

        [Test]
        public void ABarWithNoNotesIsNeverWorstAndIsGrey()
        {
            AttemptRecord r = Rec(80, "2026-11-03T12:00:00Z");
            r.bars = new List<BarStats> { Bar(1, 0, notes: 0), Bar(2, 90) };

            ResultsSummary s = ResultsSummary.From(r);

            Assert.AreEqual(2, s.WorstBar);
            Assert.IsFalse(s.Bars[0].HasNotes);
            Assert.AreEqual(OutcomeTint.Missed, s.Bars[0].Colour);
        }

        [Test]
        public void NoBarsMeansNoWorstBar()
        {
            ResultsSummary s = ResultsSummary.From(Rec(80, "2026-11-03T12:00:00Z"));
            Assert.AreEqual(-1, s.WorstBar);
            Assert.AreEqual(0, s.Bars.Count);
            Assert.IsNull(s.WorstBarText);
        }

        [Test]
        public void RampRunsCoralAmberMint()
        {
            Assert.AreEqual(OutcomeTint.Wrong, ResultsSummary.Ramp(0));
            Assert.AreEqual(ResultsSummary.RampMid, ResultsSummary.Ramp(70));
            Assert.AreEqual(OutcomeTint.Hit, ResultsSummary.Ramp(100));
            Assert.AreEqual(OutcomeTint.Hit, ResultsSummary.Ramp(150), "clamped");
            Color mid = ResultsSummary.Ramp(85);
            Assert.Greater(mid.g, ResultsSummary.RampMid.g * 0.9f);
        }
    }
}
