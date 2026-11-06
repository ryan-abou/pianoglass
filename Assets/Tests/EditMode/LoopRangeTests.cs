using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using PianoGlass.Scoring;
using PianoGlass.Song;

namespace PianoGlass.Tests.EditMode
{
    /// <summary>The practice loop (F33): which bars, what times, the clock's
    /// wrap, and a session scoring only the bars in the loop.</summary>
    public class LoopRangeTests
    {
        private static SongData Twinkle => SongData.TwinkleTwinkleLittleStar();

        [Test]
        public void TwinkleHasEightBarsOfTwoSeconds()
        {
            Assert.AreEqual(8, Twinkle.BarCount);
            Assert.AreEqual(2.0, Twinkle.SecondsPerBar, 1e-9);
            Assert.AreEqual(4.0, Twinkle.BarStartTime(3), 1e-9);
        }

        [Test]
        public void TimesCoverTheBarsWithOneBarOfCountIn()
        {
            LoopRange r = LoopRange.Between(Twinkle, 2, 4);
            Assert.AreEqual(2.0, r.NotesFrom, 1e-9);
            Assert.AreEqual(8.0, r.NotesUntil, 1e-9);
            Assert.AreEqual(0.0, r.WrapTo, 1e-9);
            Assert.AreEqual(8.0, r.PassLength, 1e-9);
            Assert.AreEqual("bars 2–4", r.Label);
        }

        [Test]
        public void FirstBarCountsInBeforeTheSong()
        {
            Assert.AreEqual(-2.0, LoopRange.Between(Twinkle, 1, 2).WrapTo, 1e-9);
        }

        [Test]
        public void BetweenClampsAndOrders()
        {
            LoopRange r = LoopRange.Between(Twinkle, 12, 7);
            Assert.AreEqual(7, r.FromBar);
            Assert.AreEqual(8, r.ToBar);
            Assert.AreEqual(16.0, r.NotesUntil, 1e-9);

            LoopRange low = LoopRange.Between(Twinkle, -3, 0);
            Assert.AreEqual(1, low.FromBar);
            Assert.AreEqual(1, low.ToBar);
            Assert.IsTrue(low.IsSingleBar);
            Assert.AreEqual("bar 1", low.Label);
        }

        [Test]
        public void HoldsIncludesTheFirstBarLineAndNotTheNextOne()
        {
            LoopRange r = LoopRange.Between(Twinkle, 2, 2);
            Assert.IsFalse(r.Holds(1.999));
            Assert.IsTrue(r.Holds(2.0));
            Assert.IsTrue(r.Holds(3.999));
            Assert.IsFalse(r.Holds(4.0));
        }

        // ---- The clock -----------------------------------------------------

        private SongClock _clock;

        [SetUp]
        public void MakeClock() => _clock = new GameObject("clock").AddComponent<SongClock>();

        [TearDown]
        public void DropClock() => Object.DestroyImmediate(_clock.gameObject);

        [Test]
        public void ClockGoesToTheCountInAndKeepsItsOvershootAtTheWrap()
        {
            _clock.TempoMultiplier = 1f;
            _clock.StartLoop(LoopRange.Between(Twinkle, 2, 2)); // wraps 4 -> 0
            Assert.AreEqual(0.0, _clock.SongTime, 1e-9);

            int before = _clock.Wraps;
            _clock.Advance(3.9);
            Assert.AreEqual(before, _clock.Wraps);
            _clock.Advance(0.2);
            Assert.AreEqual(before + 1, _clock.Wraps);
            Assert.AreEqual(0.1, _clock.SongTime, 1e-9);
        }

        [Test]
        public void AVeryLongFrameStillLandsInsideTheLoop()
        {
            _clock.TempoMultiplier = 1f;
            _clock.StartLoop(LoopRange.Between(Twinkle, 2, 2));
            int before = _clock.Wraps;
            _clock.Advance(9.0);
            Assert.AreEqual(before + 2, _clock.Wraps);
            Assert.AreEqual(1.0, _clock.SongTime, 1e-9);
        }

        [Test]
        public void TempoScalesThePassLikeAnythingElse()
        {
            _clock.TempoMultiplier = 0.5f;
            _clock.StartLoop(LoopRange.Between(Twinkle, 2, 2));
            int before = _clock.Wraps;
            _clock.Advance(7.9);
            Assert.AreEqual(before, _clock.Wraps);
            _clock.Advance(0.2);
            Assert.AreEqual(before + 1, _clock.Wraps);
        }

        [Test]
        public void RestartLeavesTheLoop()
        {
            _clock.StartLoop(LoopRange.Between(Twinkle, 2, 2));
            _clock.Restart();
            Assert.IsNull(_clock.Loop);
            _clock.TempoMultiplier = 1f;
            _clock.Advance(40.0);
            Assert.Greater(_clock.SongTime, 30.0);
        }

        [Test]
        public void NoLoopNeverWraps()
        {
            int before = _clock.Wraps;
            _clock.TempoMultiplier = 1f;
            _clock.Advance(100.0);
            Assert.AreEqual(before, _clock.Wraps);
        }

        // ---- Scoring only the loop ------------------------------------------

        private static ScoringSession LoopSession(int from, int to)
        {
            LoopRange r = LoopRange.Between(Twinkle, from, to);
            return new ScoringSession(Twinkle, TimingWindows.Default, null, r.NotesFrom, r.NotesUntil);
        }

        [Test]
        public void SessionScoresOnlyTheNotesInTheRange()
        {
            Assert.AreEqual(3, LoopSession(2, 2).ScorableCount);  // A A G
            Assert.AreEqual(10, LoopSession(2, 4).ScorableCount); // 3 + 4 + 3
            Assert.AreEqual(2.0, LoopSession(2, 2).FirstNoteTime, 1e-9);
        }

        [Test]
        public void NotesOutsideTheRangeAreNeverMissed()
        {
            ScoringSession s = LoopSession(2, 2);
            s.Advance(100.0, 1.0);
            Assert.AreEqual(3, s.Miss);
            Assert.IsTrue(s.IsComplete);
        }

        [Test]
        public void BarsListOnlyTheLoopsBars()
        {
            ScoringSession s = LoopSession(2, 3);
            s.Advance(100.0, 1.0);
            var bars = new List<int>();
            foreach (BarStats b in s.Bars()) bars.Add(b.bar);
            CollectionAssert.AreEqual(new[] { 2, 3 }, bars);
        }

        [Test]
        public void DueSoonKeysCanBeLimitedToTheLoop()
        {
            LoopRange r = LoopRange.Between(Twinkle, 2, 2);
            var due = new HashSet<int>();

            // Bar 3's first note, F4, is due at 4.0; it is outside the loop.
            ActiveNoteSelector.CollectDueSoonPitches(Twinkle.Notes, 3.8, 0.5f, due, r.NotesFrom, r.NotesUntil);
            CollectionAssert.DoesNotContain(due, 65);

            ActiveNoteSelector.CollectDueSoonPitches(Twinkle.Notes, 3.8, 0.5f, due);
            CollectionAssert.Contains(due, 65);
        }

        private static Suggestion ForBars(params double[] accuracy)
        {
            var bars = new List<BarStats>();
            for (int i = 0; i < accuracy.Length; i++) bars.Add(new BarStats { bar = i + 1, notes = 4, accuracy = accuracy[i] });
            return SuggestionPicker.Pick(90, 85, AttemptRecord.BothHands, BarGrouping.Build(bars), false, false);
        }

        [Test]
        public void SolidNeighboursAreLeftOut()
        {
            Suggestion s = ForBars(100, 100, 62, 100, 100, 100, 100, 100);
            Assert.AreEqual("Bar 3 needs work", s.Message);
            Assert.AreEqual("Loop bar 3", s.ButtonText);
            Assert.AreEqual(3, s.FromBar);
            Assert.AreEqual(3, s.ToBar);
        }

        [Test]
        public void ANeighbourThatWasNotSolidComesAlong()
        {
            Suggestion s = ForBars(100, 89.9, 62, 90, 100, 100, 100, 100);
            Assert.AreEqual("Bar 3 needs work", s.Message);
            Assert.AreEqual("Loop bars 2–3", s.ButtonText);

            s = ForBars(100, 100, 62, 70, 100, 100, 100, 100);
            Assert.AreEqual("Loop bars 3–4", s.ButtonText);

            s = ForBars(100, 80, 62, 70, 80, 100, 100, 100);
            Assert.AreEqual("Loop bars 2–4", s.ButtonText, "one either side at most");
        }

        [Test]
        public void TheFirstAndLastBarsStayInTheSong()
        {
            Assert.AreEqual("Loop bars 1–2", ForBars(62, 70, 100, 100, 100, 100, 100, 100).ButtonText);
            Assert.AreEqual("Loop bars 7–8", ForBars(100, 100, 100, 100, 100, 100, 70, 62).ButtonText);
            Assert.AreEqual("Loop bar 8", ForBars(100, 100, 100, 100, 100, 100, 100, 62).ButtonText);
        }
    }
}
