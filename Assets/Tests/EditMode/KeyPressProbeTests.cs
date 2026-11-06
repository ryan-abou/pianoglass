using System.Collections.Generic;
using NUnit.Framework;
using PianoGlass.Piano;
using UnityEngine;

namespace PianoGlass.Tests.EditMode
{
    /// <summary>A key press against the tracked fingertips: the measurement for
    /// "learn from key presses".</summary>
    public class KeyPressProbeTests
    {
        private const int Lowest = 21, Highest = 108, C4 = 60;
        private static readonly float Span = (PianoKeyLayout.CountWhiteKeys(Lowest, Highest) - 1) * PianoKeyLayout.StandardWhiteKeySpacing;

        private static PianoKeyLayout Layout() =>
            PianoKeyLayout.WithFacing(new Vector3(-Span / 2f, 0.75f, 0.3f), new Vector3(Span / 2f, 0.75f, 0.3f),
                                      Lowest, Highest, Vector3.back, 0.147f);

        private static Vector3 At(PianoKeyLayout layout, float dxMm, float dyMm, float dzMm)
        {
            layout.TryGetKey(C4, out PianoKey key);
            return key.Center + (layout.Right * dxMm + layout.Up * dyMm + layout.Forward * dzMm) * 0.001f;
        }

        [Test]
        public void ReadsTheOffsetAlongTheKeyboardsOwnAxes()
        {
            PianoKeyLayout l = Layout();
            PressReading r = KeyPressProbe.Measure(l, C4, new List<Vector3> { At(l, 4f, 9f, -30f) });
            Assert.IsTrue(r.Found);
            Assert.AreEqual(4f, r.Dx, 0.01f);
            Assert.AreEqual(9f, r.Dy, 0.01f);
            Assert.AreEqual(-30f, r.Dz, 0.01f);
            Assert.IsNaN(r.NextDx);
        }

        [Test]
        public void TheNearestFingerSidewaysIsTheOneReadAndTheNextIsReported()
        {
            PianoKeyLayout l = Layout();
            PressReading r = KeyPressProbe.Measure(l, C4, new List<Vector3>
            {
                At(l, -30f, 2f, 0f), At(l, 5f, 3f, 0f), At(l, 26f, 4f, 0f),
            });
            Assert.AreEqual(5f, r.Dx, 0.01f);
            Assert.AreEqual(3f, r.Dy, 0.01f);
            Assert.AreEqual(26f, r.NextDx, 0.01f);
        }

        [Test]
        public void AFingerTooFarAwayDidNotPressIt()
        {
            PianoKeyLayout l = Layout();
            Assert.IsFalse(KeyPressProbe.Measure(l, C4, new List<Vector3> { At(l, 60f, 0f, 0f) }).Found);
            Assert.IsFalse(KeyPressProbe.Measure(l, C4, new List<Vector3>()).Found);
        }

        [Test]
        public void AKeyOffTheKeyboardIsNotARead()
        {
            PianoKeyLayout l = Layout();
            Assert.IsFalse(KeyPressProbe.Measure(l, 12, new List<Vector3> { At(l, 0f, 0f, 0f) }).Found);
        }

        [Test]
        public void TheMedianIgnoresAStrayPressAndTheSpreadShowsTheNoise()
        {
            var stats = new PressStats();
            foreach (float dx in new[] { 8f, 10f, 12f, 11f, 40f })
                stats.Add(new PressReading(dx, 5f, -20f, float.NaN));

            Assert.AreEqual(5, stats.Count);
            Assert.AreEqual(11f, stats.MedianDx, 0.01f);
            Assert.AreEqual(1f, stats.SpreadDx, 0.01f);
            Assert.AreEqual(5f, stats.MedianDy, 0.01f);
            Assert.AreEqual(-20f, stats.MedianDz, 0.01f);
        }

        [Test]
        public void AMissedReadingCountsForNothing()
        {
            var stats = new PressStats();
            stats.Add(default);
            Assert.AreEqual(0, stats.Count);
            Assert.AreEqual(0f, stats.MedianDx);
        }
    }
}
