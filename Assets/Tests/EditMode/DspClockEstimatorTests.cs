using System;
using NUnit.Framework;
using PianoGlass.Song;

namespace PianoGlass.Tests.EditMode
{
    public class DspClockEstimatorTests
    {
        /// <summary>The Android picture: dspTime advances in 1024-sample
        /// buffers at 48 kHz (21.3 ms steps) and is read once per frame at
        /// 72 Hz. Raw reads lag by up to a buffer; the estimate must not.</summary>
        [Test]
        public void SteppedReadsGiveASteadyEstimate()
        {
            const double buffer = 1024.0 / 48000.0, frame = 1.0 / 72.0, offset = 1000.0;
            var e = new DspClockEstimator();
            double worstRaw = 0, worstEstimate = 0;
            for (int i = 0; i < 400; i++)
            {
                double real = 5.0 + i * frame;
                double trueDsp = real + offset;
                double raw = Math.Floor(trueDsp / buffer) * buffer;     // last completed buffer
                e.Observe(real, raw);
                if (i < 100) continue;                                   // let it learn
                worstRaw = Math.Max(worstRaw, trueDsp - raw);
                worstEstimate = Math.Max(worstEstimate, Math.Abs(e.Estimate(real) - trueDsp));
            }
            Assert.Greater(worstRaw, 0.015, "the raw reads really are stale");
            Assert.Less(worstEstimate, 0.002, "the estimate is within 2 ms");
        }

        [Test]
        public void RepeatedReadsOfTheSameBufferTeachNothing()
        {
            var e = new DspClockEstimator();
            e.Observe(1.0, 101.0);
            e.Observe(1.5, 101.0);   // same buffer, read later: would look 0.5 s stale
            Assert.AreEqual(102.0, e.Estimate(2.0), 1e-9);
        }

        [Test]
        public void AJumpResetsTheEstimate()
        {
            var e = new DspClockEstimator();
            e.Observe(1.0, 101.0);
            e.Observe(2.0, 102.0);
            // Audio restarted after a pause: dsp is now 50 s behind.
            e.Observe(3.0, 53.0);
            Assert.AreEqual(54.0, e.Estimate(4.0), 1e-9);
        }

        [Test]
        public void BeforeAnyReadItReturnsRealtime()
        {
            var e = new DspClockEstimator();
            Assert.IsFalse(e.HasEstimate);
            Assert.AreEqual(7.0, e.Estimate(7.0), 1e-9);
        }
    }
}
