using System.Collections.Generic;
using NUnit.Framework;
using PianoGlass.Song;

namespace PianoGlass.Tests.EditMode
{
    /// <summary>Metronome (F27) timing: Twinkle's 120 BPM, 4/4, a beat every
    /// 0.5 s, 3 s of lead-in.</summary>
    public class MetronomeScheduleTests
    {
        private const float Bpm = 120f;
        private const int Bpb = 4;
        private const double LeadIn = -3.0, End = 16.0;

        private readonly List<MetronomeClick> _out = new List<MetronomeClick>();

        private int Collect(MetronomeSchedule s, double songTime, double tempo = 1.0, double dsp = 100.0) =>
            s.Collect(Bpm, Bpb, LeadIn, End, songTime, tempo, dsp, _out);

        [Test]
        public void SchedulesBeatsWithinTheLookahead()
        {
            var s = new MetronomeSchedule(lookaheadSeconds: 0.3);
            Collect(s, songTime: 0.9);
            Assert.AreEqual(1, _out.Count, "only the beat at 1.0 s is within 0.3 s");
            Assert.AreEqual(1.0, _out[0].SongTime, 1e-9);
            Assert.AreEqual(100.1, _out[0].DspTime, 1e-9);
        }

        [Test]
        public void NeverSchedulesABeatTwice()
        {
            var s = new MetronomeSchedule(0.3);
            Collect(s, 0.9);
            Assert.AreEqual(1, _out.Count);
            Collect(s, 0.95, dsp: 100.05);
            Assert.AreEqual(0, _out.Count);
            Collect(s, 1.25, dsp: 100.35);
            Assert.AreEqual(1, _out.Count);
            Assert.AreEqual(1.5, _out[0].SongTime, 1e-9);
        }

        [Test]
        public void ResetAllowsReschedulingAfterAPause()
        {
            var s = new MetronomeSchedule(0.3);
            Collect(s, 0.9);
            s.Reset();
            Collect(s, 0.9, dsp: 200.0);
            Assert.AreEqual(1, _out.Count);
            Assert.AreEqual(200.1, _out[0].DspTime, 1e-9);
        }

        [Test]
        public void OutputLatencyIsTakenOff()
        {
            var s = new MetronomeSchedule(0.3, outputLatencySeconds: 0.05);
            Collect(s, 0.9);
            Assert.AreEqual(100.05, _out[0].DspTime, 1e-9);
        }

        [Test]
        public void TempoStretchesTheGapInRealTime()
        {
            // Half speed: 0.1 s of song is 0.2 s of real time.
            Assert.AreEqual(100.2, MetronomeSchedule.DspTimeFor(1.0, 0.9, 100.0, 0.5, 0.0), 1e-9);
            // 0.3 s of real lookahead covers 0.3 s of song at full speed but
            // only 0.15 s at half: a beat 0.2 s of song away is in the first.
            var full = new MetronomeSchedule(0.3);
            Collect(full, 0.8, tempo: 1.0);
            Assert.AreEqual(1, _out.Count);
            var half = new MetronomeSchedule(0.3);
            Collect(half, 0.8, tempo: 0.5);
            Assert.AreEqual(0, _out.Count);
        }

        [Test]
        public void TheLeadInClicksAndItsBarStartIsAccented()
        {
            var s = new MetronomeSchedule(lookaheadSeconds: 4.0);
            Collect(s, songTime: -3.0);
            // -3.0 .. 1.0 at 0.5 s: -3 -2.5 -2 -1.5 -1 -0.5 0 0.5 1
            Assert.AreEqual(9, _out.Count);
            Assert.AreEqual(-3.0, _out[0].SongTime, 1e-9);
            Assert.IsTrue(_out[2].Accent, "-2.0 s is beat -4: the start of the count-in bar");
            Assert.IsTrue(_out[6].Accent, "0 s, bar 1");
            Assert.IsFalse(_out[7].Accent);
        }

        [Test]
        public void AClickAlreadyInThePastIsSkipped()
        {
            // 80 ms latency, beat only 50 ms away: it cannot start on time.
            var s = new MetronomeSchedule(0.3, outputLatencySeconds: 0.08);
            Collect(s, 0.95);
            Assert.AreEqual(0, _out.Count);
            Collect(s, 0.96);
            Assert.AreEqual(0, _out.Count, "and it is not retried late");
        }

        [Test]
        public void NothingPastTheEndOfTheSong()
        {
            var s = new MetronomeSchedule(1.0);
            Collect(s, 15.8);
            Assert.AreEqual(1, _out.Count, "16.0 s only");
            Assert.AreEqual(16.0, _out[0].SongTime, 1e-9);
        }

        [Test]
        public void StoppedTempoSchedulesNothing()
        {
            var s = new MetronomeSchedule(0.3);
            Assert.AreEqual(0, Collect(s, 0.9, tempo: 0.0));
        }
    }
}
