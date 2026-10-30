using System.Reflection;
using NUnit.Framework;
using PianoGlass.Song;
using UnityEngine;

namespace PianoGlass.Tests.EditMode
{
    /// <summary>
    /// SongClock is the one thing every falling-note position depends on, so
    /// it gets tested directly rather than trusted by inspection. Advance()
    /// is called straight from these tests, not through Update() -- edit mode
    /// does not run a player loop, and the whole point of a separate
    /// Advance(double) method is that it does not need one.
    /// </summary>
    public class SongClockTests
    {
        private static SongClock NewClock() =>
            new GameObject("SongClockUnderTest").AddComponent<SongClock>();

        /// <summary>AddComponent does NOT call Awake() here -- confirmed by
        /// running this suite: edit mode has no player loop, and Unity only
        /// sends lifecycle messages (Awake/OnEnable/Start/Update) through
        /// that loop, which only runs in Play Mode or a build. Invoking it
        /// directly is what actually exercises the method under test, rather
        /// than a fact about whether the test runner happens to call it.</summary>
        private static void InvokeAwake(SongClock clock) =>
            typeof(SongClock)
                .GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(clock, null);

        [TearDown]
        public void TearDown()
        {
            foreach (var clock in Object.FindObjectsByType<SongClock>(FindObjectsSortMode.None))
                Object.DestroyImmediate(clock.gameObject);
        }

        [Test]
        public void DefaultLeadInIsThreeSeconds()
        {
            SongClock clock = NewClock();
            Assert.AreEqual(3f, clock.LeadInSeconds, 1e-6f);
        }

        [Test]
        public void StartsAtNegativeLeadIn()
        {
            // Set in Awake, not just Restart() -- this is what makes the very
            // first play, before anything ever calls Restart(), still have a
            // lead-in instead of starting with the first note already due.
            SongClock clock = NewClock();
            InvokeAwake(clock);
            Assert.AreEqual(-(double)clock.LeadInSeconds, clock.SongTime, 1e-9);
        }

        [Test]
        public void AdvanceAccumulatesSeconds()
        {
            SongClock clock = NewClock();
            clock.Seek(0.0); // isolate the accumulation from the lead-in start
            clock.Advance(1.0);
            clock.Advance(0.5);
            Assert.AreEqual(1.5, clock.SongTime, 1e-9);
        }

        [Test]
        public void HalfTempoAdvancesAtHalfSpeed()
        {
            SongClock clock = NewClock();
            clock.Seek(0.0);
            clock.TempoMultiplier = 0.5f;
            clock.Advance(2.0);
            Assert.AreEqual(1.0, clock.SongTime, 1e-6);
        }

        [Test]
        public void DoubleTempoAdvancesAtDoubleSpeed()
        {
            SongClock clock = NewClock();
            clock.Seek(0.0);
            clock.TempoMultiplier = 2f;
            clock.Advance(1.0);
            Assert.AreEqual(2.0, clock.SongTime, 1e-6);
        }

        [Test]
        public void ZeroTempoFreezesTheClock()
        {
            SongClock clock = NewClock();
            clock.Seek(0.0);
            clock.TempoMultiplier = 0f;
            clock.Advance(5.0);
            Assert.AreEqual(0.0, clock.SongTime, 1e-9);
        }

        [Test]
        public void SeekSetsSongTimeDirectly()
        {
            SongClock clock = NewClock();
            clock.Advance(5.0);
            clock.Seek(1.25);
            Assert.AreEqual(1.25, clock.SongTime, 1e-9);
        }

        [Test]
        public void RestartReturnsToNegativeLeadIn()
        {
            SongClock clock = NewClock();
            clock.Advance(10.0);
            clock.Restart();
            Assert.AreEqual(-(double)clock.LeadInSeconds, clock.SongTime, 1e-9);
        }

        [Test]
        public void PlayAndPauseToggleIsPlaying()
        {
            SongClock clock = NewClock();
            Assert.IsFalse(clock.IsPlaying);
            clock.Play();
            Assert.IsTrue(clock.IsPlaying);
            clock.Pause();
            Assert.IsFalse(clock.IsPlaying);
        }

        [Test]
        public void DisplaySongTimeIsAheadByTheDisplayLatencyWhilePlaying()
        {
            SongClock clock = NewClock();
            clock.Seek(2.0);
            clock.Play();
            clock.DisplayLatencySeconds = 0.030;
            Assert.AreEqual(2.030, clock.DisplaySongTime, 1e-9);
            Assert.AreEqual(2.0, clock.SongTime, 1e-9, "judging time must not move");
        }

        [Test]
        public void DisplayLeadScalesWithTempo()
        {
            // At half speed the song moves half as far in the same 30 real ms.
            SongClock clock = NewClock();
            clock.Seek(2.0);
            clock.TempoMultiplier = 0.5f;
            clock.Play();
            clock.DisplayLatencySeconds = 0.030;
            Assert.AreEqual(2.015, clock.DisplaySongTime, 1e-9);
        }

        [Test]
        public void PausedSongIsDrawnWhereItIs()
        {
            SongClock clock = NewClock();
            clock.Seek(2.0);
            clock.DisplayLatencySeconds = 0.030;
            Assert.IsFalse(clock.IsPlaying);
            Assert.AreEqual(2.0, clock.DisplaySongTime, 1e-9);
        }

        [Test]
        public void DisplayLatencyIsClampedToAPlausibleRange()
        {
            SongClock clock = NewClock();
            clock.DisplayLatencySeconds = -0.5;
            Assert.AreEqual(0.0, clock.DisplayLatencySeconds, 1e-12);
            clock.DisplayLatencySeconds = 5.0;
            Assert.AreEqual(SongClock.MaxDisplayLatencySeconds, clock.DisplayLatencySeconds, 1e-12);
        }

        [Test]
        public void DisplayLatencyDefaultsToZero()
        {
            SongClock clock = NewClock();
            clock.Seek(1.0);
            clock.Play();
            Assert.AreEqual(1.0, clock.DisplaySongTime, 1e-9);
        }
    }
}
