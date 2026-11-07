using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using PianoGlass.Piano;
using UnityEngine;

namespace PianoGlass.Tests.EditMode
{
    /// <summary>
    /// Phase 5 end to end against the editor fake: A confirms (Ready +
    /// lastVerifiedUtc), right grip starts / cancels a Middle C fix, and the
    /// Middle C touch -- offset recovered as a rigid anchor-local shift,
    /// vertical capped at 10 mm, over 150 mm refused with nothing written.
    ///
    /// The calibration is deliberately wrong by a known offset (the
    /// controller-tip error a two-touch capture inherits), and the touch is
    /// on the real Middle C. A recenter happens first in most tests, so the
    /// anchor's frame is rotated away from world and a world/local mix-up
    /// cannot pass.
    /// </summary>
    public class VerifyAndMiddleCTests
    {
        // The real 88-key keyboard: 0.5 m in front of a player at the origin, facing -z.
        private const int Lowest = 21, Highest = 108;
        private const float Depth = 0.145f;
        private static readonly Vector3 Low = new Vector3(-0.59925f, 0.75f, 0.5f);
        private static readonly Vector3 High = new Vector3(0.59925f, 0.75f, 0.5f);
        private static readonly Vector3 Toward = Vector3.back;

        private static readonly Pose Recenter = new Pose(new Vector3(0.4f, 0f, -1.2f), Quaternion.Euler(0f, 120f, 0f));
        private static readonly DateTime Now = new DateTime(2026, 10, 21, 12, 0, 0, DateTimeKind.Utc);

        private string _dir;
        private string _path;
        private EditorPianoAnchorService _fake;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "PianoGlassTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _path = Path.Combine(_dir, PianoProfileSerializer.DefaultFileName);
            _fake = new EditorPianoAnchorService();
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_dir, recursive: true); } catch (Exception) { }
        }

        // ------------------------------------------------------------------

        private AnchoredCalibrationSession NewSession() =>
            new AnchoredCalibrationSession(_fake, new PianoProfileSerializer(_path), 10, 3, () => Now);

        private static void Run(Task t)
        {
            Assert.IsTrue(t.IsCompleted, "the editor fake should complete synchronously");
            t.GetAwaiter().GetResult();
        }

        /// <summary>Calibrated with both touches off by <paramref name="error"/>
        /// from the real keys, then the room recentered. In Verifying.</summary>
        private AnchoredCalibrationSession CalibratedWithError(Vector3 error, int lowest = Lowest, int highest = Highest,
                                                               Vector3? low = null, Vector3? high = null)
        {
            AnchoredCalibrationSession s = NewSession();
            Run(s.StartAsync());
            Run(s.OnCapturedAsync((low ?? Low) + error, (high ?? High) + error, Toward, lowest, highest, Depth));
            Assert.AreEqual(FlowState.Verifying, s.Flow.State);
            _fake.SimulateRecenter(Recenter);
            return s;
        }

        /// <summary>Where the real Middle C's front edge is in today's world.</summary>
        private static Vector3 RealMiddleC() =>
            World(PianoKeyLayout.WithFacing(Low, High, Lowest, Highest, Toward, Depth).FrontEdgeCenter(60));

        private static Vector3 World(Vector3 physical) => Recenter.position + Recenter.rotation * physical;

        private PianoProfileStore OnDisk() => new PianoProfileSerializer(_path).Load().Store;

        private static void Near(Vector3 expected, Vector3 actual, float tol, string what) =>
            Assert.Less((expected - actual).magnitude, tol, $"{what}: expected {expected}, got {actual}");

        private static AnchoredCalibrationSession StartCorrecting(AnchoredCalibrationSession s)
        {
            Run(s.ToggleCorrectionAsync());
            Assert.AreEqual(FlowState.Correcting, s.Flow.State);
            return s;
        }

        // ------------------------------------------------------------------
        // A: looks right
        // ------------------------------------------------------------------

        [Test]
        public void AConfirmsToReadyAndWritesLastVerified()
        {
            AnchoredCalibrationSession s = CalibratedWithError(Vector3.zero);
            Assert.AreEqual("", OnDisk().ActiveProfile.lastVerifiedUtc, "a fresh calibration is unverified");
            Assert.IsTrue(s.AwaitingConfirmation);

            Assert.IsTrue(s.Confirm());

            Assert.AreEqual(FlowState.Ready, s.Flow.State);
            Assert.AreEqual(AnchorPanelStatus.Ready, s.Status);
            Assert.IsFalse(s.AwaitingConfirmation, "the next A is the transport's");
            Assert.AreEqual("2026-10-21T12:00:00Z", OnDisk().ActiveProfile.lastVerifiedUtc);
            Assert.IsFalse(s.Confirm(), "a second A does nothing here");
        }

        [Test]
        public void AAfterARelaunchLocalizeAlsoConfirms()
        {
            CalibratedWithError(Vector3.zero);
            _fake.EndSession();

            AnchoredCalibrationSession s = NewSession();
            Run(s.StartAsync());
            Assert.AreEqual(AnchorPanelStatus.Verifying, s.Status);
            Assert.IsTrue(s.Confirm());
            Assert.AreEqual("2026-10-21T12:00:00Z", OnDisk().ActiveProfile.lastVerifiedUtc);
        }

        [Test]
        public void AIsNotConsumedWhileNothingIsDrawn()
        {
            AnchoredCalibrationSession s = NewSession();
            Run(s.StartAsync());
            Assert.AreEqual(FlowState.FullCalibration, s.Flow.State);
            Assert.IsFalse(s.AwaitingConfirmation);
            Assert.IsFalse(s.Confirm());
            Assert.IsFalse(File.Exists(_path));
        }

        // ------------------------------------------------------------------
        // Right grip
        // ------------------------------------------------------------------

        [Test]
        public void GripStartsAMiddleCFixAndAgainCancelsIt()
        {
            AnchoredCalibrationSession s = CalibratedWithError(Vector3.zero);
            string before = File.ReadAllText(_path);

            StartCorrecting(s);
            Assert.AreEqual(AnchorPanelStatus.Correcting, s.Status);
            Assert.IsTrue(s.IsOverlayVisible, "the keys stay on show to aim at");
            Assert.IsFalse(s.AwaitingConfirmation, "A does not confirm mid-correction");

            Run(s.ToggleCorrectionAsync());
            Assert.AreEqual(FlowState.Verifying, s.Flow.State);
            Assert.AreEqual(before, File.ReadAllText(_path), "cancel writes nothing");
        }

        [Test]
        public void ACancelledFixFromReadyStaysReady()
        {
            AnchoredCalibrationSession s = CalibratedWithError(Vector3.zero);
            s.Confirm();
            StartCorrecting(s);
            Run(s.ToggleCorrectionAsync());
            Assert.AreEqual(FlowState.Ready, s.Flow.State);
        }

        [Test]
        public void GripDoesNothingWithoutMiddleCOnTheKeyboard()
        {
            // C5 to C8: 22 white keys, no Middle C.
            float spacing = PianoKeyLayout.StandardWhiteKeySpacing;
            float span = (PianoKeyLayout.CountWhiteKeys(72, 108) - 1) * spacing;
            AnchoredCalibrationSession s = CalibratedWithError(Vector3.zero, 72, 108,
                new Vector3(-span / 2f, 0.75f, 0.5f), new Vector3(span / 2f, 0.75f, 0.5f));

            Run(s.ToggleCorrectionAsync());
            Assert.AreEqual(FlowState.Verifying, s.Flow.State);
            StringAssert.Contains("Middle C", s.LastProblem);
        }

        // ------------------------------------------------------------------
        // The Middle C touch
        // ------------------------------------------------------------------

        [Test]
        public void MiddleCFixRecoversTheOffsetAndPersistsIt()
        {
            // The touches landed 6 mm left, 4 mm toward the player of the real keys.
            Vector3 error = new Vector3(-0.006f, 0f, -0.004f);
            AnchoredCalibrationSession s = StartCorrecting(CalibratedWithError(error));
            float spacingBefore = s.BuildLayout().WhiteKeySpacing;
            Vector3 rightBefore = s.BuildLayout().Right;

            Run(s.OnMiddleCTouchedAsync(RealMiddleC()));

            // Measured in the keyboard frame: x along the keys, y up, z toward the player.
            Near(new Vector3(6f, 0f, -4f), s.LastVerifyErrorMm, 0.01f, "verify error, keyboard mm");
            Assert.AreEqual(FlowState.Verifying, s.Flow.State, "corrected -> check again");
            Assert.IsNull(s.LastProblem);
            StringAssert.Contains("Middle C fix", s.CorrectionNote);

            PianoKeyLayout fixedLayout = s.BuildLayout();
            Near(RealMiddleC(), fixedLayout.FrontEdgeCenter(60), 1e-4f, "Middle C on the real key");
            Near(World(Low), fixedLayout.LowFrontEdge, 1e-4f, "A0 moved with it: rigid");
            Assert.AreEqual(spacingBefore, fixedLayout.WhiteKeySpacing, 1e-6f, "width unchanged");
            Near(rightBefore, fixedLayout.Right, 1e-5f, "yaw unchanged");

            PianoProfile p = OnDisk().ActiveProfile;
            Assert.AreEqual("middle-c", p.lastCorrection);
            // The anchor was created facing world +z before the recenter, so
            // its local frame is that world frame: the shift is -error.
            Near(new Vector3(6f, 0f, 4f), p.lastCorrectionMm, 0.01f, "recorded shift, anchor-local mm");
            Near(s.LocalGeometry.LowFront, p.lowFrontLocal, 1e-6f, "the corrected geometry is what was saved");
        }

        [Test]
        public void MiddleCFixSurvivesARelaunchAndARecenter()
        {
            Vector3 error = new Vector3(0.008f, 0f, 0.005f);
            AnchoredCalibrationSession s = StartCorrecting(CalibratedWithError(error));
            Run(s.OnMiddleCTouchedAsync(RealMiddleC()));

            _fake.EndSession();
            var second = new Pose(new Vector3(-2f, 0f, 0.7f), Quaternion.Euler(0f, -75f, 0f));
            _fake.SimulateRecenter(second);

            AnchoredCalibrationSession relaunched = NewSession();
            Run(relaunched.StartAsync());

            Vector3 realC4 = PianoKeyLayout.WithFacing(Low, High, Lowest, Highest, Toward, Depth).FrontEdgeCenter(60);
            Near(second.position + second.rotation * realC4, relaunched.BuildLayout().FrontEdgeCenter(60), 1e-4f,
                 "corrected Middle C after relaunch + recenter");
        }

        [Test]
        public void MiddleCFixLandsOnTheTouchWithANudgeApplied()
        {
            AnchoredCalibrationSession s = CalibratedWithError(new Vector3(0.003f, 0f, 0f));
            s.Nudge(new Vector3(0.012f, 0f, -0.006f));
            StartCorrecting(s);

            Run(s.OnMiddleCTouchedAsync(RealMiddleC()));

            Near(RealMiddleC(), s.BuildLayout().FrontEdgeCenter(60), 1e-4f, "what is drawn is what was touched");
        }

        [Test]
        public void ATouchOnKeysAlreadyRightLeavesThemAlone()
        {
            // The keys are right, and the touch reads as it does: a little low.
            AnchoredCalibrationSession s = StartCorrecting(CalibratedWithError(Vector3.zero));
            Vector3 before = s.BuildLayout().FrontEdgeCenter(60);

            Run(s.OnMiddleCTouchedAsync(RealMiddleC() + new Vector3(0f, -0.0068f, 0f)));

            Assert.AreEqual(FlowState.Verifying, s.Flow.State);
            Near(before, s.BuildLayout().FrontEdgeCenter(60), 1e-5f, "not moved");
            StringAssert.Contains("left alone", s.CorrectionNote);
            Assert.AreEqual("none", OnDisk().ActiveProfile.lastCorrection);
        }

        [Test]
        public void MiddleCFixCapsTheVerticalPartAtTenMillimetres()
        {
            // Calibrated 25 mm too low and 5 mm to the left.
            Vector3 error = new Vector3(-0.005f, -0.025f, 0f);
            AnchoredCalibrationSession s = StartCorrecting(CalibratedWithError(error));

            Run(s.OnMiddleCTouchedAsync(RealMiddleC()));

            Assert.AreEqual(FlowState.Verifying, s.Flow.State);
            Assert.AreEqual(25f, s.LastVerifyErrorMm.y, 0.01f, "measured in full");
            StringAssert.Contains("capped", s.CorrectionNote);

            Vector3 drawn = s.BuildLayout().FrontEdgeCenter(60);
            Vector3 real = RealMiddleC();
            Assert.AreEqual(real.y - 0.015f, drawn.y, 1e-4f, "only 10 of the 25 mm applied");
            Assert.AreEqual(0f, new Vector2(real.x - drawn.x, real.z - drawn.z).magnitude, 1e-4f, "horizontal in full");

            Assert.AreEqual(10f, OnDisk().ActiveProfile.lastCorrectionMm.y, 0.01f, "anchor-local y is world up");
        }

        [Test]
        public void MiddleCFixJustUnderFiftyMillimetresIsApplied()
        {
            AnchoredCalibrationSession s = StartCorrecting(CalibratedWithError(new Vector3(-0.049f, 0f, 0f)));
            Run(s.OnMiddleCTouchedAsync(RealMiddleC()));
            Assert.AreEqual("middle-c", OnDisk().ActiveProfile.lastCorrection);
            Near(RealMiddleC(), s.BuildLayout().FrontEdgeCenter(60), 1e-4f, "applied");
        }

        private void MarkTipMeasured()
        {
            var serializer = new PianoProfileSerializer(_path);
            PianoProfileStore store = serializer.Load().Store;
            store.hasTipOffset = true;
            Assert.IsTrue(serializer.Save(store, out string error), error);
        }

        [Test]
        public void WithAMeasuredTipAFreshCalibrationClearsTheNudge()
        {
            AnchoredCalibrationSession s = CalibratedWithError(Vector3.zero);
            MarkTipMeasured();
            s.Nudge(new Vector3(-0.016f, 0.005f, 0.006f));
            s.Confirm();

            Run(s.RecalibrateAsync());
            Run(s.OnCapturedAsync(Low, High, Toward, Lowest, Highest, Depth));

            Assert.AreEqual(Vector3.zero, s.NudgeMm);
            Assert.AreEqual(Vector3.zero, new PianoProfileSerializer(_path).Load().Store.tipNudgeKeyboardMm);
            Near(PianoKeyLayout.WithFacing(Low, High, Lowest, Highest, Toward, Depth).FrontEdgeCenter(60),
                 s.BuildLayout().FrontEdgeCenter(60), 1e-3f, "the keys are where they were touched");
        }

        private AnchoredCalibrationSession Recalibrated(AnchoredCalibrationSession s)
        {
            Run(s.RecalibrateAsync());
            Run(s.OnCapturedAsync(Low, High, Toward, Lowest, Highest, Depth));
            return s;
        }

        [Test]
        public void TheNudgeBeforeConfirmingACalibrationStartsTheNextOne()
        {
            AnchoredCalibrationSession s = CalibratedWithError(Vector3.zero);
            MarkTipMeasured();
            Recalibrated(s);
            s.Nudge(new Vector3(-0.008f, 0f, -0.005f));
            s.Confirm();

            Recalibrated(s);

            Near(new Vector3(-8f, 0f, -5f), s.NudgeMm, 1e-3f, "starts from the bias");
            Near(new Vector3(-8f, 0f, -5f), new PianoProfileSerializer(_path).Load().Store.calibrationBiasMm, 1e-3f, "saved");
        }

        [Test]
        public void AFreshCalibrationsOwnNudgeReplacesTheBias()
        {
            AnchoredCalibrationSession s = CalibratedWithError(Vector3.zero);
            MarkTipMeasured();
            Recalibrated(s);
            s.Nudge(new Vector3(-0.008f, 0f, 0f));
            s.Confirm();
            Recalibrated(s);
            s.Nudge(new Vector3(0.002f, 0f, 0f));
            s.Confirm();

            Near(new Vector3(-6f, 0f, 0f), new PianoProfileSerializer(_path).Load().Store.calibrationBiasMm, 1e-3f, "updated");
        }

        [Test]
        public void ANudgeAfterConfirmingIsStillABiasUntilTheHeadsetComesOff()
        {
            AnchoredCalibrationSession s = ReadySession();
            MarkTipMeasured();
            Recalibrated(s);
            s.Confirm();
            s.Nudge(new Vector3(-0.011f, -0.010f, 0f));
            s.Persist();

            Recalibrated(s);

            Near(new Vector3(-11f, -10f, 0f), s.NudgeMm, 1e-3f, "kept");
        }

        [Test]
        public void ANudgeAfterTheHeadsetCameOffIsNotABias()
        {
            AnchoredCalibrationSession s = ReadySession();
            MarkTipMeasured();
            Recalibrated(s);
            s.Confirm();
            _time = _time.AddSeconds(AnchoredCalibrationSession.AwaySeconds + 1);
            s.UpdateTracking();
            s.Nudge(new Vector3(-0.014f, 0f, 0.004f)); // following a drifted anchor
            s.Persist();

            Recalibrated(s);

            Assert.AreEqual(Vector3.zero, s.NudgeMm);
        }

        [Test]
        public void AHugeNudgeIsNotKeptAsABias()
        {
            AnchoredCalibrationSession s = CalibratedWithError(Vector3.zero);
            MarkTipMeasured();
            Recalibrated(s);
            s.Nudge(new Vector3(-0.030f, 0f, 0f));
            s.Confirm();

            Recalibrated(s);

            Assert.AreEqual(Vector3.zero, s.NudgeMm);
        }

        /// <summary>A Middle C touch this far along the keys from where they are drawn.</summary>
        private static void FixAlongTheKeys(AnchoredCalibrationSession s, float metres)
        {
            StartCorrecting(s);
            PianoKeyLayout layout = s.BuildLayout();
            Run(s.OnMiddleCTouchedAsync(layout.FrontEdgeCenter(60) + layout.Right * metres));
        }

        [Test]
        public void AMiddleCFixBeforeConfirmingCountsInTheBias()
        {
            // 2026-11-07: the bias held +5 while the keys needed -10 in all.
            AnchoredCalibrationSession s = CalibratedWithError(Vector3.zero);
            MarkTipMeasured();
            Recalibrated(s);
            s.Nudge(new Vector3(0.005f, 0f, 0f));
            FixAlongTheKeys(s, -0.014f);
            s.Confirm();

            Recalibrated(s);

            Near(new Vector3(-9f, 0f, 0f), s.NudgeMm, 1e-2f, "nudge and fix together");
        }

        [Test]
        public void AFixCountsOnlyInTheCalibrationItWasMadeFor()
        {
            AnchoredCalibrationSession s = CalibratedWithError(Vector3.zero);
            MarkTipMeasured();
            Recalibrated(s);
            FixAlongTheKeys(s, -0.014f);
            s.Confirm();
            Recalibrated(s);
            s.Confirm();

            Recalibrated(s);

            Near(new Vector3(-14f, 0f, 0f), s.NudgeMm, 1e-2f, "not counted twice");
        }

        [Test]
        public void AMiddleCTouchThatLeavesTheKeysAloneIsNotInTheBias()
        {
            AnchoredCalibrationSession s = CalibratedWithError(Vector3.zero);
            MarkTipMeasured();
            Recalibrated(s);
            FixAlongTheKeys(s, -0.003f);
            s.Confirm();

            Recalibrated(s);

            Assert.AreEqual(Vector3.zero, s.NudgeMm);
        }

        [Test]
        public void AMiddleCFixAfterTheHeadsetCameOffIsNotInTheBias()
        {
            AnchoredCalibrationSession s = ReadySession();
            MarkTipMeasured();
            Recalibrated(s);
            s.Confirm();
            _time = _time.AddSeconds(AnchoredCalibrationSession.AwaySeconds + 1);
            s.UpdateTracking();
            FixAlongTheKeys(s, -0.020f); // following a drifted anchor
            s.Confirm();

            Recalibrated(s);

            Assert.AreEqual(Vector3.zero, s.NudgeMm);
        }

        [Test]
        public void ACalibrationNeverConfirmedTeachesNothing()
        {
            // 2026-11-06: B before A left a value the sticks had only passed through.
            AnchoredCalibrationSession s = CalibratedWithError(Vector3.zero);
            MarkTipMeasured();
            Recalibrated(s);
            s.Nudge(new Vector3(0.007f, 0f, 0.003f));
            s.Persist();

            Recalibrated(s);

            Assert.AreEqual(Vector3.zero, s.NudgeMm);
        }

        [Test]
        public void TooMuchInAllIsNotABiasEvenIfEachPartIsSmall()
        {
            AnchoredCalibrationSession s = CalibratedWithError(Vector3.zero);
            MarkTipMeasured();
            Recalibrated(s);
            s.Nudge(new Vector3(-0.015f, 0f, 0f));
            FixAlongTheKeys(s, -0.015f);
            s.Confirm();

            Recalibrated(s);

            Assert.AreEqual(Vector3.zero, s.NudgeMm);
        }

        // ------------------------------------------------------------------
        // Back from time away: the keys get looked at again
        // ------------------------------------------------------------------

        private DateTime _time = Now;

        private AnchoredCalibrationSession ReadySession()
        {
            var s = new AnchoredCalibrationSession(_fake, new PianoProfileSerializer(_path), 10, 3, () => _time);
            Run(s.StartAsync());
            Run(s.OnCapturedAsync(Low, High, Toward, Lowest, Highest, Depth));
            s.Confirm();
            Assert.AreEqual(FlowState.Ready, s.Flow.State);
            s.UpdateTracking();
            return s;
        }

        private void Away(AnchoredCalibrationSession s)
        {
            _time = _time.AddSeconds(AnchoredCalibrationSession.AwaySeconds + 1);
            s.UpdateTracking();
        }

        /// <summary>Past the watch, a second at a time: one big gap would be
        /// time away all over again.</summary>
        private void PastTheWatch(AnchoredCalibrationSession s)
        {
            for (int i = 0; i <= AnchoredCalibrationSession.SettleSeconds + 1; i++)
            {
                _time = _time.AddSeconds(1);
                s.UpdateTracking();
            }
        }

        [Test]
        public void BackWithAnAnchorThatStaysPutAsksNothing()
        {
            AnchoredCalibrationSession s = ReadySession();
            Away(s);
            Assert.AreEqual(FlowState.Ready, s.Flow.State);

            _fake.SimulateAnchorDrift(s.Anchor.Id, new Vector3(0.004f, 0f, 0f));
            PastTheWatch(s);
            Assert.AreEqual(FlowState.Ready, s.Flow.State, "4 mm is nothing");
        }

        [Test]
        public void BackWithAnAnchorThatMovesAsksOnce()
        {
            AnchoredCalibrationSession s = ReadySession();
            Away(s);
            _fake.SimulateAnchorDrift(s.Anchor.Id, new Vector3(0.009f, 0f, 0f));
            s.UpdateTracking();
            Assert.AreEqual(FlowState.Verifying, s.Flow.State);

            s.Confirm();
            s.UpdateTracking();
            _fake.SimulateAnchorDrift(s.Anchor.Id, new Vector3(0.009f, 0f, 0f));
            s.UpdateTracking();
            Assert.AreEqual(FlowState.Ready, s.Flow.State, "once");
        }

        [Test]
        public void TrackingSpaceBeingRebasedDoesNotCountAsMoving()
        {
            AnchoredCalibrationSession s = ReadySession();
            Away(s);
            _fake.SimulateRecenter(new Pose(new Vector3(1f, 0f, -2f), Quaternion.Euler(0f, 90f, 0f)));
            s.UpdateTracking();
            Assert.AreEqual(FlowState.Ready, s.Flow.State);
        }

        [Test]
        public void MovementSplitByARebaseStillAddsUp()
        {
            AnchoredCalibrationSession s = ReadySession();
            Away(s);
            _fake.SimulateAnchorDrift(s.Anchor.Id, new Vector3(0.006f, 0f, 0f));
            s.UpdateTracking();
            _fake.SimulateRecenter(new Pose(new Vector3(1f, 0f, -2f), Quaternion.identity));
            s.UpdateTracking();
            Assert.AreEqual(FlowState.Ready, s.Flow.State);
            _fake.SimulateAnchorDrift(s.Anchor.Id, new Vector3(0.006f, 0f, 0f));
            s.UpdateTracking();
            Assert.AreEqual(FlowState.Verifying, s.Flow.State);
        }

        [Test]
        public void MovementAfterTheWatchEndsAsksNothing()
        {
            AnchoredCalibrationSession s = ReadySession();
            Away(s);
            PastTheWatch(s);
            _fake.SimulateAnchorDrift(s.Anchor.Id, new Vector3(0.02f, 0f, 0f));
            s.UpdateTracking();
            Assert.AreEqual(FlowState.Ready, s.Flow.State);
        }

        [Test]
        public void AnOrdinaryStallDoesNotInterrupt()
        {
            AnchoredCalibrationSession s = ReadySession();
            _time = _time.AddSeconds(AnchoredCalibrationSession.AwaySeconds - 1);
            s.UpdateTracking();
            Assert.AreEqual(FlowState.Ready, s.Flow.State);
        }

        [Test]
        public void AnAnchorEightyFiveMillimetresOffIsFixedWithOneTouch()
        {
            // What the headset did on 2026-11-05, after a day away.
            AnchoredCalibrationSession s = StartCorrecting(CalibratedWithError(new Vector3(-0.078f, 0f, -0.024f)));
            Run(s.OnMiddleCTouchedAsync(RealMiddleC()));
            Assert.AreEqual(FlowState.Verifying, s.Flow.State);
            Assert.IsFalse(s.Flow.RecalibrateOffered);
            Near(RealMiddleC(), s.BuildLayout().FrontEdgeCenter(60), 1e-3f, "keys back on the piano");
        }

        [Test]
        public void MiddleCFixOverTheLimitIsRefusedAndWritesNothing()
        {
            AnchoredCalibrationSession s = StartCorrecting(CalibratedWithError(new Vector3(-0.11f, 0f, -0.11f)));
            string before = File.ReadAllText(_path);
            Vector3 drawnBefore = s.BuildLayout().FrontEdgeCenter(60);

            Run(s.OnMiddleCTouchedAsync(RealMiddleC())); // ~155.6 mm away

            Assert.AreEqual(FlowState.Verifying, s.Flow.State);
            Assert.IsTrue(s.Flow.RecalibrateOffered);
            StringAssert.Contains("piano may have moved -- press B to recalibrate", s.LastProblem);
            Assert.IsNull(s.CorrectionNote);
            Assert.AreEqual(before, File.ReadAllText(_path), "profile untouched");
            Near(drawnBefore, s.BuildLayout().FrontEdgeCenter(60), 1e-6f, "placement untouched");
            Assert.Greater(s.LastVerifyErrorMm.magnitude, 150f, "still measured, for the log");

            Run(s.RecalibrateAsync());
            Assert.AreEqual(AnchorPanelStatus.Recalibrating, s.Status);
        }

        // ------------------------------------------------------------------
        // A refused Middle C warning goes stale once the placement moves
        // ------------------------------------------------------------------

        private AnchoredCalibrationSession Refused()
        {
            AnchoredCalibrationSession s = StartCorrecting(CalibratedWithError(new Vector3(-0.11f, 0f, -0.11f)));
            Run(s.OnMiddleCTouchedAsync(RealMiddleC()));
            StringAssert.Contains("mm off", s.LastProblem);
            return s;
        }

        [Test]
        public void NudgeClearsARefusedMiddleCWarning()
        {
            AnchoredCalibrationSession s = Refused();
            s.Nudge(new Vector3(0.001f, 0f, 0f));
            Assert.IsNull(s.LastProblem, "the N mm was measured against the old placement");
        }

        [Test]
        public void YawAdjustClearsARefusedMiddleCWarning()
        {
            AnchoredCalibrationSession s = Refused();
            s.AdjustYaw(0.5f);
            Assert.IsNull(s.LastProblem);
        }

        [Test]
        public void ClearingTheNudgeClearsARefusedMiddleCWarning()
        {
            AnchoredCalibrationSession s = Refused();
            s.ClearNudge();
            Assert.IsNull(s.LastProblem);
        }

        [Test]
        public void NudgeKeepsAProblemThatIsNotAboutPlacement()
        {
            // C5 to C8, as above: "not on this keyboard" does not depend on
            // where the keys are drawn, so a nudge must not hide it.
            float spacing = PianoKeyLayout.StandardWhiteKeySpacing;
            float span = (PianoKeyLayout.CountWhiteKeys(72, 108) - 1) * spacing;
            AnchoredCalibrationSession s = CalibratedWithError(Vector3.zero, 72, 108,
                new Vector3(-span / 2f, 0.75f, 0.5f), new Vector3(span / 2f, 0.75f, 0.5f));
            Run(s.ToggleCorrectionAsync());
            StringAssert.Contains("not on this keyboard", s.LastProblem);

            s.Nudge(new Vector3(0.001f, 0f, 0f));
            StringAssert.Contains("not on this keyboard", s.LastProblem);
        }

        [Test]
        public void ATouchOutsideACorrectionIsIgnored()
        {
            AnchoredCalibrationSession s = CalibratedWithError(new Vector3(0.01f, 0f, 0f));
            string before = File.ReadAllText(_path);
            Vector3 drawnBefore = s.BuildLayout().FrontEdgeCenter(60);

            Run(s.OnMiddleCTouchedAsync(RealMiddleC()));

            Assert.AreEqual(FlowState.Verifying, s.Flow.State);
            Assert.AreEqual(before, File.ReadAllText(_path));
            Near(drawnBefore, s.BuildLayout().FrontEdgeCenter(60), 1e-6f, "unchanged");
        }

        [Test]
        public void RecalibratingMidCorrectionDropsTheTouch()
        {
            AnchoredCalibrationSession s = StartCorrecting(CalibratedWithError(new Vector3(0.01f, 0f, 0f)));
            string before = File.ReadAllText(_path);

            Run(s.RecalibrateAsync());
            Assert.IsFalse(s.IsOverlayVisible);
            Run(s.OnMiddleCTouchedAsync(RealMiddleC()));

            Assert.AreEqual(before, File.ReadAllText(_path), "no correction against a missing anchor");
        }

        [Test]
        public void ANewCalibrationClearsTheRecordedCorrection()
        {
            AnchoredCalibrationSession s = StartCorrecting(CalibratedWithError(new Vector3(0.012f, 0f, 0f)));
            Run(s.OnMiddleCTouchedAsync(RealMiddleC()));
            Assert.AreEqual("middle-c", OnDisk().ActiveProfile.lastCorrection);

            Run(s.RecalibrateAsync());
            Run(s.OnCapturedAsync(World(Low), World(High), Recenter.rotation * Toward, Lowest, Highest, Depth));

            Assert.AreEqual("none", OnDisk().ActiveProfile.lastCorrection);
            Assert.IsNull(s.CorrectionNote);
        }
    }
}
