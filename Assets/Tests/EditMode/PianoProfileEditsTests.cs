using System;
using NUnit.Framework;
using PianoGlass.Piano;
using UnityEngine;

namespace PianoGlass.Tests.EditMode
{
    public class PianoProfileEditsTests
    {
        private const string A = "11111111-1111-1111-1111-111111111111";
        private const string B = "22222222-2222-2222-2222-222222222222";

        private static readonly KeyboardGeometry Local =
            new KeyboardGeometry(new Vector3(-0.59925f, 0f, 0f), new Vector3(0.59925f, 0f, 0f), Vector3.back);

        private static PianoProfileStore WithProfile(string anchor)
        {
            var store = new PianoProfileStore();
            PianoProfileEdits.ApplyCalibration(store, anchor, Local, 21, 108, 0.145f, Vector3.zero, "t0", "piano-1");
            return store;
        }

        [Test]
        public void FirstCalibrationCreatesTheActiveProfile()
        {
            var store = new PianoProfileStore();
            string replaced = PianoProfileEdits.ApplyCalibration(store, A, Local, 21, 108, 0.145f,
                                                                 new Vector3(1f, 2f, 3f), "t0", "piano-1");

            Assert.IsNull(replaced);
            Assert.AreEqual("piano-1", store.activeProfileId);
            Assert.AreEqual(1, store.profiles.Count);
            Assert.AreEqual(A, store.ActiveProfile.PrimaryAnchorUuid);
            Assert.AreEqual(new Vector3(1f, 2f, 3f), store.tipNudgeKeyboardMm);
            Assert.IsNull(PianoProfileSerializer.ValidateProfile(store.ActiveProfile));
        }

        [Test]
        public void RecalibrationReplacesTheAnchorInPlaceAndReportsTheOldOne()
        {
            PianoProfileStore store = WithProfile(A);
            store.ActiveProfile.lastVerifiedUtc = "earlier";

            string replaced = PianoProfileEdits.ApplyCalibration(store, B, Local, 21, 108, 0.145f, Vector3.zero, "t1", "unused");

            Assert.AreEqual(A, replaced);
            Assert.AreEqual(1, store.profiles.Count);
            Assert.AreEqual("piano-1", store.activeProfileId);
            Assert.AreEqual(1, store.ActiveProfile.anchorUuids.Count);
            Assert.AreEqual(B, store.ActiveProfile.PrimaryAnchorUuid);
            Assert.AreEqual("", store.ActiveProfile.lastVerifiedUtc, "a new capture is unverified");
        }

        [Test]
        public void GeometryIsOnlyAppliedToTheAnchorItIsRelativeTo()
        {
            PianoProfileStore store = WithProfile(A);
            var turned = AnchoredKeyboard.RotateYaw(Local, 2f);

            Assert.IsFalse(PianoProfileEdits.ApplyGeometry(store, B, turned));
            Assert.AreEqual(Local.HighFront, store.ActiveProfile.highFrontLocal);

            Assert.IsTrue(PianoProfileEdits.ApplyGeometry(store, A, turned));
            Assert.AreEqual(turned.HighFront, store.ActiveProfile.highFrontLocal);
        }

        [Test]
        public void LaunchCheckNeedsAValidActiveProfile()
        {
            Assert.IsFalse(PianoProfileEdits.CheckForLaunch(
                new ProfileLoadResult(ProfileLoadStatus.Missing, null, "no profile file")).Valid);

            Assert.IsFalse(PianoProfileEdits.CheckForLaunch(
                new ProfileLoadResult(ProfileLoadStatus.Loaded, new PianoProfileStore(), null)).Valid,
                "a store with only tip fields is not a piano");

            PianoProfileStore good = WithProfile(A);
            ProfileCheck ok = PianoProfileEdits.CheckForLaunch(new ProfileLoadResult(ProfileLoadStatus.Loaded, good, null));
            Assert.IsTrue(ok.Valid);
            Assert.AreSame(good.ActiveProfile, ok.Profile);

            PianoProfileStore bad = WithProfile(A);
            bad.ActiveProfile.anchorUuids[0] = Guid.Empty.ToString("D");
            ProfileCheck rejected = PianoProfileEdits.CheckForLaunch(new ProfileLoadResult(ProfileLoadStatus.Loaded, bad, null));
            Assert.IsFalse(rejected.Valid);
            Assert.IsNotNull(rejected.Reason);
        }
    }

    public class AnchorFollowTests
    {
        private static readonly Pose Origin = new Pose(Vector3.zero, Quaternion.identity);

        [Test]
        public void SubMillimetreJitterDoesNotRebuild()
        {
            Assert.IsFalse(AnchoredKeyboard.PoseMoved(Origin, new Pose(new Vector3(0.0005f, 0f, 0.0005f), Quaternion.identity)));
            Assert.IsFalse(AnchoredKeyboard.PoseMoved(Origin, new Pose(Vector3.zero, Quaternion.Euler(0f, 0.05f, 0f))));
        }

        [Test]
        public void AMillimetreOrATenthOfADegreeRebuilds()
        {
            Assert.IsTrue(AnchoredKeyboard.PoseMoved(Origin, new Pose(new Vector3(0.0015f, 0f, 0f), Quaternion.identity)));
            Assert.IsTrue(AnchoredKeyboard.PoseMoved(Origin, new Pose(Vector3.zero, Quaternion.Euler(0f, 0.2f, 0f))));
        }

        [Test]
        public void SlowDriftAccumulatesAgainstTheBuiltPose()
        {
            Pose built = Origin;
            Pose now = Origin;
            bool rebuilt = false;
            for (int frame = 0; frame < 10 && !rebuilt; frame++)
            {
                now = new Pose(now.position + new Vector3(0.0003f, 0f, 0f), Quaternion.identity);
                rebuilt = AnchoredKeyboard.PoseMoved(built, now);
            }
            Assert.IsTrue(rebuilt, "0.3 mm a frame must eventually cross 1 mm");
        }

        [Test]
        public void StatusFollowsTheFlow()
        {
            Assert.AreEqual(AnchorPanelStatus.Starting, AnchorStatusText.Classify(FlowState.Launch, 0, false, false, false));
            Assert.AreEqual(AnchorPanelStatus.Localizing, AnchorStatusText.Classify(FlowState.Localizing, 0, false, false, false));
            Assert.AreEqual(AnchorPanelStatus.Retrying, AnchorStatusText.Classify(FlowState.Localizing, 1, false, false, false));
            Assert.AreEqual(AnchorPanelStatus.LocalizeFailed, AnchorStatusText.Classify(FlowState.Localizing, 3, true, false, false));
            Assert.AreEqual(AnchorPanelStatus.Calibrating, AnchorStatusText.Classify(FlowState.FullCalibration, 0, false, false, false));
            Assert.AreEqual(AnchorPanelStatus.NotFound, AnchorStatusText.Classify(FlowState.FullCalibration, 0, false, false, true));
            Assert.AreEqual(AnchorPanelStatus.Saving, AnchorStatusText.Classify(FlowState.FullCalibration, 0, false, true, true));
            Assert.AreEqual(AnchorPanelStatus.Recalibrating, AnchorStatusText.Classify(FlowState.FullCalibration, 0, false, false, false, hasProfile: true));
            Assert.AreEqual(AnchorPanelStatus.NotFound, AnchorStatusText.Classify(FlowState.FullCalibration, 0, false, false, true, hasProfile: true));
            Assert.AreEqual(AnchorPanelStatus.Saving, AnchorStatusText.Classify(FlowState.FullCalibration, 0, false, true, false, hasProfile: true));
            Assert.AreEqual(AnchorPanelStatus.Verifying, AnchorStatusText.Classify(FlowState.Verifying, 0, false, false, false));
            Assert.AreEqual(AnchorPanelStatus.Correcting, AnchorStatusText.Classify(FlowState.Correcting, 0, false, false, false));
            Assert.AreEqual(AnchorPanelStatus.Ready, AnchorStatusText.Classify(FlowState.Ready, 0, false, false, false));
            Assert.AreEqual(AnchorPanelStatus.Untracked, AnchorStatusText.Classify(FlowState.Suspended, 0, false, false, false));
        }

        [Test]
        public void EveryStatusHasWords()
        {
            foreach (AnchorPanelStatus s in Enum.GetValues(typeof(AnchorPanelStatus)))
                Assert.IsFalse(string.IsNullOrEmpty(AnchorStatusText.Line(s, 1, 3)), s.ToString());
            StringAssert.Contains("2 of 3", AnchorStatusText.Line(AnchorPanelStatus.Retrying, 1, 3));
        }

        /// <summary>Phase 4 device observation 1: a recalibration said "no
        /// saved piano yet" while a saved piano existed.</summary>
        [Test]
        public void RecalibratingNeverSaysNoSavedPiano()
        {
            Assert.AreEqual("recalibrating -- touch the lowest key", AnchorStatusText.Line(AnchorPanelStatus.Recalibrating, 0, 3));
            Assert.AreEqual("recalibrating -- now the highest key",
                            AnchorStatusText.Line(AnchorPanelStatus.Recalibrating, 0, 3, awaitingHighest: true));
            StringAssert.DoesNotContain("no saved piano", AnchorStatusText.Line(AnchorPanelStatus.Recalibrating, 0, 3));
            StringAssert.Contains("no saved piano yet", AnchorStatusText.Line(AnchorPanelStatus.Calibrating, 0, 3));
        }

        [Test]
        public void VerifyBannerIsExact()
        {
            Assert.AreEqual("A: looks right · right grip: Middle C fix · B: recalibrate", AnchorStatusText.VerifyBanner);
        }

        [Test]
        public void ACorrectionRecordsItselfAndANewCalibrationClearsIt()
        {
            var local = new KeyboardGeometry(new Vector3(-0.6f, 0f, 0f), new Vector3(0.6f, 0f, 0f), Vector3.back);
            var store = new PianoProfileStore();
            PianoProfileEdits.ApplyCalibration(store, "anchor-a", local, 21, 108, 0.145f, Vector3.zero, "t0", "p1");

            Vector3 shift = new Vector3(0.004f, 0.002f, -0.003f);
            KeyboardGeometry moved = AnchoredKeyboard.Translate(local, shift);
            Assert.IsFalse(PianoProfileEdits.ApplyCorrection(store, "anchor-b", moved, "middle-c", shift * 1000f),
                           "never apply geometry local to another anchor");
            Assert.AreEqual("none", store.ActiveProfile.lastCorrection);

            Assert.IsTrue(PianoProfileEdits.ApplyCorrection(store, "anchor-a", moved, "middle-c", shift * 1000f));
            Assert.AreEqual("middle-c", store.ActiveProfile.lastCorrection);
            Assert.AreEqual(local.LowFront + shift, store.ActiveProfile.lowFrontLocal);
            Assert.AreEqual(shift * 1000f, store.ActiveProfile.lastCorrectionMm);

            PianoProfileEdits.ApplyCalibration(store, "anchor-c", local, 21, 108, 0.145f, Vector3.zero, "t1", "p2");
            Assert.AreEqual("none", store.ActiveProfile.lastCorrection);
            Assert.AreEqual(Vector3.zero, store.ActiveProfile.lastCorrectionMm);
        }
    }
}
