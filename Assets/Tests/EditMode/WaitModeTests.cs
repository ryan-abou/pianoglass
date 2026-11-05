using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using PianoGlass.Scoring;
using PianoGlass.Song;

namespace PianoGlass.Tests.EditMode
{
    /// <summary>Wait mode (F22): pitch-only judging, chords, and the clock's hold.</summary>
    public class WaitModeTests
    {
        private const int C4 = 60, D4 = 62, E4 = 64, G4 = 67;

        private static SongData Melody() => new SongData("melody", 120f, new[]
        {
            new SongNote(C4, 0.0, 0.5), new SongNote(C4, 0.5, 0.5), new SongNote(G4, 1.0, 0.5),
        });

        private static SongData WithChord() => new SongData("chord", 120f, new[]
        {
            new SongNote(C4, 0.0, 0.5), new SongNote(E4, 0.0, 0.5), new SongNote(G4, 0.0, 0.5),
            new SongNote(D4, 0.5, 0.5),
        });

        private static ScoringSession Wait(SongData song) =>
            new ScoringSession(song, TimingWindows.Default, pitchOnly: true);

        [Test]
        public void TheRightKeyIsCorrectNeverPerfect()
        {
            ScoringSession s = Wait(Melody());
            s.OnNoteOn(C4, 0.0, 1.0);
            Assert.AreEqual(1, s.Correct);
            Assert.AreEqual(0, s.Perfect + s.Great + s.Good + s.Miss);
            Assert.AreEqual(Judgement.Correct, s.LastResult.judgement);
            Assert.AreEqual("Correct", s.LastResult.rating);
            Assert.IsTrue(s.LastResult.IsHit);
            Assert.AreEqual(0.0, s.LastResult.errorMs, 0.0);
        }

        [Test]
        public void HowEarlyOrLateItIsPressedDoesNotMatter()
        {
            ScoringSession s = Wait(Melody());
            s.OnNoteOn(C4, -2.5, 1.0);  // long before it arrives
            Assert.AreEqual(1, s.Correct);
            Assert.AreEqual(0, s.WrongNotes.Count);
        }

        [Test]
        public void AWrongKeyNamesTheNoteItIsWaitingFor()
        {
            ScoringSession s = Wait(Melody());
            s.OnNoteOn(D4, 0.0, 1.0);
            Assert.AreEqual(0, s.Correct);
            Assert.AreEqual(1, s.WrongPitchCount);
            Assert.AreEqual(0, s.ExtraCount);
            Assert.AreEqual(C4, s.LastWrongNote.expectedPitch);
            Assert.AreEqual(1, s.LastWrongNote.bar);
        }

        [Test]
        public void ARepeatedNoteNeedsANewPress()
        {
            ScoringSession s = Wait(Melody());
            s.OnNoteOn(C4, 0.0, 1.0);
            Assert.AreEqual(0.5, s.NextDueTime, 1e-9);
            s.OnNoteOn(C4, 0.5, 1.0);
            Assert.AreEqual(1.0, s.NextDueTime, 1e-9);
            Assert.AreEqual(2, s.Correct);
        }

        [Test]
        public void TheNextNoteIsNotAcceptedUntilThisOneIsDone()
        {
            ScoringSession s = Wait(Melody());
            s.OnNoteOn(G4, 0.0, 1.0);
            Assert.AreEqual(1, s.WrongPitchCount);
            Assert.AreEqual(0.0, s.NextDueTime, 1e-9);
        }

        [Test]
        public void AChordHoldsUntilEveryPitchIsPressedInAnyOrder()
        {
            ScoringSession s = Wait(WithChord());
            Assert.AreEqual(0.0, s.NextDueTime, 1e-9);
            CollectionAssert.AreEquivalent(new[] { C4, E4, G4 }, s.WaitingPitches());

            s.OnNoteOn(G4, 0.0, 1.0);
            Assert.AreEqual(0.0, s.NextDueTime, 1e-9, "two pitches still to press");
            CollectionAssert.AreEquivalent(new[] { C4, E4 }, s.WaitingPitches());

            s.OnNoteOn(C4, 0.0, 1.0);
            Assert.AreEqual(0.0, s.NextDueTime, 1e-9);
            s.OnNoteOn(E4, 0.0, 1.0);
            Assert.AreEqual(0.5, s.NextDueTime, 1e-9, "chord done");
            CollectionAssert.AreEqual(new[] { D4 }, s.WaitingPitches());
        }

        [Test]
        public void NothingDueOnceEveryNoteIsDone()
        {
            ScoringSession s = Wait(Melody());
            s.OnNoteOn(C4, 0.0, 1.0);
            s.OnNoteOn(C4, 0.5, 1.0);
            s.OnNoteOn(G4, 1.0, 1.0);
            Assert.IsTrue(s.IsComplete);
            Assert.IsTrue(double.IsPositiveInfinity(s.NextDueTime));
            Assert.IsEmpty(s.WaitingPitches());
        }

        [Test]
        public void AccuracyIsRightKeysOutOfAllKeys()
        {
            ScoringSession s = Wait(Melody());
            s.OnNoteOn(C4, 0.0, 1.0);
            s.OnNoteOn(D4, 0.5, 1.0);   // wrong
            s.OnNoteOn(C4, 0.5, 1.0);
            s.OnNoteOn(G4, 1.0, 1.0);
            Assert.AreEqual(75.0, s.Accuracy, 1e-9);
        }

        [Test]
        public void PerBarAccuracyCountsWrongKeysToo()
        {
            ScoringSession s = Wait(Melody());
            s.OnNoteOn(D4, 0.0, 1.0);
            s.OnNoteOn(C4, 0.0, 1.0);
            s.OnNoteOn(C4, 0.5, 1.0);
            s.OnNoteOn(G4, 1.0, 1.0);
            List<BarStats> bars = s.Bars();
            Assert.AreEqual(1, bars.Count);
            Assert.AreEqual(3, bars[0].correct);
            Assert.AreEqual(1, bars[0].wrongNotes);
            Assert.AreEqual(75.0, bars[0].accuracy, 1e-9);
        }

        [Test]
        public void TheRecordSaysWaitAndHasNoTiming()
        {
            ScoringSession s = Wait(Melody());
            s.OnNoteOn(C4, 0.0, 1.0);
            s.OnNoteOn(C4, 0.5, 1.0);
            s.OnNoteOn(G4, 1.0, 1.0);
            AttemptRecord rec = s.ToRecord(AttemptRecord.Completed, "a", "b", 1.0, 0.0);
            Assert.AreEqual(AttemptRecord.Wait, rec.mode);
            Assert.AreEqual(3, rec.correct);
            Assert.AreEqual(3, rec.notesHit);
            Assert.AreEqual(0, rec.perfect + rec.great + rec.good + rec.miss);
            Assert.AreEqual(100.0, rec.accuracy, 1e-9);
            Assert.AreEqual(3, rec.bestStreak);
        }

        [Test]
        public void AContinuousRecordStaysContinuous()
        {
            var s = new ScoringSession(Melody(), TimingWindows.Default);
            Assert.AreEqual(AttemptRecord.Continuous, s.ToRecord(AttemptRecord.Completed, "a", "b", 1.0, 0.0).mode);
        }

        [Test]
        public void TheCardHasNoTimingInWaitMode()
        {
            ScoringSession s = Wait(Melody());
            s.OnNoteOn(C4, 0.0, 1.0);
            s.OnNoteOn(C4, 0.5, 1.0);
            s.OnNoteOn(G4, 1.0, 1.0);
            ResultsSummary r = ResultsSummary.From(s.ToRecord(AttemptRecord.Completed, "a", "b", 1.0, 0.0));
            Assert.AreEqual("–", r.TimingText);
            Assert.AreEqual(3, r.Correct);
            Assert.AreEqual(3, r.TotalNotes);
            Assert.IsTrue(r.IsWaitMode);
            StringAssert.StartsWith("Wait mode", r.SubLine);
            foreach (int bin in r.Histogram) Assert.AreEqual(0, bin);
        }

        [Test]
        public void WaitAndContinuousAreNeverCompared()
        {
            var wait = new AttemptRecord { song = "x", outcome = AttemptRecord.Completed, mode = AttemptRecord.Wait, startedUtc = "2", tempo = 1 };
            var before = new AttemptRecord { song = "x", outcome = AttemptRecord.Completed, mode = AttemptRecord.Continuous, startedUtc = "1", tempo = 1 };
            Assert.IsEmpty(AttemptHistory.Earlier(wait, new[] { before }));
        }

        [Test]
        public void AGoodWaitModeRunSuggestsPlayingInTimeNotAFasterTempo()
        {
            Suggestion s = SuggestionPicker.Pick(100, 80, AttemptRecord.BothHands, new List<BarGroup>(), false, false, AttemptRecord.Wait);
            Assert.AreEqual(SuggestionRule.PlayInTime, s.Rule);
            Assert.AreEqual(SuggestionAction.None, s.Action);
        }

        [Test]
        public void AWeakBarStillComesFirstInWaitMode()
        {
            var bars = new List<BarStats>();
            for (int i = 1; i <= 8; i++) bars.Add(new BarStats { bar = i, notes = 4, accuracy = i == 3 ? 50 : 100 });
            Suggestion s = SuggestionPicker.Pick(90, 80, AttemptRecord.BothHands, BarGrouping.Build(bars), false, false, AttemptRecord.Wait);
            Assert.AreEqual(SuggestionRule.WeakBars, s.Rule);
        }

        [Test]
        public void ATempoChordCarriesOnForWaitMode()
        {
            Assert.AreEqual(ChordAction.ToggleWait, TransportChord.OnNoteOn(TransportChord.D1, n => n == TransportChord.A0));
            Assert.AreEqual(ChordAction.None, TransportChord.OnNoteOn(TransportChord.D1, n => false));
        }

        // ---- The clock ------------------------------------------------------

        private SongClock _clock;

        [SetUp]
        public void MakeClock() => _clock = new GameObject("clock").AddComponent<SongClock>();

        [TearDown]
        public void DropClock() => Object.DestroyImmediate(_clock.gameObject);

        [Test]
        public void TheClockStopsExactlyOnTheHoldAndGoesOnWhenItLifts()
        {
            _clock.Seek(0.9);
            _clock.HoldAt = 1.0;
            _clock.Advance(0.5);
            Assert.AreEqual(1.0, _clock.SongTime, 0.0);
            _clock.Advance(0.5);
            Assert.AreEqual(1.0, _clock.SongTime, 0.0);

            _clock.HoldAt = double.PositiveInfinity;
            _clock.Advance(0.25);
            Assert.AreEqual(1.25, _clock.SongTime, 1e-9);
        }

        [Test]
        public void AHoldBehindTheClockFreezesItWithoutJumpingBack()
        {
            _clock.Seek(2.0);
            _clock.HoldAt = 1.9;
            _clock.Advance(0.5);
            Assert.AreEqual(2.0, _clock.SongTime, 0.0);
        }

        [Test]
        public void WhatIsDrawnStopsAtTheHoldToo()
        {
            _clock.Play();
            _clock.DisplayLatencySeconds = 0.02;
            _clock.Seek(1.0);
            _clock.HoldAt = 1.0;
            Assert.AreEqual(1.0, _clock.DisplaySongTime, 0.0);
            Assert.IsTrue(_clock.IsHolding);

            _clock.HoldAt = double.PositiveInfinity;
            Assert.AreEqual(1.02, _clock.DisplaySongTime, 1e-9);
            Assert.IsFalse(_clock.IsHolding);
        }

        [Test]
        public void APausedClockIsNotHolding()
        {
            _clock.Pause();
            _clock.Seek(1.0);
            _clock.HoldAt = 1.0;
            Assert.IsFalse(_clock.IsHolding);
        }
    }
}
