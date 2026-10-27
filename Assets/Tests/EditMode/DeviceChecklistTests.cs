using NUnit.Framework;
using PianoGlass.Piano;
using UnityEngine;

namespace PianoGlass.Tests.EditMode
{
    /// <summary>
    /// The in-headset Phase 5 checklist ticks each step from what actually
    /// happened, and never from something that merely looks similar.
    /// </summary>
    public class DeviceChecklistTests
    {
        private const string Build = "build-1";

        private static FlowTransition T(FlowState from, FlowState to) =>
            new FlowTransition(from, to, true, new FlowCommand[0]);

        private static void Flow(DeviceChecklist c, FlowEvent e, FlowState from, FlowState to,
                                 Vector3 errorMm = default, double now = 0) =>
            c.OnFlow(e, T(from, to), errorMm, now);

        private static DeviceChecklist Standing()
        {
            var c = new DeviceChecklist(null, Build);
            c.OnHead(1.70f, true);
            return c;
        }

        [Test]
        public void StartsEmptyAndKeepsProgressOnlyForTheSameBuild()
        {
            var c = new DeviceChecklist(null, Build);
            Assert.AreEqual(0, c.Current);
            Assert.AreEqual(1, c.State.run);
            Flow(c, FlowEvent.LocalizeSucceeded, FlowState.Localizing, FlowState.Verifying);

            var same = new DeviceChecklist(c.State, Build);
            Assert.AreEqual(2, same.State.run);
            Assert.AreEqual(CheckResult.Passed, same.Result(DeviceChecklist.LaunchVerifying));

            var other = new DeviceChecklist(c.State, "build-2");
            Assert.AreEqual(CheckResult.Pending, other.Result(DeviceChecklist.LaunchVerifying), "new build, new test");
            Assert.AreEqual(1, other.State.run);
        }

        [Test]
        public void ASongStartedByTheConfirmingAFailsAndStaysFailed()
        {
            var c = Standing();
            Flow(c, FlowEvent.ConfirmPressed, FlowState.Verifying, FlowState.Ready, now: 10.0);
            c.OnTransportStarted(FlowState.Ready, 10.1);
            Assert.AreEqual(CheckResult.Failed, c.Result(DeviceChecklist.ConfirmNotPlay));

            c.OnTransportStarted(FlowState.Ready, 20.0);
            Assert.AreEqual(CheckResult.Failed, c.Result(DeviceChecklist.ConfirmNotPlay));
            StringAssert.Contains("FAILED 2", c.Render());
        }

        [Test]
        public void TheNextAStartingTheSongPasses()
        {
            var c = Standing();
            c.OnTransportStarted(FlowState.Ready, 1.0);
            Assert.AreEqual(CheckResult.Pending, c.Result(DeviceChecklist.ConfirmNotPlay), "no confirm seen yet");

            Flow(c, FlowEvent.ConfirmPressed, FlowState.Verifying, FlowState.Ready, now: 10.0);
            c.OnTransportStarted(FlowState.Ready, 12.0);
            Assert.AreEqual(CheckResult.Passed, c.Result(DeviceChecklist.ConfirmNotPlay));
        }

        [Test]
        public void AppliedFixesAreSortedIntoPlainCappedAndSeated()
        {
            var c = Standing();
            c.OnHead(1.69f, true);
            Flow(c, FlowEvent.CorrectionApplied, FlowState.Correcting, FlowState.Verifying, new Vector3(3f, -2f, 4f));
            Assert.AreEqual(CheckResult.Passed, c.Result(DeviceChecklist.MiddleCFix));
            Assert.AreEqual(new Vector3(3f, -2f, 4f), c.State.fixErrorMm);

            Flow(c, FlowEvent.CorrectionApplied, FlowState.Correcting, FlowState.Verifying, new Vector3(0f, 19.5f, 0f));
            Assert.AreEqual(CheckResult.Passed, c.Result(DeviceChecklist.VerticalCap));
            Assert.AreEqual(19.5f, c.State.cappedYMm);
            Assert.AreEqual(CheckResult.Pending, c.Result(DeviceChecklist.Seated));

            c.OnHead(1.30f, true);
            Flow(c, FlowEvent.CorrectionApplied, FlowState.Correcting, FlowState.Verifying, new Vector3(1f, 6f, 0f));
            Assert.AreEqual(CheckResult.Passed, c.Result(DeviceChecklist.Seated));
            Assert.AreEqual(6f, c.State.seatedYMm);
            Assert.AreEqual(0.40f, c.State.seatedDropM, 1e-4f);
            StringAssert.Contains("seated:     y +6.0 mm", c.Render());
        }

        [Test]
        public void TheFixSurvivingNeedsADifferentRunAndAMiddleCProfile()
        {
            var c = Standing();
            Flow(c, FlowEvent.CorrectionApplied, FlowState.Correcting, FlowState.Verifying, new Vector3(3f, 0f, 0f));
            c.OnLaunchProfile("middle-c");
            Flow(c, FlowEvent.LocalizeSucceeded, FlowState.Localizing, FlowState.Verifying);
            Assert.AreEqual(CheckResult.Pending, c.Result(DeviceChecklist.FixSurvivesRelaunch), "same run");

            var noFix = new DeviceChecklist(c.State, Build);
            noFix.OnLaunchProfile("none");
            Flow(noFix, FlowEvent.LocalizeSucceeded, FlowState.Localizing, FlowState.Verifying);
            Assert.AreEqual(CheckResult.Pending, noFix.Result(DeviceChecklist.FixSurvivesRelaunch), "the fix was not on disk");

            var relaunched = new DeviceChecklist(c.State, Build);
            relaunched.OnLaunchProfile("middle-c");
            Flow(relaunched, FlowEvent.LocalizeSucceeded, FlowState.Localizing, FlowState.Verifying);
            Assert.AreEqual(CheckResult.Passed, relaunched.Result(DeviceChecklist.FixSurvivesRelaunch));
        }

        [Test]
        public void OnlyACancelBackToReadyCounts()
        {
            var c = Standing();
            Flow(c, FlowEvent.CorrectionCancelled, FlowState.Correcting, FlowState.Verifying);
            Assert.AreEqual(CheckResult.Pending, c.Result(DeviceChecklist.CancelFromReady));
            Flow(c, FlowEvent.CorrectionCancelled, FlowState.Correcting, FlowState.Ready);
            Assert.AreEqual(CheckResult.Passed, c.Result(DeviceChecklist.CancelFromReady));
        }

        [Test]
        public void RefusalAndRecalibrationTick()
        {
            var c = Standing();
            Flow(c, FlowEvent.CorrectionRejectedMoved, FlowState.Correcting, FlowState.Verifying);
            Assert.AreEqual(CheckResult.Passed, c.Result(DeviceChecklist.TooFarRefused));

            Flow(c, FlowEvent.NewAnchorSaved, FlowState.FullCalibration, FlowState.Verifying);
            Assert.AreEqual(CheckResult.Pending, c.Result(DeviceChecklist.Recalibrate), "first-time setup is not a recalibration");

            Flow(c, FlowEvent.RecalibrateRequested, FlowState.Ready, FlowState.FullCalibration);
            Flow(c, FlowEvent.NewAnchorSaved, FlowState.FullCalibration, FlowState.Verifying);
            Assert.AreEqual(CheckResult.Passed, c.Result(DeviceChecklist.Recalibrate));
        }

        [Test]
        public void ARecenterThenANoteInReadyTicksTheLastStep()
        {
            var c = Standing();
            c.OnNoteOn(FlowState.Ready);
            c.OnAnchorJump(0.001f, 0.1f, FlowState.Ready);
            c.OnNoteOn(FlowState.Ready);
            Assert.AreEqual(CheckResult.Pending, c.Result(DeviceChecklist.RecenterAndMidi), "drift is not a recenter");

            c.OnAnchorJump(0.4f, 30f, FlowState.FullCalibration);
            c.OnNoteOn(FlowState.Ready);
            Assert.AreEqual(CheckResult.Pending, c.Result(DeviceChecklist.RecenterAndMidi), "keys not placed");

            c.OnAnchorJump(0.4f, 30f, FlowState.Ready);
            c.OnNoteOn(FlowState.Verifying);
            Assert.AreEqual(CheckResult.Pending, c.Result(DeviceChecklist.RecenterAndMidi));
            c.OnNoteOn(FlowState.Ready);
            Assert.AreEqual(CheckResult.Passed, c.Result(DeviceChecklist.RecenterAndMidi));
        }

        [Test]
        public void ThePanelShowsTheFirstOpenStepThenSaysDone()
        {
            var c = Standing();
            Flow(c, FlowEvent.LocalizeSucceeded, FlowState.Localizing, FlowState.Verifying);
            string text = c.Render();
            StringAssert.Contains("1/10 passed", text);
            StringAssert.Contains("[2]", text);
            StringAssert.Contains(DeviceChecklist.Titles[DeviceChecklist.ConfirmNotPlay], text);

            for (int i = 0; i < DeviceChecklist.Count; i++) c.State.results[i] = (int)CheckResult.Passed;
            Assert.AreEqual(-1, c.Current);
            StringAssert.Contains("All steps done.", c.Render());
        }

        [Test]
        public void DirtyUntilSaved()
        {
            var c = Standing();
            Assert.IsTrue(c.Dirty, "the run count changed");
            c.MarkSaved();
            c.OnNoteOn(FlowState.Ready);
            Assert.IsFalse(c.Dirty);
            Flow(c, FlowEvent.CorrectionRejectedMoved, FlowState.Correcting, FlowState.Verifying);
            Assert.IsTrue(c.Dirty);
        }
    }
}
