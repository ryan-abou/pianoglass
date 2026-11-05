using NUnit.Framework;
using PianoGlass.Scoring;

namespace PianoGlass.Tests.EditMode
{
    /// <summary>Auto-tempo in the loop (F35).</summary>
    public class AutoTempoTests
    {
        [TestCase(0.80f, 90.0, 0.85f)]
        [TestCase(0.80f, 100.0, 0.85f)]
        [TestCase(0.80f, 89.99, 0.80f)]
        [TestCase(0.80f, 70.0, 0.80f)]
        [TestCase(0.80f, 69.99, 0.75f)]
        [TestCase(0.80f, 0.0, 0.75f)]
        public void TheThresholds(float tempo, double accuracy, float expected)
        {
            Assert.AreEqual(expected, AutoTempo.Next(tempo, accuracy), 1e-5f);
        }

        [Test]
        public void StopsAtFullSpeed()
        {
            Assert.AreEqual(1f, AutoTempo.Next(0.95f, 100.0), 1e-6f);
            Assert.AreEqual(1f, AutoTempo.Next(1f, 100.0), 1e-6f);
        }

        [Test]
        public void NeverSlowsAFastManualTempoForDoingWell()
        {
            Assert.AreEqual(1.2f, AutoTempo.Next(1.2f, 100.0), 1e-6f);
            Assert.AreEqual(1.2f, AutoTempo.Next(1.2f, 80.0), 1e-6f);
        }

        [Test]
        public void AFastManualTempoStillSlowsWhenItIsGoingBadly()
        {
            Assert.AreEqual(1.15f, AutoTempo.Next(1.2f, 50.0), 1e-5f);
        }

        [Test]
        public void StopsAtTheSlowest()
        {
            Assert.AreEqual(0.25f, AutoTempo.Next(0.25f, 10.0), 1e-6f);
        }

        [Test]
        public void TwentyPassesOfEachNeverLeaveTheGrid()
        {
            float t = 0.5f;
            for (int i = 0; i < 20; i++) t = AutoTempo.Next(t, 100.0);
            Assert.AreEqual(1f, t, 0f);
            for (int i = 0; i < 20; i++) t = AutoTempo.Next(t, 0.0);
            Assert.AreEqual(0.25f, t, 0f);
        }

        [TestCase(0.80f, 0.85f, 92.0, "92%: tempo up to 85%")]
        [TestCase(0.80f, 0.75f, 60.0, "60%: tempo down to 75%")]
        [TestCase(0.80f, 0.80f, 75.0, "75%: tempo stays 80%, 90% speeds up")]
        [TestCase(1.00f, 1.00f, 95.0, "95%: at full speed")]
        [TestCase(1.20f, 1.20f, 95.0, "95%: at full speed")]
        [TestCase(0.25f, 0.25f, 10.0, "10%: already at the slowest, 25%")]
        public void ThePanelLine(float before, float after, double accuracy, string expected)
        {
            Assert.AreEqual(expected, AutoTempo.Describe(before, after, accuracy));
        }
    }
}
