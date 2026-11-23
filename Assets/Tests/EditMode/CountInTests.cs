using System;
using NUnit.Framework;
using UnityEngine;
using PianoGlass.Scoring;
using PianoGlass.Song;
using Object = UnityEngine.Object;

namespace PianoGlass.Tests.EditMode
{
    /// <summary>
    /// The resume count-in with the metronome on (round 15, Ryan's design):
    /// back to the start of the beat it was paused in, a whole number of
    /// beats, 2.5 to 5 seconds (3 until round 16), every dot or number one beat with its own
    /// click on the beat grid, the 1 ending exactly when the song plays,
    /// and nothing judged twice.
    /// </summary>
    public class CountInTests
    {
        private const double Latency = 0.085; // the metronome's output latency on the headset
        private const double Eps = 1e-6;

        private static readonly float[] Bpms = { 36f, 60f, 90f, 120f, 160f };
        private static readonly int[] Meters = { 3, 4 };
        /// <summary>Paused just after a beat, mid-beat and just before one, as a fraction of the beat.</summary>
        private static readonly double[] Into = { 0.02, 0.5, 0.98 };

        [Test]
        public void EveryTempoMeterAndPausePoint()
        {
            foreach (float bpm in Bpms)
            foreach (int meter in Meters)
            foreach (double into in Into)
                Check(bpm, meter, into);
        }

        private static void Check(float bpm, int meter, double into)
        {
            string at = $"{bpm} BPM {meter}/4, paused {into:0.00} into a beat";
            TempoMap map = TempoMap.Constant(bpm, meter);
            double beat = 60.0 / bpm;
            long k = 21; // part-way in: a bar line in 3/4, not in 4/4
            double pausedAt = k * beat + into * beat;

            ResumeCountdown.CountIn plan = ResumeCountdown.Plan(map, pausedAt, 1.0);

            // Back to the start of the beat it was paused in, never forward.
            Assert.AreEqual(k * beat, plan.RewindTo, Eps, at + ": backs up to the start of its beat");
            Assert.LessOrEqual(plan.RewindTo, pausedAt, at);
            Assert.AreEqual(map.Beat(k).IsBarLine, plan.OnBarLine, at);

            // A whole number of beats, at least 3, and 2.5 to 5 seconds.
            Assert.AreEqual(beat, plan.Step, Eps, at);
            Assert.GreaterOrEqual(plan.Beats, ResumeCountdown.From, at);
            Assert.GreaterOrEqual(plan.Seconds, 2.5 - Eps, at + ": at least 2.5 seconds");
            Assert.LessOrEqual(plan.Seconds, 5.0 + Eps, at + ": no more than 5 seconds");
            Assert.AreEqual(plan.Beats * plan.Step, plan.Seconds, Eps);

            var c = new ResumeCountdown();
            c.Start(0.0, Latency, plan.Step, plan.Beats);
            Assert.AreEqual(Math.Max(0, plan.Beats - 3), c.Dots, at);

            // Every dot or number lasts exactly one beat, and starts with its click.
            for (int i = 0; i < plan.Beats; i++)
            {
                double starts = Latency + i * plan.Step;
                foreach (double t in new[] { starts + Eps, starts + plan.Step * 0.5, starts + plan.Step - 1e-4 })
                {
                    Assert.AreEqual(i, c.BeatIndex(t), $"{at}: beat {i} shows through its whole beat");
                    if (i < c.Dots)
                    {
                        Assert.AreEqual(0, c.Number(t), $"{at}: beat {i} is a dot");
                        Assert.AreEqual(i + 1, c.FilledDots(t), $"{at}: one dot fills per click");
                    }
                    else
                    {
                        Assert.AreEqual(plan.Beats - i, c.Number(t), $"{at}: beat {i} is a number");
                    }
                }
                Assert.AreEqual(0.0, c.SinceBeat(starts), 1e-4, $"{at}: beat {i} starts on its click");
            }

            // Clicks: one a beat, then the song's own beat; all on the song's beat grid.
            for (int i = 0; i <= plan.Beats; i++)
            {
                double clickSong = plan.RewindTo - (plan.Beats - i) * beat; // where the click lands in the song's time
                double gridBeat = map.Beat(map.FirstBeatAtOrAfter(clickSong - Eps)).Time;
                Assert.AreEqual(gridBeat, clickSong, Eps, $"{at}: click {i} is on the beat grid");
            }

            // The 1 is there to the end, and the song plays exactly then, on the "play now" click.
            double plays = Latency + plan.Beats * plan.Step;
            Assert.AreEqual(1, c.Number(plays - 1e-4), at + ": the 1 lasts to the end");
            Assert.IsFalse(c.Finished(plays - 1e-4), at);
            Assert.IsTrue(c.Finished(plays + 1e-9), at + ": the song plays the moment the 1 ends");
            Assert.AreEqual(0, c.Number(plays + 1e-9), at + ": nothing left on screen");
        }

        [Test]
        public void HowManyBeats()
        {
            Assert.AreEqual(3, ResumeCountdown.BeatsFor(1.0), "60 BPM: 3, 2, 1, no dots");
            Assert.AreEqual(4, ResumeCountdown.BeatsFor(60.0 / 90.0), "90 BPM: one dot, 2.67 s");
            Assert.AreEqual(5, ResumeCountdown.BeatsFor(0.5), "120 BPM: 2.5 s exactly");
            Assert.AreEqual(7, ResumeCountdown.BeatsFor(0.375), "160 BPM: 2.63 s");
            Assert.AreEqual(3, ResumeCountdown.BeatsFor(60.0 / 36.0), "36 BPM: 3 beats is 5 s");
            Assert.AreEqual(3, ResumeCountdown.BeatsFor(2.5), "under 36 BPM it stays 3, 2, 1");
        }

        [Test]
        public void TheTempoSettingSetsTheBeat()
        {
            // 120 BPM at 50% plays at 60.
            ResumeCountdown.CountIn plan = ResumeCountdown.Plan(TempoMap.Constant(120f), 10.3, 0.5);
            Assert.AreEqual(10.0, plan.RewindTo, Eps);
            Assert.AreEqual(1.0, plan.Step, Eps);
            Assert.AreEqual(3, plan.Beats);
        }

        [Test]
        public void PausedRightOnABeatStaysPut()
        {
            ResumeCountdown.CountIn plan = ResumeCountdown.Plan(TempoMap.Constant(120f), 10.5, 1.0);
            Assert.AreEqual(10.5, plan.RewindTo, Eps);
        }

        [Test]
        public void PausedInTheLeadIn()
        {
            ResumeCountdown.CountIn plan = ResumeCountdown.Plan(TempoMap.Constant(120f), -1.3, 1.0);
            Assert.AreEqual(-1.5, plan.RewindTo, Eps, "the lead-in's beats go on before 0");
        }

        [Test]
        public void TheGlideIsShortSoftAndDoneInTheFirstBeat()
        {
            foreach (float bpm in Bpms)
            {
                double step = 60.0 / bpm;
                var c = new ResumeCountdown();
                c.Start(0.0, Latency, step, ResumeCountdown.BeatsFor(step));
                double glide = Math.Min(0.4, step);
                Assert.AreEqual(glide, c.GlideSeconds, Eps, $"{bpm} BPM");
                Assert.AreEqual(0.0, c.GlideProgress(0.0), Eps);
                Assert.AreEqual(1.0, c.GlideProgress(glide), Eps);
                Assert.Less(glide, Latency + step, $"{bpm} BPM: over before the second beat");
                if (c.Dots > 0) Assert.Less(glide, Latency + c.Dots * step, $"{bpm} BPM: over before the 3");

                double last = 0.0;
                for (int i = 1; i <= 20; i++)
                {
                    double p = c.GlideProgress(glide * i / 20.0);
                    Assert.GreaterOrEqual(p, last, "only ever backwards, never overshoots");
                    last = p;
                }
                Assert.Less(c.GlideProgress(glide * 0.05), 0.05, "starts softly");
                Assert.Greater(c.GlideProgress(glide * 0.95), 0.95, "stops softly");
            }
        }

        [Test]
        public void WithoutTheMetronomeItIsThreeSecondsOfNumbers()
        {
            var c = new ResumeCountdown();
            c.Start(10.0);
            Assert.AreEqual(0, c.Dots);
            Assert.AreEqual(3, c.Number(10.5));
            Assert.AreEqual(2, c.Number(11.5));
            Assert.AreEqual(1, c.Number(12.5));
            Assert.IsTrue(c.Finished(13.0));
        }

        // ---- Nothing judged twice -------------------------------------------

        private static SongData Scale() => new SongData("scale", 120f, new[]
        {
            new SongNote(60, 1.0, 0.25),
            new SongNote(62, 1.25, 0.25),
            new SongNote(64, 1.5, 0.25),
            new SongNote(65, 1.75, 0.25),
        });

        [Test]
        public void ANoteAlreadyPlayedIsNotScoredAgainAfterTheBackUp()
        {
            var s = new ScoringSession(Scale(), TimingWindows.Default);
            s.OnNoteOn(60, 1.0, 1.0);
            s.OnNoteOn(62, 1.26, 1.0);
            Assert.AreEqual(2, s.NotesHit);

            // Paused at 1.4, backed up to the beat at 1.0, and played again.
            s.ReplayUntil = 1.4;
            s.OnNoteOn(60, 1.01, 1.0);
            s.OnNoteOn(62, 1.25, 1.0);
            Assert.AreEqual(2, s.NotesHit, "not hit twice");
            Assert.AreEqual(2, s.JudgedCount);
            Assert.AreEqual(0, s.WrongNotes.Count, "not wrong either");

            // The notes after the pause count as normal.
            s.OnNoteOn(64, 1.5, 1.0);
            Assert.AreEqual(3, s.NotesHit);
            s.OnNoteOn(67, 1.75, 1.0);
            Assert.AreEqual(1, s.WrongNotes.Count, "a real wrong note still is one");
        }

        [Test]
        public void AMissBeforeThePauseStaysAMiss()
        {
            var s = new ScoringSession(Scale(), TimingWindows.Default);
            s.Advance(1.3, 1.0); // 60 went by unplayed
            Assert.AreEqual(1, s.NotesMissed);
            s.ReplayUntil = 1.3;
            s.OnNoteOn(60, 1.0, 1.0);
            Assert.AreEqual(1, s.NotesMissed);
            Assert.AreEqual(0, s.NotesHit);
            Assert.AreEqual(0, s.WrongNotes.Count);
        }

        [Test]
        public void WaitModeReplaysAreNotWrongEither()
        {
            var s = new ScoringSession(Scale(), TimingWindows.Default, pitchOnly: true);
            s.OnNoteOn(60, 1.0, 1.0);
            s.ReplayUntil = 1.1;
            s.OnNoteOn(60, 1.02, 1.0);
            Assert.AreEqual(1, s.NotesHit);
            Assert.AreEqual(0, s.WrongNotes.Count);
        }

        // ---- The clock --------------------------------------------------------

        private SongClock _clock;

        [SetUp]
        public void MakeClock() => _clock = new GameObject("clock").AddComponent<SongClock>();

        [TearDown]
        public void DropClock() => Object.DestroyImmediate(_clock.gameObject);

        [Test]
        public void TheGlideIsOnlyDrawn()
        {
            _clock.Seek(10.4);
            _clock.ShowAt(10.2);
            Assert.AreEqual(10.2, _clock.DisplaySongTime, Eps, "the highway draws the glide");
            Assert.AreEqual(10.4, _clock.SongTime, Eps, "judging still sees where it is");
            Assert.IsTrue(_clock.IsGliding);

            _clock.RewindForResume(10.0);
            Assert.IsFalse(_clock.IsGliding);
            Assert.AreEqual(10.0, _clock.SongTime, Eps);
            Assert.AreEqual(10.0, _clock.DisplaySongTime, Eps);
            Assert.AreEqual(1, _clock.ResumeRewinds, "told apart from a restart");
            Assert.AreEqual(10.4, _clock.ReplayUntil, Eps);
        }

        [Test]
        public void ItNeverBacksUpForwards()
        {
            _clock.Seek(10.0);
            _clock.RewindForResume(10.5);
            Assert.AreEqual(10.0, _clock.SongTime, Eps);
            Assert.AreEqual(0, _clock.ResumeRewinds);
        }

        [Test]
        public void ASeekOrRestartEndsTheGlide()
        {
            _clock.ShowAt(3.0);
            _clock.Restart();
            Assert.IsFalse(_clock.IsGliding);
            _clock.ShowAt(3.0);
            _clock.Seek(1.0);
            Assert.IsFalse(_clock.IsGliding);
        }
    }
}
