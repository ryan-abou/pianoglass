using NUnit.Framework;
using PianoGlass.Piano;
using UnityEngine;

namespace PianoGlass.Tests.EditMode
{
    public class PressingFingerTests
    {
        private static PressingFinger.Tip T(float x, float y, float z, float drop, bool tracked = true) =>
            new PressingFinger.Tip { Tracked = tracked, Position = new Vector3(x, y, z), Drop = drop };

        // 2026-11-14 round: A0 pressed at about (-0.60, 1.26, 0.40); the other hand in the lap.
        private static readonly PressingFinger.Tip Pressing = T(-0.60f, 1.26f, 0.40f, 0.012f);
        private static readonly PressingFinger.Tip Lap = T(-0.45f, 1.07f, 0.03f, 0.001f);

        [Test]
        public void TheFingerThatCameDownIsTheOneEvenWhenTheOtherIsLower()
        {
            Assert.AreEqual(0, PressingFinger.Choose(new[] { Pressing, Lap }, null));
            Assert.AreEqual(1, PressingFinger.Choose(new[] { Lap, Pressing }, null));
        }

        [Test]
        public void OneTrackedHandIsTheOne()
        {
            Assert.AreEqual(1, PressingFinger.Choose(new[] { T(0, 0, 0, 0, false), Lap }, null));
            Assert.AreEqual(-1, PressingFinger.Choose(new[] { T(0, 0, 0, 0, false), T(0, 0, 0, 0, false) }, null));
        }

        [Test]
        public void BothComingDownGoesByWhereTheKeyWasLastTime()
        {
            var a = T(-0.60f, 1.26f, 0.40f, 0.010f);
            var b = T(0.10f, 1.26f, 0.38f, 0.008f);
            Assert.AreEqual(0, PressingFinger.Choose(new[] { a, b }, new Vector3(-0.59f, 1.26f, 0.39f)));
            Assert.AreEqual(1, PressingFinger.Choose(new[] { a, b }, new Vector3(0.11f, 1.26f, 0.39f)));
        }

        [Test]
        public void NeitherComingDownGoesByWhereTheKeyWasLastTime()
        {
            var still = T(-0.60f, 1.26f, 0.40f, 0.002f);
            Assert.AreEqual(0, PressingFinger.Choose(new[] { still, Lap }, new Vector3(-0.60f, 1.26f, 0.38f)));
        }

        [Test]
        public void NoClearDropAndNoLastTimeIsNotGuessed()
        {
            var a = T(-0.60f, 1.26f, 0.40f, 0.010f);
            var b = T(0.10f, 1.26f, 0.38f, 0.009f);
            Assert.AreEqual(-1, PressingFinger.Choose(new[] { a, b }, null));
        }

        [Test]
        public void ALastSpotFarFromBothIsNotGuessedEither()
        {
            var a = T(-0.60f, 1.26f, 0.40f, 0.002f);
            var b = T(0.10f, 1.26f, 0.38f, 0.002f);
            Assert.AreEqual(-1, PressingFinger.Choose(new[] { a, b }, new Vector3(2f, 1.26f, 0.4f)));
        }
    }
}
