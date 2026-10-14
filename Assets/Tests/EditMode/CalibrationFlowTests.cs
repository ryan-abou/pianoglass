using System;
using NUnit.Framework;
using PianoGlass.Piano;

namespace PianoGlass.Tests.EditMode
{
    public class CalibrationFlowTests
    {
        private static CalibrationFlow AtVerifying()
        {
            var f = new CalibrationFlow();
            f.Handle(FlowEvent.ProfileValid);
            f.Handle(FlowEvent.LocalizeSucceeded);
            Assert.AreEqual(FlowState.Verifying, f.State);
            return f;
        }

        [Test]
        public void NoProfileGoesToFullCalibration()
        {
            var f = new CalibrationFlow();
            FlowTransition t = f.Handle(FlowEvent.ProfileMissingOrInvalid);
            Assert.AreEqual(FlowState.FullCalibration, t.To);
            Assert.IsTrue(t.Has(FlowCommand.BeginCapture));
            Assert.IsFalse(f.IsOverlayVisible);
        }

        [Test]
        public void ProfileGoesToLocalizingWithNothingDrawn()
        {
            var f = new CalibrationFlow();
            FlowTransition t = f.Handle(FlowEvent.ProfileValid);
            Assert.AreEqual(FlowState.Localizing, t.To);
            Assert.IsTrue(t.Has(FlowCommand.LoadAndLocalizeAnchor));
            Assert.IsFalse(t.Has(FlowCommand.CreateAndSaveAnchor), "never create an anchor on launch");
            Assert.IsFalse(f.IsOverlayVisible);
        }

        [Test]
        public void LocalizedGoesToVerifyingThenAConfirmsToReady()
        {
            CalibrationFlow f = AtVerifying();
            Assert.IsTrue(f.IsOverlayVisible);

            FlowTransition t = f.Handle(FlowEvent.ConfirmPressed);
            Assert.AreEqual(FlowState.Ready, t.To);
            Assert.IsTrue(t.Has(FlowCommand.MarkVerified));
            Assert.IsTrue(f.IsOverlayVisible);
        }

        [Test]
        public void TimeoutRetriesAndNeverErases()
        {
            var f = new CalibrationFlow();
            f.Handle(FlowEvent.ProfileValid);

            FlowTransition t = f.Handle(FlowEvent.LocalizeTimedOut);
            Assert.AreEqual(FlowState.Localizing, t.To);
            Assert.IsTrue(t.Has(FlowCommand.LoadAndLocalizeAnchor));
            Assert.IsFalse(t.Has(FlowCommand.EraseOldAnchor));
            Assert.IsFalse(t.Has(FlowCommand.WriteProfile));
            Assert.IsFalse(f.IsOverlayVisible);
        }

        [Test]
        public void RepeatedTimeoutsOfferRecalibrateWithTheProfileIntact()
        {
            var f = new CalibrationFlow(maxLocalizeAttempts: 3);
            f.Handle(FlowEvent.ProfileValid);
            f.Handle(FlowEvent.LocalizeTimedOut);
            f.Handle(FlowEvent.LocalizeFailed);
            FlowTransition t = f.Handle(FlowEvent.LocalizeTimedOut);

            Assert.AreEqual(FlowState.Localizing, t.To);
            Assert.IsTrue(t.Has(FlowCommand.OfferRecalibrate));
            Assert.IsFalse(t.Has(FlowCommand.LoadAndLocalizeAnchor), "stops retrying by itself");
            Assert.IsFalse(t.Has(FlowCommand.EraseOldAnchor));
            Assert.IsFalse(t.Has(FlowCommand.WriteProfile));
            Assert.IsTrue(f.HasProfile);
            Assert.IsTrue(f.RecalibrateOffered);

            FlowTransition retry = f.Handle(FlowEvent.RetryRequested);
            Assert.IsTrue(retry.Has(FlowCommand.LoadAndLocalizeAnchor));
        }

        [Test]
        public void EmptyLoadGoesToFullCalibrationWithoutErasing()
        {
            var f = new CalibrationFlow();
            f.Handle(FlowEvent.ProfileValid);
            FlowTransition t = f.Handle(FlowEvent.AnchorNotFound);

            Assert.AreEqual(FlowState.FullCalibration, t.To);
            Assert.IsFalse(t.Has(FlowCommand.EraseOldAnchor));
            Assert.IsFalse(t.Has(FlowCommand.WriteProfile));
        }

        [Test]
        public void RecalibrateSavesTheNewAnchorThenWritesThenErasesTheOld()
        {
            CalibrationFlow f = AtVerifying();
            f.Handle(FlowEvent.ConfirmPressed);

            FlowTransition r = f.Handle(FlowEvent.RecalibrateRequested);
            Assert.AreEqual(FlowState.FullCalibration, r.To);
            Assert.IsFalse(r.Has(FlowCommand.EraseOldAnchor), "nothing is erased on the way in");
            Assert.IsFalse(f.IsOverlayVisible);

            FlowTransition c = f.Handle(FlowEvent.CaptureCompleted);
            Assert.IsTrue(c.Has(FlowCommand.CreateAndSaveAnchor));
            Assert.IsFalse(c.Has(FlowCommand.EraseOldAnchor));

            FlowTransition s = f.Handle(FlowEvent.NewAnchorSaved);
            Assert.AreEqual(FlowState.Verifying, s.To);
            int write = IndexOf(s, FlowCommand.WriteProfile);
            int erase = IndexOf(s, FlowCommand.EraseOldAnchor);
            Assert.GreaterOrEqual(write, 0);
            Assert.Greater(erase, write, "old anchor erased only after the new profile is written");
        }

        [Test]
        public void AFailedSaveDuringRecalibrationErasesNothing()
        {
            CalibrationFlow f = AtVerifying();
            f.Handle(FlowEvent.RecalibrateRequested);
            f.Handle(FlowEvent.CaptureCompleted);

            FlowTransition t = f.Handle(FlowEvent.NewAnchorSaveFailed);
            Assert.AreEqual(FlowState.FullCalibration, t.To);
            Assert.IsFalse(t.Has(FlowCommand.EraseOldAnchor));
            Assert.IsFalse(t.Has(FlowCommand.WriteProfile));
            Assert.IsTrue(t.Has(FlowCommand.BeginCapture));
        }

        [Test]
        public void FirstTimeSetupHasNoOldAnchorToErase()
        {
            var f = new CalibrationFlow();
            f.Handle(FlowEvent.ProfileMissingOrInvalid);
            f.Handle(FlowEvent.CaptureCompleted);
            FlowTransition t = f.Handle(FlowEvent.NewAnchorSaved);

            Assert.IsTrue(t.Has(FlowCommand.WriteProfile));
            Assert.IsFalse(t.Has(FlowCommand.EraseOldAnchor));
        }

        [Test]
        public void TrackingLostInReadySuspendsAndHidesThenResumes()
        {
            CalibrationFlow f = AtVerifying();
            f.Handle(FlowEvent.ConfirmPressed);

            f.Handle(FlowEvent.TrackingLost);
            Assert.AreEqual(FlowState.Suspended, f.State);
            Assert.IsFalse(f.IsOverlayVisible);

            f.Handle(FlowEvent.TrackingRegained);
            Assert.AreEqual(FlowState.Ready, f.State);
            Assert.IsTrue(f.IsOverlayVisible);
        }

        [Test]
        public void CorrectionAppliedWritesAndReturnsToVerifying()
        {
            CalibrationFlow f = AtVerifying();
            Assert.AreEqual(FlowState.Correcting, f.Handle(FlowEvent.CorrectionStarted).To);
            Assert.IsTrue(f.IsOverlayVisible);

            FlowTransition t = f.Handle(FlowEvent.CorrectionApplied);
            Assert.AreEqual(FlowState.Verifying, t.To);
            Assert.IsTrue(t.Has(FlowCommand.WriteProfile));
        }

        [Test]
        public void CorrectionThatSaysMovedOffersRecalibrateAndWritesNothing()
        {
            CalibrationFlow f = AtVerifying();
            f.Handle(FlowEvent.CorrectionStarted);
            FlowTransition t = f.Handle(FlowEvent.CorrectionRejectedMoved);

            Assert.AreEqual(FlowState.Verifying, t.To);
            Assert.IsTrue(t.Has(FlowCommand.OfferRecalibrate));
            Assert.IsFalse(t.Has(FlowCommand.WriteProfile));
        }

        [Test]
        public void OverlayIsVisibleOnlyInVerifyingCorrectingAndReady()
        {
            foreach (FlowState s in Enum.GetValues(typeof(FlowState)))
            {
                bool expected = s == FlowState.Verifying || s == FlowState.Correcting || s == FlowState.Ready;
                Assert.AreEqual(expected, CalibrationFlow.OverlayVisible(s), s.ToString());
            }
        }

        /// <summary>Exhaustive: no failure-type event, from any reachable
        /// state, ever produces EraseOldAnchor.</summary>
        [Test]
        public void NoFailureEventEverErases()
        {
            FlowEvent[] failures =
            {
                FlowEvent.LocalizeTimedOut, FlowEvent.LocalizeFailed, FlowEvent.AnchorNotFound,
                FlowEvent.NewAnchorSaveFailed, FlowEvent.CorrectionRejectedMoved, FlowEvent.TrackingLost,
                FlowEvent.ProfileMissingOrInvalid,
            };

            foreach (Func<CalibrationFlow> start in Starts())
            foreach (FlowEvent e in failures)
            {
                CalibrationFlow f = start();
                FlowTransition t = f.Handle(e);
                Assert.IsFalse(t.Has(FlowCommand.EraseOldAnchor), $"{t} on {e}");
            }
        }

        [Test]
        public void UnhandledEventsChangeNothing()
        {
            var f = new CalibrationFlow();
            FlowTransition t = f.Handle(FlowEvent.ConfirmPressed);
            Assert.IsFalse(t.Handled);
            Assert.AreEqual(FlowState.Launch, f.State);
            Assert.AreEqual(0, t.Commands.Count);
        }

        // ------------------------------------------------------------------

        private static Func<CalibrationFlow>[] Starts() => new Func<CalibrationFlow>[]
        {
            () => new CalibrationFlow(),
            () => { var f = new CalibrationFlow(); f.Handle(FlowEvent.ProfileValid); return f; },
            () => { var f = new CalibrationFlow(); f.Handle(FlowEvent.ProfileMissingOrInvalid); return f; },
            () => { var f = new CalibrationFlow(); f.Handle(FlowEvent.ProfileValid); f.Handle(FlowEvent.RecalibrateRequested); f.Handle(FlowEvent.CaptureCompleted); return f; },
            AtVerifying,
            () => { var f = AtVerifying(); f.Handle(FlowEvent.CorrectionStarted); return f; },
            () => { var f = AtVerifying(); f.Handle(FlowEvent.ConfirmPressed); return f; },
            () => { var f = AtVerifying(); f.Handle(FlowEvent.TrackingLost); return f; },
        };

        private static int IndexOf(FlowTransition t, FlowCommand c)
        {
            for (int i = 0; i < t.Commands.Count; i++) if (t.Commands[i] == c) return i;
            return -1;
        }
    }
}
