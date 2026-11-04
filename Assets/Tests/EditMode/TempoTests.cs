using System;
using System.Collections.Generic;
using NUnit.Framework;
using PianoGlass.Song;

namespace PianoGlass.Tests.EditMode
{
    /// <summary>Tempo +-5% (F26): the stepping/clamping rule and the A0 chords.</summary>
    public class TempoTests
    {
        // ------------------------------------------------------------------
        // TempoStep
        // ------------------------------------------------------------------

        [Test]
        public void StepsAreFivePercent()
        {
            Assert.AreEqual(0.95f, TempoStep.Step(1f, -1), 1e-6f);
            Assert.AreEqual(1.05f, TempoStep.Step(1f, +1), 1e-6f);
        }

        [Test]
        public void ClampedAtTwentyFiveAndTwoHundred()
        {
            Assert.AreEqual(0.25f, TempoStep.Step(0.25f, -1), 1e-6f);
            Assert.AreEqual(2.00f, TempoStep.Step(2.00f, +1), 1e-6f);
            Assert.AreEqual(0.25f, TempoStep.Step(0.30f, -1), 1e-6f);
            Assert.AreEqual(2.00f, TempoStep.Step(1.95f, +1), 1e-6f);
        }

        [Test]
        public void ManyStepsNeverDriftOffTheGrid()
        {
            float t = 1f;
            for (int i = 0; i < 15; i++) t = TempoStep.Step(t, -1);   // 100 -> 25
            Assert.AreEqual(25, TempoStep.ToPercent(t));
            for (int i = 0; i < 35; i++) t = TempoStep.Step(t, +1);   // 25 -> 200
            Assert.AreEqual(2f, t, 0f);
            for (int i = 0; i < 20; i++) t = TempoStep.Step(t, -1);   // 200 -> 100
            Assert.AreEqual(1f, t, 0f);
        }

        [Test]
        public void ZeroDirectionKeepsTheTempo()
        {
            Assert.AreEqual(0.85f, TempoStep.Step(0.85f, 0), 1e-6f);
        }

        [Test]
        public void SanitizeFixesStoredGarbage()
        {
            Assert.AreEqual(1f, TempoStep.Sanitize(float.NaN), 0f);
            Assert.AreEqual(1f, TempoStep.Sanitize(float.PositiveInfinity), 0f);
            Assert.AreEqual(0.25f, TempoStep.Sanitize(0f), 1e-6f);
            Assert.AreEqual(0.25f, TempoStep.Sanitize(-3f), 1e-6f);
            Assert.AreEqual(2f, TempoStep.Sanitize(9f), 1e-6f);
            Assert.AreEqual(0.85f, TempoStep.Sanitize(0.8499f), 1e-6f);
            Assert.AreEqual(0.85f, TempoStep.Sanitize(0.86f), 1e-6f);
        }

        [Test]
        public void LabelIsWholePercent()
        {
            Assert.AreEqual("Tempo 100%", TempoStep.Label(1f));
            Assert.AreEqual("Tempo 85%", TempoStep.Label(0.85f));
            Assert.AreEqual("Tempo 200%", TempoStep.Label(2f));
        }

        // ------------------------------------------------------------------
        // TransportChord
        // ------------------------------------------------------------------

        private const int A0 = 21, ASharp0 = 22, B0 = 23, C1 = 24;

        private static Func<int, bool> Held(params int[] keys)
        {
            var set = new HashSet<int>(keys);
            return set.Contains;
        }

        [Test]
        public void AZeroAloneDoesNothing()
        {
            Assert.AreEqual(ChordAction.None, TransportChord.OnNoteOn(A0, Held(A0)));
        }

        [Test]
        public void AZeroWithASharpZeroStillRestartsEitherOrder()
        {
            Assert.AreEqual(ChordAction.Restart, TransportChord.OnNoteOn(ASharp0, Held(A0, ASharp0)));
            Assert.AreEqual(ChordAction.Restart, TransportChord.OnNoteOn(A0, Held(A0, ASharp0)));
        }

        [Test]
        public void HoldAZeroPressBZeroIsSlower()
        {
            Assert.AreEqual(ChordAction.Slower, TransportChord.OnNoteOn(B0, Held(A0, B0)));
        }

        [Test]
        public void HoldAZeroPressCOneIsFaster()
        {
            Assert.AreEqual(ChordAction.Faster, TransportChord.OnNoteOn(C1, Held(A0, C1)));
        }

        [Test]
        public void StepKeysWithoutAZeroDoNothing()
        {
            Assert.AreEqual(ChordAction.None, TransportChord.OnNoteOn(B0, Held(B0)));
            Assert.AreEqual(ChordAction.None, TransportChord.OnNoteOn(C1, Held(C1, ASharp0)));
        }

        [Test]
        public void PressingAZeroWhileAStepKeyIsHeldDoesNotStep()
        {
            Assert.AreEqual(ChordAction.None, TransportChord.OnNoteOn(A0, Held(A0, B0)));
            Assert.AreEqual(ChordAction.None, TransportChord.OnNoteOn(A0, Held(A0, C1)));
        }

        [Test]
        public void OtherKeysDoNothing()
        {
            Assert.AreEqual(ChordAction.None, TransportChord.OnNoteOn(60, Held(A0, 60)));
        }

        [Test]
        public void ConfiguredRestartKeysAreHonoured()
        {
            // SongTransport's chordNoteA/B are serialized and may be retuned.
            Assert.AreEqual(ChordAction.Restart,
                TransportChord.OnNoteOn(30, Held(29, 30), hold: 29, restart: 30));
        }
    }
}
