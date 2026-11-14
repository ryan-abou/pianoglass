using NUnit.Framework;
using PianoGlass.UI;

namespace PianoGlass.Tests.EditMode
{
    public class PokeDetectorTests
    {
        private static int Presses(PokeDetector p, bool over, params float[] depths)
        {
            int n = 0;
            foreach (float d in depths) if (p.Update(d, over)) n++;
            return n;
        }

        [Test]
        public void PushingThroughTheFacePressesOnce()
        {
            var p = new PokeDetector();
            Assert.AreEqual(1, Presses(p, true, 60f, 30f, 12f, 3f, -2f, -5f, -5f));
        }

        [Test]
        public void HoverShowsOnlyCloseInFront()
        {
            var p = new PokeDetector();
            p.Update(60f, true);
            Assert.IsFalse(p.Hovered, "too far");
            p.Update(25f, true);
            Assert.IsTrue(p.Hovered);
            p.Update(25f, false);
            Assert.IsFalse(p.Hovered, "not over the button");
        }

        [Test]
        public void ItRearmsOnlyAfterBackingOff()
        {
            var p = new PokeDetector();
            Assert.AreEqual(1, Presses(p, true, 30f, 0f));
            Assert.AreEqual(0, Presses(p, true, 8f, 2f), "a small wobble on the button is not a second press");
            Assert.AreEqual(1, Presses(p, true, 20f, 1f), "backed off past 15 mm, then pressed again");
        }

        [Test]
        public void AFingerFromBehindNeverPresses()
        {
            var p = new PokeDetector();
            Assert.AreEqual(0, Presses(p, true, -30f, -10f, -2f, 1f));
        }

        [Test]
        public void SlidingOntoTheButtonAlreadyPushedInDoesNotPress()
        {
            var p = new PokeDetector();
            Assert.AreEqual(0, Presses(p, false, 30f, 1f));
            Assert.AreEqual(0, Presses(p, true, 0f, -1f), "came over it from the side, behind the face");
        }

        [Test]
        public void TheFirstFrameIsNeverAPress()
        {
            var p = new PokeDetector();
            Assert.IsFalse(p.Update(0f, true));
        }

        [Test]
        public void ResetForgetsTheLastFrame()
        {
            var p = new PokeDetector();
            p.Update(30f, true);
            p.Reset();
            Assert.IsFalse(p.Update(0f, true), "the hand came back already on the button");
        }
    }
}
