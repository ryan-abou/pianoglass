using NUnit.Framework;
using PianoGlass.Song;
using UnityEngine;

namespace PianoGlass.Tests.EditMode
{
    public class OutcomeTintTests
    {
        private static readonly Color Base = new Color(0.40f, 0.68f, 0.95f, 1f);

        [Test]
        public void ColoursMatchThePalette()
        {
            AssertColor(new Color32(0x3D, 0xDC, 0x97, 255), OutcomeTint.Hit);
            AssertColor(new Color32(0x5A, 0x63, 0x6E, 255), OutcomeTint.Missed);
            AssertColor(new Color32(0xFF, 0x6B, 0x5B, 255), OutcomeTint.Wrong);
        }

        [Test]
        public void PendingKeepsTheBaseColourWhateverTheTime()
        {
            AssertColor(Base, OutcomeTint.Slab(Base, NoteOutcome.Pending, 5f, OutcomeTint.Hit, OutcomeTint.Missed));
        }

        [TestCase(NoteOutcome.Hit)]
        [TestCase(NoteOutcome.Missed)]
        public void JudgedSlabStartsAtBaseAndEndsAtItsOutcomeColour(NoteOutcome outcome)
        {
            Color target = outcome == NoteOutcome.Hit ? OutcomeTint.Hit : OutcomeTint.Missed;
            AssertColor(Base, OutcomeTint.Slab(Base, outcome, 0f, OutcomeTint.Hit, OutcomeTint.Missed));
            AssertColor(target, OutcomeTint.Slab(Base, outcome, OutcomeTint.FadeSeconds, OutcomeTint.Hit, OutcomeTint.Missed));
            AssertColor(target, OutcomeTint.Slab(Base, outcome, 10f, OutcomeTint.Hit, OutcomeTint.Missed));
        }

        [Test]
        public void FadeTakesAbout120MsAndFrontLoadsTheChange()
        {
            Assert.AreEqual(0.12f, OutcomeTint.FadeSeconds, 1e-6f);
            Assert.AreEqual(0f, OutcomeTint.Fade(-0.05f));
            Assert.AreEqual(0.75f, OutcomeTint.Fade(0.06f), 1e-5f); // ease-out: 3/4 done at half time
            Assert.AreEqual(1f, OutcomeTint.Fade(0.12f));
        }

        [Test]
        public void WrongPulseIsFullAtThePressAndGoneAfterwards()
        {
            Assert.AreEqual(1f, OutcomeTint.WrongPulse(0f), 1e-6f);
            Assert.AreEqual(0f, OutcomeTint.WrongPulse(OutcomeTint.WrongPulseSeconds));
            Assert.AreEqual(0f, OutcomeTint.WrongPulse(1f));
            Assert.AreEqual(0f, OutcomeTint.WrongPulse(-0.01f), "nothing before the press");
            Assert.IsTrue(OutcomeTint.IsPulsing(0.1f));
            Assert.IsFalse(OutcomeTint.IsPulsing(OutcomeTint.WrongPulseSeconds));
        }

        [Test]
        public void WrongKeyStartsBrightCoralHeldOrNot()
        {
            Color idle = new Color(0.55f, 0.60f, 0.50f, 1f);
            AssertColor(OutcomeTint.Wrong, OutcomeTint.WrongKey(idle, held: true, 0f));
            AssertColor(OutcomeTint.Wrong, OutcomeTint.WrongKey(idle, held: false, 0f));
        }

        [Test]
        public void WrongKeyStaysCoralWhileHeldAndNeverTurnsGreen()
        {
            Color idle = new Color(0.55f, 0.60f, 0.50f, 1f);
            Color late = OutcomeTint.WrongKey(idle, held: true, 5f);
            AssertColor(OutcomeTint.WrongHeld, late);
            Assert.Greater(late.r, late.g, "still reads as coral, not green");
            Assert.Less(late.r, OutcomeTint.Wrong.r, "dimmer than the flash");
        }

        [Test]
        public void ReleasedWrongKeyFadesBackToIdle()
        {
            Color idle = new Color(0.55f, 0.60f, 0.50f, 1f);
            AssertColor(idle, OutcomeTint.WrongKey(idle, held: false, OutcomeTint.WrongPulseSeconds));
        }

        [Test]
        public void WrongPulseOnlyEverDecays()
        {
            float last = float.MaxValue;
            for (float t = 0f; t < OutcomeTint.WrongPulseSeconds; t += 0.01f)
            {
                float v = OutcomeTint.WrongPulse(t);
                Assert.LessOrEqual(v, last);
                last = v;
            }
        }

        private static void AssertColor(Color expected, Color actual)
        {
            Assert.AreEqual(expected.r, actual.r, 1e-4f, "r");
            Assert.AreEqual(expected.g, actual.g, 1e-4f, "g");
            Assert.AreEqual(expected.b, actual.b, 1e-4f, "b");
            Assert.AreEqual(expected.a, actual.a, 1e-4f, "a");
        }
    }
}
