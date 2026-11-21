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
        public void ASmallTurnIsTakenOffAboutTheMiddle()
        {
            // C8 pressed 2 cm further back than A0: about 1 degree of turn.
            Vector3 low = new Vector3(-0.6f, 1.28f, 0.51f), high = new Vector3(0.6f, 1.28f, 0.53f);
            Vector3 middle = (low + high) * 0.5f;
            float length = Vector3.Distance(low, high);
            Assert.IsTrue(HandSpan.KeepTurn(ref low, ref high, Vector3.right, Vector3.up));
            Assert.AreEqual(0f, high.z - low.z, 1e-5f, "square to the keys again");
            Assert.Less(Vector3.Distance(middle, (low + high) * 0.5f), 1e-6f, "the average depth stays");
            Assert.AreEqual(length, Vector3.Distance(low, high), 1e-5f, "same length");
        }

        [Test]
        public void ABigTurnIsBelieved()
        {
            Vector3 low = new Vector3(-0.6f, 1.28f, 0.40f), high = new Vector3(0.6f, 1.28f, 0.60f); // 9.5 degrees
            Assert.IsFalse(HandSpan.KeepTurn(ref low, ref high, Vector3.right, Vector3.up));
            Assert.AreEqual(0.60f, high.z, 1e-6f);
        }

        [Test]
        public void ATiltedHandCalibrationComesOutLevel()
        {
            // Round 8, 2026-11-21: A0 pressed 14 mm higher than C8.
            Vector3 low = new Vector3(-0.612f, 2.254f, 0.471f), high = new Vector3(0.569f, 2.240f, 0.532f);
            Vector3 middle = (low + high) * 0.5f;
            Assert.IsTrue(HandSpan.KeepLevel(ref low, ref high, Vector3.up));
            Assert.AreEqual(low.y, high.y, 1e-6f, "both ends at the same height");
            Assert.AreEqual(2.247f, low.y, 1e-5f, "the average height");
            Assert.Less(Vector3.Distance(middle, (low + high) * 0.5f), 1e-6f, "the middle stays");
            Assert.AreEqual(-0.612f, low.x, 1e-6f, "only the height moves");
            Assert.AreEqual(0.532f, high.z, 1e-6f, "only the height moves");
        }

        [Test]
        public void ALevelHandCalibrationIsLeftAlone()
        {
            Vector3 low = new Vector3(-0.6f, 0.7002f, 0.47f), high = new Vector3(0.6f, 0.7f, 0.47f);
            Assert.IsFalse(HandSpan.KeepLevel(ref low, ref high, Vector3.up));
            Assert.AreEqual(0.7002f, low.y, 1e-7f);
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
