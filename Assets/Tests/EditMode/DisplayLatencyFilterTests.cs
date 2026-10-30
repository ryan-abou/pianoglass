using NUnit.Framework;
using PianoGlass.Song;

namespace PianoGlass.Tests.EditMode
{
    /// <summary>
    /// The readings these feed in are the ones seen on device 2026-10-29:
    /// mostly 15-44 ms, some frames 0, one raw -253 s.
    /// </summary>
    public class DisplayLatencyFilterTests
    {
        private const double Frame = 1.0 / 72.0;

        [Test]
        public void ZeroUntilFirstPlausibleReading()
        {
            var f = new DisplayLatencyFilter();
            Assert.AreEqual(0.0, f.Value, 1e-12);
            Assert.IsFalse(f.HasValue);
            f.Add(0.0, Frame);
            Assert.AreEqual(0.0, f.Value, 1e-12);
            Assert.IsFalse(f.HasValue);
        }

        [Test]
        public void FirstPlausibleReadingSeedsDirectly()
        {
            // No one-second ramp from 0: the first notes would be drawn uncompensated.
            var f = new DisplayLatencyFilter();
            f.Add(0.030, Frame);
            Assert.IsTrue(f.HasValue);
            Assert.AreEqual(0.030, f.Value, 1e-12);
        }

        [TestCase(0.0)]
        [TestCase(-253.0)]
        [TestCase(0.001)]
        [TestCase(0.5)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void ImplausibleReadingIsRejectedAndLastValueHeld(double raw)
        {
            var f = new DisplayLatencyFilter();
            f.Add(0.030, Frame);
            f.Add(raw, Frame);
            Assert.AreEqual(0.030, f.Value, 1e-12);
            Assert.AreEqual(1, f.RejectedCount);
            Assert.AreEqual(1, f.AcceptedCount);
        }

        [Test]
        public void SingleOutlierBarelyMovesTheValue()
        {
            // A plausible-but-high frame must not jerk the highway.
            var f = new DisplayLatencyFilter(1.0);
            f.Add(0.030, Frame);
            f.Add(0.090, Frame);
            Assert.Less(f.Value, 0.031);
        }

        [Test]
        public void ConvergesToAStepChangeOverAboutTheTimeConstant()
        {
            var f = new DisplayLatencyFilter(1.0);
            f.Add(0.020, Frame);
            for (int i = 0; i < 72; i++) f.Add(0.040, Frame); // 1 s
            // One time constant: 1 - 1/e = 63% of the way.
            Assert.AreEqual(0.020 + 0.020 * (1.0 - System.Math.Exp(-1.0)), f.Value, 1e-4);
            for (int i = 0; i < 72 * 5; i++) f.Add(0.040, Frame);
            Assert.AreEqual(0.040, f.Value, 1e-4);
        }

        [Test]
        public void SmoothingDependsOnElapsedTimeNotFrameCount()
        {
            var a = new DisplayLatencyFilter(1.0);
            var b = new DisplayLatencyFilter(1.0);
            a.Add(0.020, Frame);
            b.Add(0.020, Frame);
            for (int i = 0; i < 72; i++) a.Add(0.040, 1.0 / 72.0);
            for (int i = 0; i < 36; i++) b.Add(0.040, 1.0 / 36.0);
            Assert.AreEqual(a.Value, b.Value, 1e-9);
        }

        [Test]
        public void NoisyDeviceStreamStaysInsideTheGoodRange()
        {
            var f = new DisplayLatencyFilter(1.0);
            double[] raw = { 0.015, 0.044, 0.0, 0.030, -253.0, 0.025, 0.0, 0.040, 0.018 };
            for (int rep = 0; rep < 50; rep++)
                foreach (double r in raw)
                {
                    f.Add(r, Frame);
                    Assert.GreaterOrEqual(f.Value, 0.015);
                    Assert.LessOrEqual(f.Value, 0.044);
                }
        }

        [Test]
        public void ResetCountsKeepsValue()
        {
            var f = new DisplayLatencyFilter();
            f.Add(0.030, Frame);
            f.Add(0.0, Frame);
            f.ResetCounts();
            Assert.AreEqual(0, f.AcceptedCount);
            Assert.AreEqual(0, f.RejectedCount);
            Assert.AreEqual(0.030, f.Value, 1e-12);
        }
    }
}
