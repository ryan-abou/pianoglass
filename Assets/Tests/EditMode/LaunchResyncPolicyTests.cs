using NUnit.Framework;
using PianoGlass.Piano;

namespace PianoGlass.Tests.EditMode
{
    public class LaunchResyncPolicyTests
    {
        [Test]
        public void AsksOnceHalfASecondAfterTheLaunchLoad()
        {
            var p = new LaunchResyncPolicy();
            p.OnFlowEvent(FlowEvent.LocalizeSucceeded, 10.0);
            Assert.IsFalse(p.Due(10.2), "not before the pose settles");
            Assert.IsTrue(p.Due(10.5));
            Assert.IsFalse(p.Due(10.6), "only once");
            Assert.IsTrue(p.Done);
        }

        [Test]
        public void NothingWithoutALaunchLoad()
        {
            var p = new LaunchResyncPolicy();
            Assert.IsFalse(p.Due(100.0));
            p.OnFlowEvent(FlowEvent.NewAnchorSaved, 1.0);
            Assert.IsFalse(p.Due(100.0), "a fresh calibration's anchor is already in the head's frame");
        }

        [Test]
        public void ALaterLoadInTheSameLaunchDoesNotAskAgain()
        {
            var p = new LaunchResyncPolicy();
            p.OnFlowEvent(FlowEvent.LocalizeSucceeded, 1.0);
            Assert.IsTrue(p.Due(2.0));
            p.OnFlowEvent(FlowEvent.LocalizeSucceeded, 50.0);
            Assert.IsFalse(p.Due(60.0));
        }
    }
}
