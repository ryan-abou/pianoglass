using System.Collections.Generic;
using NUnit.Framework;
using PianoGlass.Song;

namespace PianoGlass.Tests.EditMode
{
    public class TempoMapTests
    {
        private const int Ticks = 480;
        private const double Eps = 1e-9;

        private static MidiTempoChange Tempo(long tick, double bpm) =>
            new MidiTempoChange(tick, (int)System.Math.Round(60000000.0 / bpm));

        private static MidiTimeSignature Sig(long tick, int top, int bottom) =>
            new MidiTimeSignature(tick, top, bottom);

        private static TempoMap Map(long endTick, MidiTempoChange[] tempos, params MidiTimeSignature[] sigs) =>
            TempoMap.FromMidi(Ticks, tempos, sigs, endTick);

        private static List<BeatLine> Lines(TempoMap map, double from, double to)
        {
            var into = new List<BeatLine>();
            BeatGrid.CollectVisible(map, from, to, from, to - from, into);
            return into;
        }

        [Test]
        public void TicksBecomeSecondsAcrossTempoChanges()
        {
            // 120 BPM for 2 beats (1 s), then 60 BPM (1 s per beat).
            TempoMap map = Map(Ticks * 8, new[] { Tempo(0, 120), Tempo(Ticks * 2, 60) });
            Assert.AreEqual(0.0, map.TicksToSeconds(0), Eps);
            Assert.AreEqual(0.5, map.TicksToSeconds(Ticks), Eps);
            Assert.AreEqual(1.0, map.TicksToSeconds(Ticks * 2), Eps);
            Assert.AreEqual(2.0, map.TicksToSeconds(Ticks * 3), Eps);
            Assert.AreEqual(1.5, map.TicksToSeconds(Ticks * 2 + Ticks / 2), Eps);
        }

        [Test]
        public void TempoChangesCanArriveOutOfOrder()
        {
            TempoMap map = Map(Ticks * 8, new[] { Tempo(Ticks * 2, 60), Tempo(0, 120) });
            Assert.AreEqual(2.0, map.TicksToSeconds(Ticks * 3), Eps);
        }

        [Test]
        public void NoTempoOrTimeSignatureMeans120And44()
        {
            TempoMap map = Map(Ticks * 4, new MidiTempoChange[0]);
            Assert.AreEqual(120f, map.FirstBpm, 0.001f);
            Assert.AreEqual(4, map.FirstBeatsPerBar);
            Assert.AreEqual(4, map.FirstBeatUnit);
            Assert.AreEqual(0.5, map.TicksToSeconds(Ticks), Eps);
        }

        [Test]
        public void TheFirstBpmIsTheFirstTempo()
        {
            TempoMap map = Map(Ticks * 8, new[] { Tempo(0, 90), Tempo(Ticks * 4, 150) });
            Assert.AreEqual(90f, map.FirstBpm, 0.01f);
        }

        [Test]
        public void BarsAndBeatsFollowAFourFourToThreeFourChange()
        {
            // One bar of 4/4 (2 s at 120), then 3/4 bars of 1.5 s.
            TempoMap map = Map(Ticks * 10, new[] { Tempo(0, 120) }, Sig(0, 4, 4), Sig(Ticks * 4, 3, 4));

            Assert.IsFalse(map.BarLinesMayBeOff);
            Assert.AreEqual(0.0, map.BarStartTime(1), Eps);
            Assert.AreEqual(2.0, map.BarStartTime(2), Eps);
            Assert.AreEqual(3.5, map.BarStartTime(3), Eps);
            Assert.AreEqual(5.0, map.BarStartTime(4), Eps);

            Assert.AreEqual(1, map.BarOf(1.99));
            Assert.AreEqual(2, map.BarOf(2.0));
            Assert.AreEqual(2, map.BarOf(3.49));
            Assert.AreEqual(3, map.BarOf(3.5));

            BeatLine beat = map.Beat(4);
            Assert.AreEqual(2.0, beat.Time, Eps);
            Assert.AreEqual(1, beat.BeatInBar);
            Assert.AreEqual(2, beat.Bar);
            Assert.AreEqual(3, map.Beat(6).BeatInBar);
            Assert.AreEqual(1, map.Beat(7).BeatInBar);
            Assert.AreEqual(3, map.Beat(7).Bar);
        }

        [Test]
        public void SixEightHasSixEighthNoteBeats()
        {
            TempoMap map = Map(Ticks * 3, new[] { Tempo(0, 120) }, Sig(0, 6, 8));
            Assert.AreEqual(6, map.FirstBeatsPerBar);
            Assert.AreEqual(8, map.FirstBeatUnit);
            Assert.AreEqual("6/8", map.TimeSignatureText);

            for (int k = 0; k < 6; k++)
            {
                Assert.AreEqual(k * 0.25, map.Beat(k).Time, Eps, $"beat {k}");
                Assert.AreEqual(k + 1, map.Beat(k).BeatInBar);
                Assert.AreEqual(1, map.Beat(k).Bar);
            }
            Assert.AreEqual(1, map.Beat(6).BeatInBar);
            Assert.AreEqual(2, map.Beat(6).Bar);
        }

        [Test]
        public void TheLeadInRunsBackwardsAtTheFirstTempoAndTimeSignature()
        {
            TempoMap map = Map(Ticks * 8, new[] { Tempo(0, 120), Tempo(Ticks, 40) }, Sig(0, 3, 4));
            BeatLine last = map.Beat(-1);
            Assert.AreEqual(-0.5, last.Time, Eps);
            Assert.AreEqual(3, last.BeatInBar);
            Assert.AreEqual(0, last.Bar);

            BeatLine barLine = map.Beat(-3);
            Assert.AreEqual(-1.5, barLine.Time, Eps);
            Assert.AreEqual(1, barLine.BeatInBar);
            Assert.AreEqual(0, barLine.Bar);

            Assert.AreEqual(-1.5, map.BarStartTime(0), Eps);
        }

        [Test]
        public void ATimeSignatureChangeMidBarGivesTheWarningAndStartsABar()
        {
            TempoMap map = Map(Ticks * 8, new[] { Tempo(0, 120) }, Sig(0, 4, 4), Sig(Ticks * 2, 3, 4));
            Assert.IsTrue(map.BarLinesMayBeOff);
            Assert.AreEqual(1.0, map.BarStartTime(2), Eps);
            Assert.AreEqual(2, map.BarOf(1.0));
        }

        [Test]
        public void AChangeBetweenBeatsAlsoWarns()
        {
            TempoMap map = Map(Ticks * 8, new[] { Tempo(0, 120) }, Sig(0, 4, 4), Sig(Ticks + 10, 3, 4));
            Assert.IsTrue(map.BarLinesMayBeOff);
        }

        [Test]
        public void RepeatingTheSameTimeSignatureMidBarIsNotAChange()
        {
            TempoMap map = Map(Ticks * 8, new[] { Tempo(0, 120) }, Sig(0, 4, 4), Sig(Ticks * 2, 4, 4));
            Assert.IsFalse(map.BarLinesMayBeOff);
            Assert.AreEqual(2.0, map.BarStartTime(2), Eps);
        }

        [Test]
        public void BarLinesStayOnNotesAfterATempoChange()
        {
            TempoMap map = Map(Ticks * 16, new[] { Tempo(0, 120), Tempo(Ticks * 4, 80) });
            // A note on bar 3's first beat, tick 3840 = 8 beats.
            double noteTime = map.TicksToSeconds(Ticks * 8);
            Assert.AreEqual(noteTime, map.BarStartTime(3), Eps);
            Assert.AreEqual(3, map.BarOf(noteTime));
            Assert.AreEqual(noteTime, map.Beat(8).Time, Eps);
        }

        [Test]
        public void TheListEndsOnTheBarLineAfterTheLastNote()
        {
            // Ends 2 beats into bar 2: the closing line is bar 3's first beat.
            TempoMap map = Map(Ticks * 6, new[] { Tempo(0, 120) });
            List<BeatLine> lines = Lines(map, 0.0, 4.0);
            Assert.AreEqual(9, lines.Count);
            Assert.AreEqual(4.0, lines[8].Time, Eps);
            Assert.AreEqual(3, lines[8].Bar);
        }

        [Test]
        public void BeatsPastTheEndCarryOn()
        {
            TempoMap map = Map(Ticks * 4, new[] { Tempo(0, 120), Tempo(Ticks * 2, 60) }, Sig(0, 4, 4));
            // Beats 0-1 at 0.5 s, then 1 s each from beat 2.
            Assert.AreEqual(3.0, map.Beat(4).Time, Eps);
            Assert.AreEqual(4.0, map.Beat(5).Time, Eps);
            Assert.AreEqual(2, map.Beat(5).BeatInBar);
            Assert.AreEqual(2, map.Beat(5).Bar);
            Assert.AreEqual(2, map.BarOf(3.5));
        }

        [Test]
        public void AOneTempoSongMatchesTheOldBpmMathsExactly()
        {
            const float bpm = 120f;
            const int perBar = 4;
            double beat = 60.0 / bpm;
            double bar = beat * perBar;

            TempoMap fromMidi = Map(Ticks * 32, new[] { Tempo(0, bpm) }, Sig(0, perBar, 4));
            TempoMap constant = TempoMap.Constant(bpm, perBar);

            foreach (TempoMap map in new[] { fromMidi, constant })
            {
                for (long k = -8; k < 64; k++)
                {
                    BeatLine line = map.Beat(k);
                    Assert.AreEqual(k * beat, line.Time, Eps, $"time of beat {k}");
                    Assert.AreEqual(BeatGrid.BeatInBar(k, perBar), line.BeatInBar, $"beat in bar {k}");
                    Assert.AreEqual(BeatGrid.BarOf(k, perBar), line.Bar, $"bar of beat {k}");
                }

                for (int b = 0; b < 20; b++) Assert.AreEqual((b - 1) * bar, map.BarStartTime(b), Eps, $"bar {b}");

                for (double t = -3.0; t < 40.0; t += 0.123)
                {
                    int old = t <= 0.0 ? 1 : (int)System.Math.Floor(t / bar + 1e-9) + 1;
                    Assert.AreEqual(old, map.BarOf(t), $"at {t}");
                }

                Assert.AreEqual(bar, map.FirstBarSeconds, Eps);
                Assert.AreEqual(bpm, map.FirstBpm, 0.001f);
            }
        }

        [Test]
        public void CollectVisibleMatchesTheOldBpmVersion()
        {
            var old = new List<BeatLine>();
            var now = new List<BeatLine>();
            TempoMap map = TempoMap.Constant(120f, 4);

            for (double t = -3.0; t < 18.0; t += 0.37)
            {
                BeatGrid.CollectVisible(120f, 4, -3.0, 16.0, t, 3.0, old);
                BeatGrid.CollectVisible(map, -3.0, 16.0, t, 3.0, now);
                Assert.AreEqual(old.Count, now.Count, $"at {t}");
                for (int i = 0; i < old.Count; i++)
                {
                    Assert.AreEqual(old[i].Time, now[i].Time, Eps);
                    Assert.AreEqual(old[i].BeatInBar, now[i].BeatInBar);
                    Assert.AreEqual(old[i].Bar, now[i].Bar);
                }
            }
        }

        [Test]
        public void CollectVisibleCrossesATempoChange()
        {
            TempoMap map = Map(Ticks * 8, new[] { Tempo(0, 120), Tempo(Ticks * 4, 60) });
            var into = new List<BeatLine>();
            BeatGrid.CollectVisible(map, -3.0, 100.0, 1.0, 2.0, into);
            // 1.0, 1.5, 2.0 at 120 BPM, then 3.0 once it slows to 1 s a beat.
            Assert.AreEqual(4, into.Count);
            Assert.AreEqual(1.0, into[0].Time, Eps);
            Assert.AreEqual(3.0, into[3].Time, Eps);
        }

        [Test]
        public void ABeatExactlyOnTheEdgeIsKept()
        {
            TempoMap map = Map(Ticks * 8, new[] { Tempo(0, 120) });
            var into = new List<BeatLine>();
            BeatGrid.CollectVisible(map, -3.0, 100.0, 1.0, 1.0, into);
            Assert.AreEqual(1.0, into[0].Time, Eps);
            Assert.AreEqual(2.0, into[into.Count - 1].Time, Eps);
        }

        [Test]
        public void ZeroBpmHasNoBeats()
        {
            TempoMap map = TempoMap.Constant(0f, 4);
            Assert.IsFalse(map.HasBeats);
            Assert.AreEqual(1, map.BarOf(5.0));
            Assert.AreEqual(0.0, map.BarStartTime(3), Eps);
            var into = new List<BeatLine>();
            Assert.AreEqual(0, BeatGrid.CollectVisible(map, -3.0, 16.0, 0.0, 3.0, into));
        }

        [Test]
        public void AnAbsurdlyLongFileIsCaught()
        {
            TempoMap map = TempoMap.FromMidi(1, new[] { Tempo(0, 120) }, new MidiTimeSignature[0], int.MaxValue);
            Assert.IsTrue(map.TooLong);
        }

        [Test]
        public void SongDataGoesThroughTheMap()
        {
            TempoMap map = Map(Ticks * 7, new[] { Tempo(0, 120) }, Sig(0, 4, 4), Sig(Ticks * 4, 3, 4));
            var song = new SongData("changes", new[] { new SongNote(60, 3.0, 0.5) }, map);

            Assert.AreEqual(120f, song.Bpm, 0.001f);
            Assert.AreEqual(4, song.BeatsPerBar);
            Assert.AreEqual(2, song.BarCount);
            Assert.AreEqual(2.0, song.BarStartTime(2), Eps);
            Assert.AreEqual(2, song.BarOf(3.0));
        }

        [Test]
        public void ALoopFollowsBarsOfDifferentLengths()
        {
            TempoMap map = Map(Ticks * 13, new[] { Tempo(0, 120) }, Sig(0, 4, 4), Sig(Ticks * 4, 3, 4));
            var song = new SongData("changes", new[] { new SongNote(60, 6.0, 0.5) }, map);

            LoopRange loop = LoopRange.Between(song, 2, 2);
            Assert.AreEqual(2.0, loop.NotesFrom, Eps);
            Assert.AreEqual(3.5, loop.NotesUntil, Eps);
            Assert.AreEqual(0.0, loop.WrapTo, Eps, "one bar of count-in: the 4/4 bar before it");
        }

        [Test]
        public void TheMetronomeReadsBeatsFromTheMap()
        {
            TempoMap map = Map(Ticks * 8, new[] { Tempo(0, 120), Tempo(Ticks * 4, 60) });
            var schedule = new MetronomeSchedule(3.0);
            var clicks = new List<MetronomeClick>();

            schedule.Collect(map, -3.0, 100.0, 1.9, 1.0, 100.0, clicks);

            Assert.AreEqual(3, clicks.Count);
            Assert.AreEqual(2.0, clicks[0].SongTime, Eps);
            Assert.IsTrue(clicks[0].Accent);
            Assert.AreEqual(3.0, clicks[1].SongTime, Eps);
            Assert.AreEqual(4.0, clicks[2].SongTime, Eps);
        }
    }
}
