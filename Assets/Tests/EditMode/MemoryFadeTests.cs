using NUnit.Framework;
using UnityEngine;
using PianoGlass.Song;

namespace PianoGlass.Tests.EditMode
{
    /// <summary>Memory fade (F36): the steps, and Ryan's rule that being judged
    /// changes a faded note's colour and never its opacity.</summary>
    public class MemoryFadeTests
    {
        [TestCase(0, 1f)]
        [TestCase(1, 1f)]
        [TestCase(2, 0.6f)]
        [TestCase(3, 0.3f)]
        [TestCase(4, 0f)]
        [TestCase(50, 0f)]
        public void TheSteps(int pass, float opacity)
        {
            Assert.AreEqual(opacity, MemoryFade.Opacity(pass), 1e-6f);
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
        public void NotesFadeOverThePassesOfALoopWhenItIsOn()
        {
            _clock.MemoryFadeOn = true;
            _clock.StartLoop(Bar2);
            Assert.AreEqual(1, _clock.LoopPass);
            Assert.AreEqual(1f, _clock.NoteOpacity, 1e-6f);

            _clock.Advance(4.1);
            Assert.AreEqual(2, _clock.LoopPass);
            Assert.AreEqual(0.6f, _clock.NoteOpacity, 1e-6f);

            _clock.Advance(4.0);
            Assert.AreEqual(0.3f, _clock.NoteOpacity, 1e-6f);

            _clock.Advance(4.0);
            Assert.AreEqual(0f, _clock.NoteOpacity, 1e-6f);
        }

        [Test]
        public void OffMeansAlwaysFullyVisible()
        {
            _clock.MemoryFadeOn = false;
            _clock.StartLoop(Bar2);
            _clock.Advance(20.0);
            Assert.AreEqual(1f, _clock.NoteOpacity, 1e-6f);
        }

        [Test]
        public void OutsideALoopNothingFades()
        {
            _clock.MemoryFadeOn = true;
            Assert.AreEqual(1f, _clock.NoteOpacity, 1e-6f);
            Assert.AreEqual(0, _clock.LoopPass);
        }

        [Test]
        public void ANewLoopStartsVisibleAgain()
        {
            _clock.MemoryFadeOn = true;
            _clock.StartLoop(Bar2);
            _clock.Advance(20.0);
            Assert.AreEqual(0f, _clock.NoteOpacity, 1e-6f);

            _clock.Restart();
            Assert.AreEqual(1f, _clock.NoteOpacity, 1e-6f);

            _clock.StartLoop(Bar2);
            Assert.AreEqual(1, _clock.LoopPass);
            Assert.AreEqual(1f, _clock.NoteOpacity, 1e-6f);
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
