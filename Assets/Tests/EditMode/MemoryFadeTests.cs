using NUnit.Framework;
using UnityEngine;
using PianoGlass.Scoring;
using PianoGlass.Song;

namespace PianoGlass.Tests.EditMode
{
    /// <summary>Memory fade (F36): the ladder, and Ryan's rule that being judged
    /// changes a faded note's colour and never its opacity.</summary>
    public class MemoryFadeTests
    {
        private static MemoryFade At(int step)
        {
            var f = new MemoryFade();
            for (int i = 0; i < step; i++) f.EndPass(100.0, true);
            return f;
        }

        [TestCase(0, 1f)]
        [TestCase(1, 0.6f)]
        [TestCase(2, 0.3f)]
        [TestCase(3, 0.15f)]
        public void TheLadder(int step, float opacity)
        {
            Assert.AreEqual(opacity, At(step).Opacity(0.0), 1e-6f);
            Assert.AreEqual(opacity, At(step).Opacity(10.0), 1e-6f);
            Assert.AreEqual(opacity, At(step).SteadyOpacity, 1e-6f);
        }

        [Test]
        public void AGlimpseShowsAt15ThenFadesOutAndIsNeverSkipped()
        {
            MemoryFade f = At(MemoryFade.Glimpse);
            Assert.AreEqual(0.15f, f.Opacity(0.0), 1e-6f);
            Assert.AreEqual(0.15f, f.Opacity(MemoryFade.GlimpseHoldSeconds), 1e-6f);
            Assert.AreEqual(0.075f, f.Opacity(MemoryFade.GlimpseHoldSeconds + MemoryFade.GlimpseFadeSeconds / 2), 1e-5f);
            Assert.AreEqual(0f, f.Opacity(MemoryFade.GlimpseHoldSeconds + MemoryFade.GlimpseFadeSeconds), 1e-6f);
            Assert.AreEqual(0f, f.SteadyOpacity, 1e-6f, "no lit key at a glimpse");
        }

        [TestCase(2, 90.0, 3)]
        [TestCase(2, 100.0, 3)]
        [TestCase(2, 89.9, 2)]
        [TestCase(2, 70.0, 2)]
        [TestCase(2, 69.9, 1)]
        [TestCase(2, 40.0, 1)]
        [TestCase(3, 39.9, 1)]
        [TestCase(1, 10.0, 0)]
        [TestCase(0, 10.0, 0)]
        [TestCase(4, 100.0, 4)]
        public void APassMovesTheLadder(int step, double keysRight, int after)
        {
            MemoryFade f = At(step);
            Assert.AreEqual(after, f.StepAfter(keysRight, true));
            f.EndPass(keysRight, true);
            Assert.AreEqual(after, f.Step);
        }

        [Test]
        public void APassWithNothingPlayedMovesNothing()
        {
            MemoryFade f = At(4);
            f.EndPass(0.0, false);
            Assert.AreEqual(4, f.Step);
        }

        [Test]
        public void ThreeMistakesInARowHelpForTheRestOfThePass()
        {
            MemoryFade f = At(3);
            f.OnMistake(); f.OnMistake();
            f.OnNoteRight();
            f.OnMistake(); f.OnMistake();
            Assert.AreEqual(0, f.Help, "a right note breaks the run");
            f.OnMistake();
            Assert.AreEqual(1, f.Help);
            Assert.AreEqual(2, f.ShownStep);
            Assert.AreEqual(3, f.Step, "the ladder itself waits for the end of the pass");
            f.OnMistake(); f.OnMistake(); f.OnMistake();
            Assert.AreEqual(1, f.ShownStep);
        }

        [Test]
        public void HelpFromAGlimpseShowsTheNoteSteadily()
        {
            MemoryFade f = At(4);
            f.AskForHelp();
            Assert.AreEqual(0.15f, f.Opacity(10.0), 1e-6f);
            Assert.AreEqual(0.15f, f.SteadyOpacity, 1e-6f);
        }

        [Test]
        public void HelpEndsWithThePassAndAHelpedPassCannotGoFainter()
        {
            MemoryFade f = At(2);
            f.AskForHelp();
            Assert.AreEqual(2, f.StepAfter(95.0, true));
            f.EndPass(95.0, true);
            Assert.AreEqual(0, f.Help);
            Assert.AreEqual(2, f.ShownStep);
            Assert.AreEqual(3, f.StepAfter(95.0, true));
        }

        [Test]
        public void HelpAtFullIsNothing()
        {
            var f = new MemoryFade();
            f.AskForHelp();
            Assert.AreEqual(0, f.Help);
        }

        [TestCase(0, 95.0, 0.80f, "tempo waits while notes can still fade")]
        [TestCase(4, 95.0, 0.85f, "at a glimpse it speeds up")]
        [TestCase(2, 50.0, 0.80f, "notes come back before it slows")]
        [TestCase(0, 50.0, 0.75f, "at full notes it slows")]
        [TestCase(4, 80.0, 0.80f, "holds")]
        public void OneThingChangesAtATime(int step, double accuracy, float expected, string why)
        {
            Assert.AreEqual(expected, AutoTempo.NextWithFade(0.80f, accuracy, At(step)), 1e-5f, why);
        }

        [Test]
        public void KeysRightIgnoresTimingAndCountsWrongPresses()
        {
            var late = new AttemptRecord { notesHit = 10, accuracy = 60 };
            Assert.AreEqual(100.0, late.KeysRightPercent, 1e-9);
            var wrong = new AttemptRecord { notesHit = 9, notesMissed = 1, wrongPitch = 6, extra = 2, accuracy = 87 };
            Assert.AreEqual(50.0, wrong.KeysRightPercent, 1e-9);
        }

        private static readonly Color Blue = new Color(0.4f, 0.68f, 0.95f, 1f);

        private static Color Tint(NoteOutcome outcome, float seconds, float opacity) =>
            OutcomeTint.FadedSlab(Blue, outcome, seconds, OutcomeTint.Hit, OutcomeTint.Missed, opacity);

        [TestCase(1f)]
        [TestCase(0.6f)]
        [TestCase(0.3f)]
        [TestCase(0f)]
        public void JudgingNeverChangesTheOpacity(float opacity)
        {
            Assert.AreEqual(opacity, Tint(NoteOutcome.Pending, 0f, opacity).a, 1e-6f);
            Assert.AreEqual(opacity, Tint(NoteOutcome.Hit, 0.01f, opacity).a, 1e-6f);
            Assert.AreEqual(opacity, Tint(NoteOutcome.Hit, 5f, opacity).a, 1e-6f);
            Assert.AreEqual(opacity, Tint(NoteOutcome.Missed, 5f, opacity).a, 1e-6f);
        }

        [Test]
        public void AHalfFadedNoteStillTurnsMintOrSlate()
        {
            Color hit = Tint(NoteOutcome.Hit, 5f, 0.6f);
            Assert.AreEqual(OutcomeTint.Hit.r, hit.r, 1e-6f);
            Assert.AreEqual(OutcomeTint.Hit.g, hit.g, 1e-6f);
            Assert.AreEqual(OutcomeTint.Hit.b, hit.b, 1e-6f);

            Color missed = Tint(NoteOutcome.Missed, 5f, 0.3f);
            Assert.AreEqual(OutcomeTint.Missed.r, missed.r, 1e-6f);
        }

        // ---- The clock ------------------------------------------------------

        private SongClock _clock;

        [SetUp]
        public void MakeClock() => _clock = new GameObject("clock").AddComponent<SongClock>();

        [TearDown]
        public void DropClock() => Object.DestroyImmediate(_clock.gameObject);

        private static LoopRange Bar2 => LoopRange.Between(SongData.TwinkleTwinkleLittleStar(), 2, 2);

        [Test]
        public void TheClockFollowsTheLadderNotThePass()
        {
            _clock.MemoryFadeOn = true;
            _clock.StartLoop(Bar2);
            Assert.AreEqual(1f, _clock.NoteOpacity, 1e-6f);

            _clock.Advance(4.1);
            Assert.AreEqual(2, _clock.LoopPass);
            Assert.AreEqual(1f, _clock.NoteOpacity, 1e-6f, "wrapping alone fades nothing");

            _clock.Fade.EndPass(100.0, true);
            Assert.AreEqual(0.6f, _clock.NoteOpacity, 1e-6f);
            Assert.AreEqual(0.6f, _clock.NoteOpacityAt(2.0, 3.0), 1e-6f);
        }

        [Test]
        public void TheFirstNotesOfAPassGetTheirGlimpse()
        {
            _clock.MemoryFadeOn = true;
            _clock.TempoMultiplier = 1f;
            _clock.StartLoop(Bar2);   // wraps back to song time 0, notes from 2
            for (int i = 0; i < MemoryFade.Glimpse; i++) _clock.Fade.EndPass(100.0, true);

            // Lead 3 s: this note was due to appear at -1, before the pass began.
            Assert.AreEqual(0.15f, _clock.NoteOpacityAt(2.0, 3.0), 1e-6f);

            _clock.Advance(2.0);
            Assert.AreEqual(0f, _clock.NoteOpacityAt(2.0, 3.0), 1e-6f);
            Assert.AreEqual(0.15f, _clock.NoteOpacityAt(5.0, 3.0), 1e-6f, "a later note has only just appeared");
        }

        [Test]
        public void OffMeansAlwaysFullyVisible()
        {
            _clock.MemoryFadeOn = false;
            _clock.StartLoop(Bar2);
            _clock.Fade.EndPass(100.0, true);
            Assert.AreEqual(1f, _clock.NoteOpacity, 1e-6f);
            Assert.AreEqual(1f, _clock.NoteOpacityAt(2.0, 3.0), 1e-6f);
        }

        [Test]
        public void OutsideALoopNothingFades()
        {
            _clock.MemoryFadeOn = true;
            Assert.IsFalse(_clock.IsFading);
            Assert.AreEqual(1f, _clock.NoteOpacity, 1e-6f);
            Assert.AreEqual(0, _clock.LoopPass);
        }

        [Test]
        public void ANewLoopStartsVisibleAgain()
        {
            _clock.MemoryFadeOn = true;
            _clock.StartLoop(Bar2);
            _clock.Fade.EndPass(100.0, true);
            Assert.AreEqual(0.6f, _clock.NoteOpacity, 1e-6f);

            _clock.Restart();
            Assert.AreEqual(1f, _clock.NoteOpacity, 1e-6f);
            Assert.IsFalse(_clock.IsFading);

            _clock.StartLoop(Bar2);
            Assert.AreEqual(1, _clock.LoopPass);
            Assert.AreEqual(1f, _clock.NoteOpacity, 1e-6f);
        }

        [Test]
        public void ASavedStepIsWhereASongStartsAndIsClamped()
        {
            Assert.AreEqual(3, new MemoryFade(3).Step);
            Assert.AreEqual(0, new MemoryFade(-2).Step);
            Assert.AreEqual(MemoryFade.Glimpse, new MemoryFade(99).Step);
        }

        [Test]
        public void AWholeSongFadesWithNoLoop()
        {
            _clock.MemoryFadeOn = true;
            Assert.IsFalse(_clock.IsFading, "no ladder loaded yet");

            _clock.SongFade = new MemoryFade(1);
            Assert.IsTrue(_clock.IsFading);
            Assert.AreEqual(0.6f, _clock.NoteOpacity, 1e-6f);
            Assert.AreEqual(0.6f, _clock.NoteOpacityAt(10.0, 3.0), 1e-6f);

            _clock.MemoryFadeOn = false;
            Assert.AreEqual(1f, _clock.NoteOpacity, 1e-6f, "off means off");
        }

        [Test]
        public void ALoopHasItsOwnLadderAndLeavesTheSongsAlone()
        {
            _clock.MemoryFadeOn = true;
            var song = new MemoryFade(3);
            _clock.SongFade = song;

            _clock.StartLoop(Bar2);
            Assert.AreEqual(1f, _clock.NoteOpacity, 1e-6f, "a new loop starts visible");
            _clock.Fade.EndPass(100.0, true);
            Assert.AreEqual(3, song.Step);

            _clock.Restart();
            Assert.AreSame(song, _clock.Fade);
            Assert.AreEqual(0.15f, _clock.NoteOpacity, 1e-6f);
        }

        [Test]
        public void RestartingASongDropsItsHelpButKeepsItsStep()
        {
            var song = new MemoryFade(2);
            _clock.MemoryFadeOn = true;
            _clock.SongFade = song;
            song.AskForHelp();
            Assert.AreEqual(1, song.ShownStep);

            _clock.Restart();
            Assert.AreEqual(2, song.ShownStep);
            Assert.AreEqual(2, song.Step);
        }

        [Test]
        public void TheFirstNotesOfASongGetTheirGlimpse()
        {
            _clock.MemoryFadeOn = true;
            _clock.SongFade = new MemoryFade(MemoryFade.Glimpse);
            _clock.Restart();
            // Due at 0 with 5 s of highway: part way down when the song starts.
            Assert.AreEqual(0.15f, _clock.NoteOpacityAt(0.0, 5.0), 1e-6f);
        }

        [Test]
        public void TheChordIsHoldA0PressDSharp1()
        {
            Assert.AreEqual(ChordAction.ToggleMemoryFade,
                            TransportChord.OnNoteOn(TransportChord.DSharp1, n => n == TransportChord.A0));
            Assert.AreEqual(ChordAction.None, TransportChord.OnNoteOn(TransportChord.DSharp1, n => false));
        }
    }
}
