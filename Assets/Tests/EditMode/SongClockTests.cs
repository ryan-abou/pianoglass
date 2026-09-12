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

        [TearDown]
        public void TearDown()
        {
            foreach (var clock in Object.FindObjectsByType<SongClock>(FindObjectsSortMode.None))
                Object.DestroyImmediate(clock.gameObject);
        }

        [Test]
        public void StartsAtZero()
        {
            SongClock clock = NewClock();
            Assert.AreEqual(0.0, clock.SongTime, 1e-9);
        }

        [Test]
        public void AdvanceAccumulatesSeconds()
        {
            SongClock clock = NewClock();
            clock.Advance(1.0);
            clock.Advance(0.5);
            Assert.AreEqual(1.5, clock.SongTime, 1e-9);
        }

        [Test]
        public void HalfTempoAdvancesAtHalfSpeed()
        {
            SongClock clock = NewClock();
            clock.TempoMultiplier = 0.5f;
            clock.Advance(2.0);
            Assert.AreEqual(1.0, clock.SongTime, 1e-6);
        }

        [Test]
        public void DoubleTempoAdvancesAtDoubleSpeed()
        {
            SongClock clock = NewClock();
            clock.TempoMultiplier = 2f;
            clock.Advance(1.0);
            Assert.AreEqual(2.0, clock.SongTime, 1e-6);
        }

        [Test]
        public void ZeroTempoFreezesTheClock()
        {
            SongClock clock = NewClock();
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
        public void RestartReturnsToZero()
        {
            SongClock clock = NewClock();
            clock.Advance(10.0);
            clock.Restart();
            Assert.AreEqual(0.0, clock.SongTime, 1e-9);
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
    }
}
