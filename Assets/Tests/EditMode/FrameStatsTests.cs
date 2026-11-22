using NUnit.Framework;
using PianoGlass.Song;

namespace PianoGlass.Tests.EditMode
{
    public class FrameStatsTests
    {
        [Test]
        public void SteadyFramesAverageTheHeadsetRate()
        {
            var s = new FrameStats();
            for (int i = 0; i < 720; i++) s.Add(1.0 / 72.0);
            Assert.AreEqual(72.0, s.AverageFps, 0.01);
            Assert.AreEqual(0, s.Slow);
            Assert.AreEqual(13.9, s.WorstMs, 0.05);
        }

        [Test]
        public void ARepeatedFrameCountsAsSlow()
        {
            var s = new FrameStats();
            s.Add(1.0 / 72.0);
            s.Add(2.0 / 72.0); // 27.8 ms: one frame shown twice
            Assert.AreEqual(1, s.Slow);
            Assert.AreEqual(27.8, s.WorstMs, 0.05);
        }

        [Test]
        public void TakingTheHeadsetOffIsNotASlowFrame()
        {
            var s = new FrameStats();
            s.Add(1.0 / 72.0);
            s.Add(12.0); // paused for 12 s
            Assert.AreEqual(1, s.Frames);
            Assert.AreEqual(0, s.Slow);
            Assert.AreEqual(72.0, s.AverageFps, 0.01);
        }

        [Test]
        public void ClearStartsOver()
        {
            var s = new FrameStats();
            s.Add(0.03);
            s.Clear();
            Assert.AreEqual(0, s.Frames);
            Assert.AreEqual(0.0, s.AverageFps);
            StringAssert.Contains("0 frames", s.Summary("Twinkle"));
        }
    }
}
