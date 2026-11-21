using NUnit.Framework;
using PianoGlass.UI;
using Step = PianoGlass.UI.PinchGrab.Step;

namespace PianoGlass.Tests.EditMode
{
    public class PinchGrabTests
    {
        private const float Dt = 1f / 72f;

        /// <summary>Feeds <paramref name="seconds"/> of frames; the last frame's step.</summary>
        private static Step Run(PinchGrab g, float seconds, bool tracked, bool pinching, bool onTarget)
        {
            Step last = Step.None;
            for (float t = 0f; t < seconds - 1e-4f; t += Dt) last = g.Update(tracked, pinching, onTarget, Dt);
            return last;
        }

        /// <summary>Feeds frames until something other than <paramref name="skip"/>
        /// comes back, or the time runs out.</summary>
        private static Step First(PinchGrab g, float seconds, bool tracked, bool pinching, bool onTarget,
                                  Step skip = Step.None)
        {
            for (float t = 0f; t < seconds - 1e-4f; t += Dt)
            {
                Step s = g.Update(tracked, pinching, onTarget, Dt);
                if (s != skip) return s;
            }
            return skip;
        }

        [Test]
        public void ClosingTheFingersOnAPanelGrabsIt()
        {
            var g = new PinchGrab();
            Run(g, 0.2f, true, false, true);
            Assert.AreEqual(Step.Start, First(g, 0.2f, true, true, true));
            Assert.AreEqual(Step.Hold, g.Update(true, true, false, Dt), "holds even once off the panel");
            Assert.AreEqual(Step.End, First(g, 0.3f, true, false, false, Step.Hold));
            Assert.IsFalse(g.Holding);
        }

        [Test]
        public void AFlickerOpenWhileHoldingKeepsHold()
        {
            var g = new PinchGrab();
            Run(g, 0.2f, true, false, true);
            First(g, 0.2f, true, true, true);
            for (int i = 0; i < 20; i++)
            {
                Assert.AreEqual(Step.Hold, g.Update(true, false, false, Dt), "one frame open");
                Assert.AreEqual(Step.Hold, g.Update(true, true, false, Dt));
            }
            Assert.IsTrue(g.Holding);
        }

        [Test]
        public void ItTakesAMomentOfClosedFingers()
        {
            var g = new PinchGrab();
            Run(g, 0.2f, true, false, true);
            Assert.AreEqual(Step.None, g.Update(true, true, true, Dt), "one frame closed is not a pinch yet");
            Assert.AreEqual(Step.Start, First(g, PinchGrab.CloseSeconds + 2 * Dt, true, true, true));
        }

        [Test]
        public void AOneFrameFlickerDoesNotCount()
        {
            var g = new PinchGrab();
            Run(g, 0.2f, true, false, true);
            for (int i = 0; i < 20; i++)
            {
                Assert.AreEqual(Step.None, g.Update(true, true, true, Dt));
                Assert.AreEqual(Step.None, g.Update(true, false, true, Dt));
            }
        }

        [Test]
        public void ReopeningForAFrameDoesNotPressTwice()
        {
            var g = new PinchGrab();
            Run(g, 0.2f, true, false, true);
            Assert.AreEqual(Step.Start, First(g, 0.2f, true, true, true));
            g.Release(); // pressed a button
            Run(g, 0.1f, true, true, true);
            g.Update(true, false, true, Dt); // tracking says open for one frame
            Assert.AreEqual(Step.None, First(g, 0.3f, true, true, true));
        }

        [Test]
        public void AHandFirstSeenPinchedDoesNotGrab()
        {
            var g = new PinchGrab();
            Assert.AreEqual(Step.None, First(g, 0.5f, true, true, true));
            Run(g, 0.2f, true, false, true);
            Assert.AreEqual(Step.Start, First(g, 0.2f, true, true, true));
        }

        [Test]
        public void PinchingOffAPanelAndMovingOntoItDoesNotGrab()
        {
            var g = new PinchGrab();
            Run(g, 0.2f, true, false, false);
            Assert.AreEqual(Step.None, First(g, 0.2f, true, true, false));
            Assert.AreEqual(Step.None, First(g, 0.5f, true, true, true), "already pinched when it got there");
        }

        [Test]
        public void AShortTrackingDropKeepsHold()
        {
            var g = new PinchGrab();
            Run(g, 0.2f, true, false, true);
            First(g, 0.2f, true, true, true);
            for (int i = 0; i < 10; i++) Assert.AreEqual(Step.Hold, g.Update(false, false, false, Dt));
            Assert.AreEqual(Step.Hold, g.Update(true, true, false, Dt));
        }

        [Test]
        public void ALongTrackingDropLetsGo()
        {
            var g = new PinchGrab();
            Run(g, 0.2f, true, false, true);
            First(g, 0.2f, true, true, true);
            Step last = Step.Hold;
            for (int i = 0; i < 72 && last == Step.Hold; i++) last = g.Update(false, false, false, Dt);
            Assert.AreEqual(Step.End, last);
            Assert.IsFalse(g.Holding);
            Assert.AreEqual(Step.None, First(g, 0.5f, true, true, true), "back still pinched: has to open first");
        }

        [Test]
        public void ReleaseAfterAPressNeedsTheFingersToOpen()
        {
            var g = new PinchGrab();
            Run(g, 0.2f, true, false, true);
            Assert.AreEqual(Step.Start, First(g, 0.2f, true, true, true));
            g.Release();
            Assert.AreEqual(Step.None, First(g, 0.5f, true, true, true));
            Run(g, 0.2f, true, false, true);
            Assert.AreEqual(Step.Start, First(g, 0.2f, true, true, true));
        }
    }
}
