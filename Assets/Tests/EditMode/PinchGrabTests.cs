using NUnit.Framework;
using PianoGlass.UI;
using Step = PianoGlass.UI.PinchGrab.Step;

namespace PianoGlass.Tests.EditMode
{
    public class PinchGrabTests
    {
        private const float Dt = 1f / 72f;

        [Test]
        public void ClosingTheFingersOnAPanelGrabsIt()
        {
            var g = new PinchGrab();
            Assert.AreEqual(Step.None, g.Update(true, false, true, Dt));
            Assert.AreEqual(Step.Start, g.Update(true, true, true, Dt));
            Assert.AreEqual(Step.Hold, g.Update(true, true, false, Dt), "holds even once off the panel");
            Assert.AreEqual(Step.End, g.Update(true, false, false, Dt));
            Assert.IsFalse(g.Holding);
        }

        [Test]
        public void AHandFirstSeenPinchedDoesNotGrab()
        {
            var g = new PinchGrab();
            Assert.AreEqual(Step.None, g.Update(true, true, true, Dt));
            Assert.AreEqual(Step.None, g.Update(true, true, true, Dt));
            Assert.AreEqual(Step.None, g.Update(true, false, true, Dt));
            Assert.AreEqual(Step.Start, g.Update(true, true, true, Dt));
        }

        [Test]
        public void PinchingOffAPanelAndMovingOntoItDoesNotGrab()
        {
            var g = new PinchGrab();
            g.Update(true, false, false, Dt);
            Assert.AreEqual(Step.None, g.Update(true, true, false, Dt));
            Assert.AreEqual(Step.None, g.Update(true, true, true, Dt), "already pinched when it got there");
        }

        [Test]
        public void AShortTrackingDropKeepsHold()
        {
            var g = new PinchGrab();
            g.Update(true, false, true, Dt);
            g.Update(true, true, true, Dt);
            for (int i = 0; i < 10; i++) Assert.AreEqual(Step.Hold, g.Update(false, false, false, Dt));
            Assert.AreEqual(Step.Hold, g.Update(true, true, false, Dt));
        }

        [Test]
        public void ALongTrackingDropLetsGo()
        {
            var g = new PinchGrab();
            g.Update(true, false, true, Dt);
            g.Update(true, true, true, Dt);
            Step last = Step.Hold;
            for (int i = 0; i < 40 && last == Step.Hold; i++) last = g.Update(false, false, false, Dt);
            Assert.AreEqual(Step.End, last);
            Assert.AreEqual(Step.None, g.Update(true, true, true, Dt), "back still pinched: has to open first");
        }

        [Test]
        public void ReleaseAfterAPressNeedsTheFingersToOpen()
        {
            var g = new PinchGrab();
            g.Update(true, false, true, Dt);
            Assert.AreEqual(Step.Start, g.Update(true, true, true, Dt));
            g.Release();
            Assert.AreEqual(Step.None, g.Update(true, true, true, Dt));
            Assert.AreEqual(Step.None, g.Update(true, false, true, Dt));
            Assert.AreEqual(Step.Start, g.Update(true, true, true, Dt));
        }
    }
}
