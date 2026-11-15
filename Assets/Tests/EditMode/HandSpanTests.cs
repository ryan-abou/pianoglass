using NUnit.Framework;
using PianoGlass.Piano;
using UnityEngine;

namespace PianoGlass.Tests.EditMode
{
    public class HandSpanTests
    {
        // 2026-11-14, by hand: A0 and C8 measured 23.0 mm white keys; the piano's are 23.5 mm.
        private static readonly Vector3 A0 = new Vector3(-0.74f, 1.29f, 0.10f);
        private static readonly Vector3 C8 = new Vector3(0.39f, 1.29f, 0.41f);

        [Test]
        public void AShortHandSpanGrowsEvenlyToTheKnownWidth()
        {
            Vector3 low = A0, high = C8;
            Assert.IsTrue(HandSpan.KeepSpacing(ref low, ref high, 0.0230f, 0.0235f));
            Assert.AreEqual(Vector3.Distance(A0, C8) * 0.0235f / 0.0230f, Vector3.Distance(low, high), 1e-5f);
            Assert.Less(Vector3.Distance((A0 + C8) * 0.5f, (low + high) * 0.5f), 1e-6f, "middle stays");
            Assert.Less(Vector3.Angle(C8 - A0, high - low), 0.01f, "direction stays");
        }

        [Test]
        public void FarOffTheLastWidthTheHandIsBelieved()
        {
            Vector3 low = A0, high = C8;
            Assert.IsFalse(HandSpan.KeepSpacing(ref low, ref high, 0.0200f, 0.0235f));
            Assert.AreEqual(A0, low);
            Assert.AreEqual(C8, high);
        }

        [Test]
        public void NoLastWidthChangesNothing()
        {
            Vector3 low = A0, high = C8;
            Assert.IsFalse(HandSpan.KeepSpacing(ref low, ref high, 0.0230f, 0f));
            Assert.AreEqual(C8, high);
        }
    }
}
