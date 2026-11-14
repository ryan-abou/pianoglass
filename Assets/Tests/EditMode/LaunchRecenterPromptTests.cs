using NUnit.Framework;
using PianoGlass.Piano;

namespace PianoGlass.Tests.EditMode
{
    public class LaunchRecenterPromptTests
    {
        [Test]
        public void AQuest3LaunchAsksAndARealRecenterClearsIt()
        {
            // Round 3, 2026-11-14: head at (0.76, -0.56) facing 269 deg at the load.
            var p = new LaunchRecenterPrompt();
            p.OnLaunchLoad(0.76f, -0.56f, 269f);
            Assert.IsTrue(p.Waiting);

            Assert.IsFalse(p.OnHeadStep(0.004f, 1.5f), "ordinary head motion");
            Assert.IsTrue(p.Waiting);

            // The Meta-button recenter: 0.92 m and 86 deg in one frame.
            Assert.IsTrue(p.OnHeadStep(0.92f, 86.5f));
            Assert.IsFalse(p.Waiting);
        }

        [Test]
        public void TheAppsOwnRecenterMovesNothingSoKeepsAsking()
        {
            var p = new LaunchRecenterPrompt();
            p.OnLaunchLoad(0.76f, -0.56f, 269f);
            Assert.IsFalse(p.OnHeadStep(0.001f, 0f), "RecenterPose fired events but the head did not move");
            Assert.IsTrue(p.Waiting);
        }

        [Test]
        public void AHeadAlreadyAtTheOriginNeedsNoRecenter()
        {
            var p = new LaunchRecenterPrompt();
            p.OnLaunchLoad(0.03f, 0.04f, 358f); // -2 deg
            Assert.IsFalse(p.Waiting);
        }

        [Test]
        public void TurnedAwayAtTheOriginStillAsks()
        {
            var p = new LaunchRecenterPrompt();
            p.OnLaunchLoad(0f, 0f, 90f);
            Assert.IsTrue(p.Waiting);
        }

        [Test]
        public void AFastTurnJustUnderTheLimitIsNotARecenter()
        {
            var p = new LaunchRecenterPrompt();
            p.OnLaunchLoad(0.8f, 0f, 0f);
            Assert.IsFalse(p.OnHeadStep(0.02f, -14f));
            Assert.IsFalse(p.OnHeadStep(0.02f, 350f), "350 deg is -10 deg once wrapped");
            Assert.IsTrue(p.Waiting);
        }

        [Test]
        public void OnlyTheFirstLoadCounts()
        {
            var p = new LaunchRecenterPrompt();
            p.OnLaunchLoad(0f, 0f, 0f);
            p.OnLaunchLoad(0.9f, 0f, 90f);
            Assert.IsFalse(p.Waiting);
        }
    }
}
