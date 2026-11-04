using System.Collections.Generic;
using NUnit.Framework;
using PianoGlass.Scoring;
using PianoGlass.Song;
using UnityEngine;

namespace PianoGlass.Tests.EditMode
{
    /// <summary>
    /// Note judgement and scoring. Mostly against the real Twinkle melody
    /// (120 BPM, a quarter note every 0.5 s, C4 C4 G4 G4 A4 A4 G4 ...), so the
    /// repeated-note cases are the ones the player actually meets.
    /// </summary>
    public class ScoringTests
    {
        private const int C4 = 60, D4 = 62, E4 = 64, G4 = 67, A4 = 69;
        private static readonly TimingWindows W = TimingWindows.Default; // 40 / 80 / 150 ms

        private static ScoringSession Twinkle() =>
            new ScoringSession(SongData.TwinkleTwinkleLittleStar(), W);

        private static SongData Make(params (int pitch, double start)[] notes)
        {
            var list = new List<SongNote>();
            foreach ((int p, double s) in notes) list.Add(new SongNote(p, s, 0.25));
            return new SongData("test", 120f, list);
        }

        // ------------------------------------------------------------------
        // NoteJudge
        // ------------------------------------------------------------------

        [Test]
        public void WindowEdgesAreInclusive()
        {
            Assert.AreEqual(Judgement.Perfect, NoteJudge.Judge(40, W));
            Assert.AreEqual(Judgement.Perfect, NoteJudge.Judge(-40, W));
            Assert.AreEqual(Judgement.Great, NoteJudge.Judge(40.1, W));
            Assert.AreEqual(Judgement.Great, NoteJudge.Judge(-80, W));
            Assert.AreEqual(Judgement.Good, NoteJudge.Judge(150, W));
            Assert.AreEqual(Judgement.Miss, NoteJudge.Judge(150.1, W));
        }

        [Test]
        public void ErrorsAreRealTimeAtAnyTempo()
        {
            // 20 ms of song time late at half speed is 40 ms of real time.
            Assert.AreEqual(40.0, NoteJudge.ErrorMs(1.020, 1.0, 0.5), 1e-9);
            Assert.AreEqual(-20.0, NoteJudge.ErrorMs(0.980, 1.0, 1.0), 1e-9);
            Assert.AreEqual(0.075, NoteJudge.WindowSongSeconds(150, 0.5), 1e-12);
        }

        // ------------------------------------------------------------------
        // Scoring from part-way through (attempt begun after a Verifying A)
        // ------------------------------------------------------------------

        [Test]
        public void NotesBeforeScoreFromAreNotScored()
        {
            var s = new ScoringSession(SongData.TwinkleTwinkleLittleStar(), W, null, scoreNotesFrom: 1.0);
            int all = Twinkle().ScorableCount;

            Assert.AreEqual(all - 2, s.ScorableCount, "the two C4s at 0 and 0.5 s are out");
            Assert.AreEqual(1.0, s.FirstNoteTime, 1e-9);
        }

        [Test]
        public void NotesBeforeScoreFromAreNeverMissed()
        {
            var s = new ScoringSession(SongData.TwinkleTwinkleLittleStar(), W, null, scoreNotesFrom: 1.0);
            s.Advance(1.05, 1.0);

            Assert.AreEqual(0, s.Miss, "nothing before 1.0 s is judged, and 1.0 s is still in its window");
            Assert.AreEqual(0, s.JudgedCount);
        }

        [Test]
        public void ANoteAtScoreFromStillCounts()
        {
            var s = new ScoringSession(SongData.TwinkleTwinkleLittleStar(), W, null, scoreNotesFrom: 1.0);
            s.OnNoteOn(G4, 1.010, 1.0);

            Assert.AreEqual(1, s.Perfect);
        }

        [Test]
        public void LowestTempoIsTheSlowestEverPlayed()
        {
            ScoringSession s = Twinkle();
            Assert.IsTrue(double.IsPositiveInfinity(s.LowestTempo));
            s.Advance(0.1, 1.0);
            s.Advance(0.2, 0.8);   // slowed while paused
            s.OnNoteOn(G4, 1.0, 0.9);
            s.Advance(1.2, 1.05);  // then sped up past where it started
            Assert.AreEqual(0.8, s.LowestTempo, 1e-12);
        }

        [Test]
        public void ZeroTempoIsNotATempo()
        {
            ScoringSession s = Twinkle();
            s.Advance(0.1, 0.0);
            Assert.IsTrue(double.IsPositiveInfinity(s.LowestTempo));
        }

        [Test]
        public void DefaultScoresEveryNote()
        {
            var s = new ScoringSession(SongData.TwinkleTwinkleLittleStar(), W);
            Assert.AreEqual(SongData.TwinkleTwinkleLittleStar().Notes.Count, s.ScorableCount);
        }

        // ------------------------------------------------------------------
        // Matching
        // ------------------------------------------------------------------

        [Test]
        public void TwoRepeatedNotesAreJudgedSeparately()
        {
            ScoringSession s = Twinkle();
            s.OnNoteOn(C4, 0.010, 1.0);
            s.OnNoteOn(C4, 0.560, 1.0);

            Assert.AreEqual(2, s.JudgedCount);
            Assert.AreEqual(Judgement.Perfect, s.Results[0].judgement);
            Assert.AreEqual(0, s.Results[0].noteIndex);
            Assert.AreEqual(Judgement.Great, s.Results[1].judgement);
            Assert.AreEqual(1, s.Results[1].noteIndex);
            Assert.AreEqual(60.0, s.Results[1].errorMs, 1e-6);
        }

        [Test]
        public void OnePressNeverCountsForTwoNotes()
        {
            ScoringSession s = Twinkle();
            s.OnNoteOn(C4, 0.0, 1.0);
            // Key held, never pressed again: the second C4 goes by.
            s.Advance(0.7, 1.0);

            Assert.AreEqual(1, s.Perfect);
            Assert.AreEqual(1, s.Miss);
            Assert.IsFalse(s.Results[1].pressed);
            Assert.AreEqual(1, s.Results[1].noteIndex);
        }

        [Test]
        public void APressMatchesTheEarliestNoteWhenWindowsOverlap()
        {
            // Two C4s 0.1 s apart: both windows contain a press at 0.06.
            ScoringSession s = new ScoringSession(Make((C4, 0.0), (C4, 0.1)), W);
            s.OnNoteOn(C4, 0.06, 1.0);
            Assert.AreEqual(0, s.Results[0].noteIndex);
            Assert.AreEqual(60.0, s.Results[0].errorMs, 1e-6);

            s.OnNoteOn(C4, 0.11, 1.0);
            Assert.AreEqual(1, s.Results[1].noteIndex);
        }

        [Test]
        public void ALateNoteIsStillJudgedRightUpToTheGoodWindow()
        {
            ScoringSession s = Twinkle();
            s.Advance(0.149, 1.0);
            Assert.AreEqual(0, s.JudgedCount, "not missed yet");
            s.OnNoteOn(C4, 0.149, 1.0);
            Assert.AreEqual(Judgement.Good, s.LastResult.judgement);
        }

        [Test]
        public void MissesArriveOnlyOncePastTheGoodWindow()
        {
            ScoringSession s = Twinkle();
            s.Advance(0.150, 1.0);
            Assert.AreEqual(0, s.Miss, "exactly at the edge: still hittable");
            s.Advance(0.151, 1.0);
            Assert.AreEqual(1, s.Miss);
            s.Advance(0.151, 1.0);
            Assert.AreEqual(1, s.Miss, "never judged twice");
        }

        [Test]
        public void AtHalfSpeedTheWindowIsInRealTime()
        {
            ScoringSession s = Twinkle();
            // 0.05 s of song time late at tempo 0.5 = 100 ms real: GOOD, not GREAT.
            s.OnNoteOn(C4, 0.05, 0.5);
            Assert.AreEqual(Judgement.Good, s.LastResult.judgement);
            Assert.AreEqual(100.0, s.LastResult.errorMs, 1e-6);

            // The window in song time halves too: 0.075 s is the edge.
            s.Advance(0.576, 0.5);
            Assert.AreEqual(1, s.Miss, "second C4 at 0.5 missed past 0.575");
        }

        // ------------------------------------------------------------------
        // Wrong notes
        // ------------------------------------------------------------------

        [Test]
        public void AWrongKeyNamesTheNoteThatWasDue()
        {
            ScoringSession s = Twinkle();
            s.OnNoteOn(61, 0.02, 1.0); // C#4 instead of C4

            Assert.AreEqual(0, s.JudgedCount, "a wrong key does not judge the due note");
            Assert.AreEqual(1, s.WrongPitchCount);
            WrongNote w = s.WrongNotes[0];
            Assert.AreEqual(WrongNote.WrongPitch, w.kind);
            Assert.AreEqual(61, w.pressedPitch);
            Assert.AreEqual(C4, w.expectedPitch);
            Assert.AreEqual(0, w.expectedNoteIndex);
            Assert.AreEqual(20.0, w.errorMs, 1e-6);
            Assert.AreEqual(1, w.bar);

            // The right key, still in time, still counts.
            s.OnNoteOn(C4, 0.05, 1.0);
            Assert.AreEqual(Judgement.Great, s.LastResult.judgement);
        }

        [Test]
        public void APressWithNothingDueIsExtra()
        {
            ScoringSession s = Twinkle();
            s.OnNoteOn(D4, 0.25, 1.0); // halfway between the two C4s
            Assert.AreEqual(1, s.ExtraCount);
            Assert.AreEqual(WrongNote.Extra, s.WrongNotes[0].kind);
            Assert.AreEqual(-1, s.WrongNotes[0].expectedPitch);
        }

        [Test]
        public void ASecondPressOfAnAlreadyHitNoteIsExtra()
        {
            ScoringSession s = Twinkle();
            s.OnNoteOn(C4, 0.0, 1.0);
            s.OnNoteOn(C4, 0.05, 1.0);
            Assert.AreEqual(1, s.JudgedCount);
            Assert.AreEqual(1, s.ExtraCount);
        }

        // ------------------------------------------------------------------
        // Chords, range, totals
        // ------------------------------------------------------------------

        [Test]
        public void EachNoteOfAChordIsJudgedOnItsOwn()
        {
            ScoringSession s = new ScoringSession(Make((C4, 1.0), (E4, 1.0), (G4, 1.0)), W);
            s.OnNoteOn(E4, 1.005, 1.0);
            s.OnNoteOn(C4, 1.060, 1.0);
            s.OnNoteOn(D4, 1.010, 1.0); // wrong: names the one note still due, G4
            s.Advance(2.0, 1.0);

            Assert.AreEqual(1, s.Perfect);
            Assert.AreEqual(1, s.Great);
            Assert.AreEqual(1, s.Miss);
            Assert.AreEqual(G4, s.WrongNotes[0].expectedPitch);
            Assert.IsTrue(s.IsComplete);
        }

        [Test]
        public void NotesOffTheKeyboardAreNotScored()
        {
            var s = new ScoringSession(Make((20, 0.0), (C4, 0.5)), W, pitch => pitch >= 21 && pitch <= 108);
            Assert.AreEqual(1, s.ScorableCount);
            s.Advance(5.0, 1.0);
            Assert.AreEqual(1, s.Miss);
            Assert.IsTrue(s.IsComplete);
        }

        [Test]
        public void TotalsScoreAndAccuracy()
        {
            ScoringSession s = Twinkle();
            s.OnNoteOn(C4, 0.0, 1.0);   // PERFECT 100
            s.OnNoteOn(C4, 0.56, 1.0);  // GREAT 70
            s.OnNoteOn(G4, 1.12, 1.0);  // GOOD 40
            s.Advance(1.7, 1.0);        // G4 at 1.5 MISS

            Assert.AreEqual(210, s.Score);
            Assert.AreEqual(3, s.NotesHit);
            Assert.AreEqual(1, s.NotesMissed);
            Assert.AreEqual(52.5, s.Accuracy, 1e-9); // 210 of 400
            Assert.AreEqual(0.0, AccuracyCalculator.Accuracy(0, 0, 0, 0));
        }

        [Test]
        public void ErrorStatsAreOverPressedNotesOnly()
        {
            ScoringSession s = Twinkle();
            s.OnNoteOn(C4, 0.030, 1.0);
            s.OnNoteOn(C4, 0.550, 1.0);
            s.Advance(1.2, 1.0); // G4 missed: not in the stats
            (double mean, double sd, int n) = s.ErrorStats();
            Assert.AreEqual(2, n);
            Assert.AreEqual(40.0, mean, 1e-6);
            Assert.AreEqual(10.0, sd, 1e-6);
        }

        // ------------------------------------------------------------------
        // Bars and the attempt file
        // ------------------------------------------------------------------

        [Test]
        public void TwinkleHasEightBarsOfFourBeats()
        {
            SongData song = SongData.TwinkleTwinkleLittleStar();
            Assert.AreEqual(1, song.BarOf(-2.0), "lead-in");
            Assert.AreEqual(1, song.BarOf(0.0));
            Assert.AreEqual(1, song.BarOf(1.999));
            Assert.AreEqual(2, song.BarOf(2.0));
            Assert.AreEqual(8, song.BarOf(song.Notes[song.Notes.Count - 1].StartTime));
            Assert.AreEqual(8, Twinkle().Bars().Count);
        }

        [Test]
        public void TheWorstBarIsFoundFromPerBarAccuracy()
        {
            ScoringSession s = Twinkle();
            // Bar 1 (0-2 s): all four perfect. Bar 2 (2-4 s): all missed.
            s.OnNoteOn(C4, 0.0, 1.0);
            s.OnNoteOn(C4, 0.5, 1.0);
            s.OnNoteOn(G4, 1.0, 1.0);
            s.OnNoteOn(G4, 1.5, 1.0);
            s.OnNoteOn(E4, 2.0, 1.0); // wrong key for A4, in bar 2
            s.Advance(4.2, 1.0);

            List<BarStats> bars = s.Bars();
            Assert.AreEqual(100.0, bars[0].accuracy, 1e-9);
            Assert.AreEqual(4, bars[0].perfect);
            Assert.AreEqual(0.0, bars[1].accuracy, 1e-9);
            Assert.AreEqual(3, bars[1].miss);
            Assert.AreEqual(1, bars[1].wrongNotes);
            Assert.AreEqual(2, s.WorstBar().bar);
        }

        [Test]
        public void TheAttemptRecordSurvivesJson()
        {
            ScoringSession s = Twinkle();
            s.OnNoteOn(C4, 0.5 + 0.03, 1.0); // second C4 first: the record is sorted anyway
            s.OnNoteOn(61, 0.01, 1.0);
            s.Advance(0.7, 1.0);

            AttemptRecord rec = s.ToRecord(AttemptRecord.Abandoned, "2026-10-27T10:00:00Z", "2026-10-27T10:00:05Z", 1.0, 12.5);
            AttemptRecord back = JsonUtility.FromJson<AttemptRecord>(JsonUtility.ToJson(rec));

            Assert.AreEqual(AttemptRecord.CurrentSchema, back.schema);
            Assert.AreEqual("Twinkle, Twinkle, Little Star", back.song);
            Assert.AreEqual(AttemptRecord.Abandoned, back.outcome);
            Assert.AreEqual(12.5, back.inputOffsetMs, 1e-9);
            Assert.AreEqual(40f, back.windows.perfectMs);
            Assert.AreEqual(2, back.notes.Count);
            Assert.AreEqual(0, back.notes[0].noteIndex, "sorted by time");
            Assert.AreEqual("Miss", back.notes[0].rating);
            Assert.AreEqual("Perfect", back.notes[1].rating);
            Assert.AreEqual(1, back.wrongNotes.Count);
            Assert.AreEqual(WrongNote.WrongPitch, back.wrongNotes[0].kind);
            Assert.AreEqual(8, back.bars.Count);
        }
    }
}
