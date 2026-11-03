using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using PianoGlass.Piano;
using UnityEngine;

namespace PianoGlass.Tests.EditMode
{
    /// <summary>
    /// Phase 4 end to end against the editor fake: launch, capture, relaunch,
    /// recenter, recalibrate, and every failure path -- checking the rules
    /// that matter on the headset: nothing created on launch, nothing erased
    /// on a failure, the old anchor erased only after the new profile is on
    /// disk, and only anchor-local geometry stored.
    /// </summary>
    public class AnchoredCalibrationSessionTests
    {
        // An 88-key keyboard 0.5 m in front of a player at the origin, facing -z.
        private const int Lowest = 21, Highest = 108;
        private const float Depth = 0.145f;
        private static readonly Vector3 Low = new Vector3(-0.59925f, 0.75f, 0.5f);
        private static readonly Vector3 High = new Vector3(0.59925f, 0.75f, 0.5f);
        private static readonly Vector3 Toward = Vector3.back;

        private const float Tol = 1e-3f;

        private string _dir;
        private string _path;
        private EditorPianoAnchorService _fake;
        private SwitchableService _svc;

        [SetUp]
        public void SetUp()
        {
            _dir = Path.Combine(Path.GetTempPath(), "PianoGlassTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
            _path = Path.Combine(_dir, PianoProfileSerializer.DefaultFileName);
            _fake = new EditorPianoAnchorService();
            _svc = new SwitchableService(_fake);
        }

        [TearDown]
        public void TearDown()
        {
            try { Directory.Delete(_dir, recursive: true); } catch (Exception) { }
        }

        // ------------------------------------------------------------------

        private AnchoredCalibrationSession NewSession(PianoProfileSerializer serializer = null, int maxAttempts = 3) =>
            new AnchoredCalibrationSession(_svc, serializer ?? new PianoProfileSerializer(_path), 10, maxAttempts);

        /// <summary>The fake completes synchronously, so every call has
        /// finished by the time it returns.</summary>
        private static void Run(Task t)
        {
            Assert.IsTrue(t.IsCompleted, "the editor fake should complete synchronously");
            t.GetAwaiter().GetResult();
        }

        private static void Capture(AnchoredCalibrationSession s, Vector3 low, Vector3 high) =>
            Run(s.OnCapturedAsync(low, high, Toward, Lowest, Highest, Depth));

        private AnchoredCalibrationSession Calibrated(PianoProfileSerializer serializer = null)
        {
            AnchoredCalibrationSession s = NewSession(serializer);
            Run(s.StartAsync());
            Capture(s, Low, High);
            Assert.AreEqual(FlowState.Verifying, s.Flow.State);
            return s;
        }

        private PianoProfileStore OnDisk() => new PianoProfileSerializer(_path).Load().Store;

        private static Vector3 Apply(Pose p, Vector3 v) => p.position + p.rotation * v;

        private static void Near(Vector3 expected, Vector3 actual, string what)
        {
            Assert.Less((expected - actual).magnitude, Tol, $"{what}: expected {expected}, got {actual}");
        }

        // ------------------------------------------------------------------

        [Test]
        public void FirstLaunchAsksForCalibrationAndCreatesNothing()
        {
            AnchoredCalibrationSession s = NewSession();
            int captures = 0;
            s.CaptureRequested += () => captures++;

            Run(s.StartAsync());

            Assert.AreEqual(FlowState.FullCalibration, s.Flow.State);
            Assert.AreEqual(1, captures);
            Assert.AreEqual(0, _fake.CreateCount, "never create an anchor on launch");
            Assert.AreEqual(0, _fake.LoadCount);
            Assert.AreEqual(AnchorPanelStatus.Calibrating, s.Status);
            Assert.IsFalse(s.IsOverlayVisible);
        }

        [Test]
        public void CaptureCreatesSavesAndStoresOnlyAnchorLocalGeometry()
        {
            AnchoredCalibrationSession s = Calibrated();

            Assert.AreEqual(1, _fake.CreateCount);
            Assert.AreEqual(1, _fake.SaveCount);
            Assert.IsTrue(_fake.Stored.ContainsKey(s.Anchor.Id));
            Assert.IsTrue(s.IsOverlayVisible);
            Assert.AreEqual(AnchorPanelStatus.Verifying, s.Status, "drawn, waiting for A");

            PianoProfile p = OnDisk().ActiveProfile;
            Assert.IsNotNull(p);
            Assert.AreEqual(s.Anchor.Id, p.PrimaryAnchorUuid);
            Assert.AreEqual(Lowest, p.lowestNote);
            Assert.AreEqual(Highest, p.highestNote);

            // The anchor sits at the keyboard's centre, so the stored points
            // are about half a span either side of zero at height zero -- not
            // the world coordinates that were touched.
            Near(new Vector3(-0.59925f, 0f, 0f), p.lowFrontLocal, "low front, anchor-local");
            Near(new Vector3(0.59925f, 0f, 0f), p.highFrontLocal, "high front, anchor-local");
            Assert.IsNull(PianoProfileSerializer.ValidateProfile(p));

            PianoKeyLayout layout = s.BuildLayout();
            Near(Low, layout.LowFrontEdge, "layout low front");
            Near(High, layout.HighFrontEdge, "layout high front");
            Near(Toward, layout.Forward, "layout forward");
        }

        [Test]
        public void RelaunchAfterRecenterLocalizesWithoutCreatingAndLandsOnThePiano()
        {
            Calibrated();
            _fake.EndSession();

            var recenter = new Pose(new Vector3(1f, 0f, 2f), Quaternion.Euler(0f, 90f, 0f));
            _fake.SimulateRecenter(recenter);

            AnchoredCalibrationSession s = NewSession();
            Run(s.StartAsync());

            Assert.AreEqual(FlowState.Verifying, s.Flow.State);
            Assert.AreEqual(1, _fake.CreateCount, "launch must not create");
            Assert.AreEqual(1, _fake.LoadCount);
            Assert.AreEqual(0, _fake.EraseCount);

            PianoKeyLayout layout = s.BuildLayout();
            Near(Apply(recenter, Low), layout.LowFrontEdge, "low front after recenter");
            Near(Apply(recenter, High), layout.HighFrontEdge, "high front after recenter");
        }

        [Test]
        public void ConfiguredKeyDepthOverridesTheSavedOneOnRelaunch()
        {
            // Profiles saved before the keys were measured hold the old
            // 145 mm default; the piano's dimensions come from the scene.
            Calibrated();
            _fake.EndSession();

            AnchoredCalibrationSession s = NewSession();
            s.ConfiguredWhiteKeyDepth = 0.14764f;
            Run(s.StartAsync());

            Assert.AreEqual(0.14764f, s.WhiteKeyDepth, 1e-6f);
            Assert.AreEqual(0.14764f, s.BuildLayout().WhiteKeyDepth, 1e-6f);
        }

        [Test]
        public void WithoutAConfiguredDepthTheSavedOneIsUsed()
        {
            Calibrated();
            _fake.EndSession();

            AnchoredCalibrationSession s = NewSession();
            Run(s.StartAsync());

            Assert.AreEqual(Depth, s.WhiteKeyDepth, 1e-6f);
        }

        [Test]
        public void RecenterWhileRunningMovesTheLayoutWithTheAnchor()
        {
            AnchoredCalibrationSession s = Calibrated();
            var recenter = new Pose(new Vector3(-0.3f, 0f, 0.4f), Quaternion.Euler(0f, -35f, 0f));

            Pose before = s.Anchor.Pose;
            _fake.SimulateRecenter(recenter);

            Assert.IsTrue(AnchoredKeyboard.PoseMoved(before, s.Anchor.Pose), "the manager would rebuild");
            PianoKeyLayout layout = s.BuildLayout();
            Near(Apply(recenter, Low), layout.LowFrontEdge, "low front");
            Near(Apply(recenter, High), layout.HighFrontEdge, "high front");
        }

        [Test]
        public void RecalibrateSavesNewAnchorWritesProfileThenErasesOld()
        {
            AnchoredCalibrationSession s = Calibrated();
            string oldId = s.Anchor.Id;
            int captures = 0;
            s.CaptureRequested += () => captures++;

            Run(s.RecalibrateAsync());

            Assert.AreEqual(FlowState.FullCalibration, s.Flow.State);
            Assert.AreEqual(1, captures);
            Assert.IsNull(s.Anchor);
            Assert.IsFalse(s.IsOverlayVisible, "B hides the keys");
            Assert.AreEqual(AnchorPanelStatus.Recalibrating, s.Status, "a saved piano exists: not 'no saved piano yet'");
            Assert.AreEqual("recalibrating -- touch the lowest key", s.StatusLine);
            Assert.IsTrue(_fake.Stored.ContainsKey(oldId), "old anchor untouched on the way in");
            Assert.AreEqual(oldId, OnDisk().ActiveProfile.PrimaryAnchorUuid);
            Assert.AreEqual(0, _fake.EraseCount);

            Vector3 shift = new Vector3(0.05f, 0f, 0.02f);
            Capture(s, Low + shift, High + shift);

            string newId = s.Anchor.Id;
            Assert.AreNotEqual(oldId, newId);
            Assert.AreEqual(newId, OnDisk().ActiveProfile.PrimaryAnchorUuid);
            Assert.AreEqual(1, OnDisk().profiles.Count, "same piano, profile replaced in place");
            Assert.IsFalse(_fake.Stored.ContainsKey(oldId), "old anchor erased");
            Assert.IsTrue(_fake.Stored.ContainsKey(newId));
            Assert.AreEqual(1, _fake.EraseCount);
            Near(Low + shift, s.BuildLayout().LowFrontEdge, "new placement");
        }

        [Test]
        public void RecalibrateKeepsOldAnchorWhenTheProfileCannotBeWritten()
        {
            var serializer = new PianoProfileSerializer(_path);
            AnchoredCalibrationSession s = Calibrated(serializer);
            string oldId = s.Anchor.Id;
            string before = File.ReadAllText(_path);

            serializer.WriteAllText = (p, c) => throw new IOException("disk full");
            Run(s.RecalibrateAsync());
            Capture(s, Low, High);

            Assert.IsTrue(_fake.Stored.ContainsKey(oldId), "never erase the anchor the profile still names");
            Assert.AreEqual(0, _fake.EraseCount);
            Assert.AreEqual(before, File.ReadAllText(_path));
            StringAssert.Contains("NOT saved", s.LastProblem);
        }

        [Test]
        public void AnchorGoneMeansSetupAndNothingIsErased()
        {
            Calibrated();
            string before = File.ReadAllText(_path);

            // Spatial data cleared: same profile file, empty anchor storage.
            _fake = new EditorPianoAnchorService();
            _svc = new SwitchableService(_fake);

            AnchoredCalibrationSession s = NewSession();
            Run(s.StartAsync());

            Assert.AreEqual(FlowState.FullCalibration, s.Flow.State);
            Assert.AreEqual(AnchorPanelStatus.NotFound, s.Status);
            Assert.AreEqual(0, _fake.CreateCount);
            Assert.AreEqual(0, _fake.EraseCount);
            Assert.AreEqual(before, File.ReadAllText(_path), "profile untouched until a replacement saves");

            Capture(s, Low, High);
            Assert.AreEqual(s.Anchor.Id, OnDisk().ActiveProfile.PrimaryAnchorUuid);
            Assert.AreEqual(0, _fake.EraseCount, "nothing to erase: the old anchor no longer exists");
            Assert.AreEqual(AnchorPanelStatus.Verifying, s.Status);
        }

        [Test]
        public void TimeoutRetriesThenOffersRecalibrateWithoutTouchingAnything()
        {
            Calibrated();
            _fake.EndSession();
            string before = File.ReadAllText(_path);
            int storedBefore = _fake.Stored.Count;

            _fake.ForceLoad = AnchorStatus.Timeout;
            AnchoredCalibrationSession s = NewSession(maxAttempts: 3);
            Run(s.StartAsync());

            Assert.AreEqual(3, _fake.LoadCount);
            Assert.AreEqual(FlowState.Localizing, s.Flow.State);
            Assert.IsTrue(s.Flow.RecalibrateOffered);
            Assert.AreEqual(AnchorPanelStatus.LocalizeFailed, s.Status);
            Assert.IsFalse(s.IsOverlayVisible);
            Assert.AreEqual(0, _fake.EraseCount);
            Assert.AreEqual(1, _fake.CreateCount);
            Assert.AreEqual(storedBefore, _fake.Stored.Count);
            Assert.AreEqual(before, File.ReadAllText(_path));

            _fake.ForceLoad = null;
            Run(s.RetryAsync());
            Assert.AreEqual(FlowState.Verifying, s.Flow.State);
            Assert.IsTrue(s.IsOverlayVisible);
        }

        [Test]
        public void ARetryIsOnlyAcceptedOnceOffered()
        {
            Calibrated();
            _fake.EndSession();

            AnchoredCalibrationSession s = NewSession();
            Run(s.StartAsync());
            Assert.AreEqual(FlowState.Verifying, s.Flow.State);
            int loads = _fake.LoadCount;

            Run(s.RetryAsync());
            Assert.AreEqual(loads, _fake.LoadCount, "no retry while drawing");
        }

        [Test]
        public void LostTrackingHidesAndRegainingResumes()
        {
            AnchoredCalibrationSession s = Calibrated();

            _svc.Last.Lost = true;
            s.UpdateTracking();
            Assert.AreEqual(FlowState.Suspended, s.Flow.State);
            Assert.AreEqual(AnchorPanelStatus.Untracked, s.Status);
            Assert.IsFalse(s.IsOverlayVisible);

            s.UpdateTracking();
            Assert.AreEqual(FlowState.Suspended, s.Flow.State, "stays suspended while lost");

            _svc.Last.Lost = false;
            s.UpdateTracking();
            Assert.AreEqual(FlowState.Verifying, s.Flow.State);
            Assert.IsTrue(s.IsOverlayVisible);
        }

        [Test]
        public void ImplausibleCaptureCreatesNoAnchorAndAsksAgain()
        {
            AnchoredCalibrationSession s = NewSession();
            int captures = 0;
            s.CaptureRequested += () => captures++;
            Run(s.StartAsync());

            // 88 keys in 0.5 m: a 9.8 mm white key. Not those two keys.
            Capture(s, new Vector3(-0.25f, 0.75f, 0.5f), new Vector3(0.25f, 0.75f, 0.5f));

            Assert.AreEqual(0, _fake.CreateCount);
            Assert.AreEqual(FlowState.FullCalibration, s.Flow.State);
            Assert.IsFalse(s.Flow.SavingNewAnchor);
            Assert.AreEqual(2, captures);
            Assert.IsNotNull(s.LastProblem);
            Assert.IsFalse(File.Exists(_path));
        }

        [Test]
        public void FailedSaveAsksForCaptureAgainAndWritesNothing()
        {
            AnchoredCalibrationSession s = NewSession();
            Run(s.StartAsync());
            _fake.ForceSave = AnchorStatus.Failed;

            Capture(s, Low, High);

            Assert.AreEqual(FlowState.FullCalibration, s.Flow.State);
            Assert.IsNull(s.Anchor);
            Assert.IsFalse(File.Exists(_path));
            Assert.AreEqual(0, _fake.Stored.Count);
        }

        [Test]
        public void NudgeAndYawPersistAndTheNudgeSurvivesRecalibration()
        {
            AnchoredCalibrationSession s = Calibrated();

            s.Nudge(new Vector3(0.002f, 0f, -0.001f));
            s.AdjustYaw(1f);
            Assert.IsTrue(s.Persist());

            PianoProfileStore store = OnDisk();
            Near(new Vector3(2f, 0f, -1f), store.tipNudgeKeyboardMm, "nudge at the root, mm");
            Vector3 storedRight = (store.ActiveProfile.highFrontLocal - store.ActiveProfile.lowFrontLocal).normalized;
            Assert.AreEqual(1f, Vector3.Angle(Vector3.right, storedRight), 0.01f, "yaw baked into the geometry");

            Run(s.RecalibrateAsync());
            Capture(s, Low, High);
            Near(new Vector3(2f, 0f, -1f), s.NudgeMm, "nudge in the session");
            Near(new Vector3(2f, 0f, -1f), OnDisk().tipNudgeKeyboardMm, "nudge on disk");
            Assert.AreEqual(0f, s.SessionYawDegrees, "yaw belongs to the old capture");
        }

        [Test]
        public void ClearNudgeWritesZero()
        {
            AnchoredCalibrationSession s = Calibrated();
            s.Nudge(new Vector3(-0.009f, 0f, -0.012f));
            s.Persist();

            s.ClearNudge();

            Assert.AreEqual(Vector3.zero, s.NudgeMm);
            Assert.AreEqual(Vector3.zero, OnDisk().tipNudgeKeyboardMm);
            Near(Low, s.BuildLayout().LowFrontEdge, "back on the captured point");
        }

        [Test]
        public void YawIsClampedToTenDegrees()
        {
            AnchoredCalibrationSession s = Calibrated();
            s.AdjustYaw(25f);
            Assert.AreEqual(10f, s.SessionYawDegrees, 1e-4f);
        }

        [Test]
        public void CaptureOutsideCalibrationIsIgnored()
        {
            AnchoredCalibrationSession s = Calibrated();
            Capture(s, Low, High);
            Assert.AreEqual(1, _fake.CreateCount);
            Assert.AreEqual(FlowState.Verifying, s.Flow.State);
        }

        [Test]
        public void TipFieldsWrittenByTipCalibrationSurviveProfileWrites()
        {
            var serializer = new PianoProfileSerializer(_path);
            var tipStore = new PianoProfileStore
            {
                hasTipOffset = true,
                tipOffsetController = new Vector3(0f, -0.01f, 0.05f),
                tipOffsetRmsMm = 1.2f,
            };
            Assert.IsTrue(serializer.Save(tipStore, out _));

            Calibrated(serializer);

            PianoProfileStore store = OnDisk();
            Assert.IsTrue(store.hasTipOffset);
            Near(new Vector3(0f, -0.01f, 0.05f), store.tipOffsetController, "tip offset kept");
        }

        // ------------------------------------------------------------------

        /// <summary>Wraps the editor fake so a test can take the anchor's
        /// tracking away and give it back without killing the handle.</summary>
        private sealed class SwitchableService : IPianoAnchorService
        {
            private readonly EditorPianoAnchorService _inner;
            public SwitchableHandle Last { get; private set; }

            public SwitchableService(EditorPianoAnchorService inner) { _inner = inner; }

            public async Task<AnchorResult> CreateAsync(Vector3 position, Quaternion rotation) =>
                Wrap(await _inner.CreateAsync(position, rotation));

            public Task<AnchorResult> SaveAsync(IPianoAnchorHandle anchor) => _inner.SaveAsync(Unwrap(anchor));

            public async Task<AnchorResult> LoadAsync(string uuid, double localizeTimeoutSeconds) =>
                Wrap(await _inner.LoadAsync(uuid, localizeTimeoutSeconds));

            public Task<AnchorResult> EraseAsync(IPianoAnchorHandle anchor) => _inner.EraseAsync(Unwrap(anchor));

            public Task<AnchorResult> EraseByUuidAsync(string uuid) => _inner.EraseByUuidAsync(uuid);

            private AnchorResult Wrap(AnchorResult r)
            {
                if (r.Handle == null) return r;
                Last = new SwitchableHandle(r.Handle);
                return new AnchorResult(r.Status, r.Uuid, Last, r.Detail, r.ElapsedMs, r.LocalizeMs);
            }

            private static IPianoAnchorHandle Unwrap(IPianoAnchorHandle h) => h is SwitchableHandle s ? s.Inner : h;
        }

        private sealed class SwitchableHandle : IPianoAnchorHandle
        {
            public readonly IPianoAnchorHandle Inner;
            public bool Lost;

            public SwitchableHandle(IPianoAnchorHandle inner) { Inner = inner; }

            public string Id => Inner.Id;
            public Pose Pose => Inner.Pose;
            public bool IsTracked => !Lost && Inner.IsTracked;
            public bool IsAlive => Inner.IsAlive;
        }
    }
}
