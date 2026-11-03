using System;
using System.Threading.Tasks;
using UnityEngine;

namespace PianoGlass.Piano
{
    /// <summary>What the panel's "anchor:" line says. One value per thing
    /// the player can do something about.</summary>
    public enum AnchorPanelStatus
    {
        /// <summary>Reading piano_profiles.json.</summary>
        Starting,
        /// <summary>No usable profile yet: first-time setup, touch two keys.</summary>
        Calibrating,
        /// <summary>B pressed with a saved piano: touch two keys. The saved
        /// piano stays on disk until the new one is saved.</summary>
        Recalibrating,
        /// <summary>The saved anchor's load came back empty: it is gone.</summary>
        NotFound,
        /// <summary>Points captured; creating and saving the new anchor.</summary>
        Saving,
        Localizing,
        /// <summary>A localize attempt failed or timed out; trying again.</summary>
        Retrying,
        /// <summary>Gave up retrying on its own; trigger retries, B recalibrates.</summary>
        LocalizeFailed,
        /// <summary>Overlays drawn; waiting for A (looks right), right grip
        /// (Middle C fix) or B (recalibrate).</summary>
        Verifying,
        /// <summary>Middle C fix: waiting for the touch.</summary>
        Correcting,
        /// <summary>Confirmed by the player; overlays drawn.</summary>
        Ready,
        /// <summary>Anchor localized but the runtime has lost it. Nothing drawn.</summary>
        Untracked,
    }

    public static class AnchorStatusText
    {
        public static AnchorPanelStatus Classify(FlowState state, int localizeFailures, bool recalibrateOffered,
                                                 bool savingNewAnchor, bool anchorNotFound, bool hasProfile = false)
        {
            switch (state)
            {
                case FlowState.Launch:
                    return AnchorPanelStatus.Starting;
                case FlowState.Localizing:
                    if (recalibrateOffered) return AnchorPanelStatus.LocalizeFailed;
                    return localizeFailures > 0 ? AnchorPanelStatus.Retrying : AnchorPanelStatus.Localizing;
                case FlowState.FullCalibration:
                    if (savingNewAnchor) return AnchorPanelStatus.Saving;
                    if (anchorNotFound) return AnchorPanelStatus.NotFound;
                    return hasProfile ? AnchorPanelStatus.Recalibrating : AnchorPanelStatus.Calibrating;
                case FlowState.Suspended:
                    return AnchorPanelStatus.Untracked;
                case FlowState.Verifying:
                    return AnchorPanelStatus.Verifying;
                case FlowState.Correcting:
                    return AnchorPanelStatus.Correcting;
                default:
                    return AnchorPanelStatus.Ready;
            }
        }

        /// <summary>The Verifying prompt, exactly as the panel shows it.</summary>
        public const string VerifyBanner = "A: looks right · right grip: Middle C fix · B: recalibrate";

        /// <summary>The panel line, without the "anchor:" label.
        /// <paramref name="awaitingHighest"/>: the lowest key is already
        /// touched (only changes the Recalibrating line).</summary>
        public static string Line(AnchorPanelStatus s, int localizeFailures, int maxAttempts,
                                  bool awaitingHighest = false) => s switch
        {
            AnchorPanelStatus.Starting       => "starting",
            AnchorPanelStatus.Calibrating    => "no saved piano yet -- calibrate (touch two keys)",
            AnchorPanelStatus.Recalibrating  => awaitingHighest
                                                    ? "recalibrating -- now the highest key"
                                                    : "recalibrating -- touch the lowest key",
            AnchorPanelStatus.NotFound       => "not found -- saved piano is gone; calibrate again",
            AnchorPanelStatus.Saving         => "saving the piano's position...",
            AnchorPanelStatus.Localizing     => "localizing -- look at the piano",
            AnchorPanelStatus.Retrying       => $"retrying -- attempt {localizeFailures + 1} of {maxAttempts}",
            AnchorPanelStatus.LocalizeFailed => $"not localized after {maxAttempts} tries -- trigger = retry, B = recalibrate",
            AnchorPanelStatus.Verifying      => "verifying -- do the keys line up?",
            AnchorPanelStatus.Correcting     => "Middle C fix -- touch the front edge of Middle C, pull the trigger",
            AnchorPanelStatus.Ready          => "ready",
            AnchorPanelStatus.Untracked      => "untracked -- keys hidden until the piano is found again",
            _                                => s.ToString(),
        };
    }

    /// <summary>
    /// Anchored calibration, end to end, with no MonoBehaviour and no Meta
    /// types: CalibrationFlow decides, this carries out its commands against
    /// an IPianoAnchorService and piano_profiles.json, and holds the bound
    /// anchor plus the keyboard expressed in its frame. CalibrationFlowController
    /// is the thin Unity shell around it; the EditMode tests drive it with
    /// EditorPianoAnchorService.
    ///
    /// The rules it exists to keep, on top of CalibrationFlow's own:
    ///   * Nothing is created on launch. An anchor is created only after a
    ///     two-touch capture.
    ///   * The old anchor is erased only after the profile naming the new
    ///     one has been written. If that write fails, the old one stays.
    ///   * A failed, slow or empty load never erases anything and never
    ///     rewrites the profile.
    ///   * Only anchor-local geometry is stored. The world pose is read from
    ///     the live anchor every time it is needed.
    /// </summary>
    public sealed class AnchoredCalibrationSession
    {
        public CalibrationFlow Flow { get; }
        public int MaxLocalizeAttempts { get; }

        /// <summary>The anchor the keyboard is currently placed from, or null
        /// (launch, localizing, mid-recalibration).</summary>
        public IPianoAnchorHandle Anchor { get; private set; }

        /// <summary>Keyboard in Anchor's frame, any yaw fine-adjust already
        /// applied. Meaningless while Anchor is null.</summary>
        public KeyboardGeometry LocalGeometry { get; private set; }
        public int LowestNote { get; private set; }
        public int HighestNote { get; private set; }
        public float WhiteKeyDepth { get; private set; }

        /// <summary>The piano's white key depth as configured in the scene.
        /// When set, it wins over the depth saved in the profile: the depth is
        /// a property of the piano, and profiles saved before the keys were
        /// measured hold the old 145 mm guess. Null keeps the saved value.</summary>
        public float? ConfiguredWhiteKeyDepth { get; set; }

        /// <summary>Millimetres along (right, up, toward player). The store
        /// root's tipNudgeKeyboardMm: survives recalibration.</summary>
        public Vector3 NudgeMm { get; private set; }

        /// <summary>Yaw fine-adjust since the geometry was last loaded or
        /// captured, for display and the ±10° clamp. The geometry itself
        /// already includes it.</summary>
        public float SessionYawDegrees { get; private set; }

        /// <summary>The last launch load came back empty. Cleared once a
        /// replacement anchor is saved.</summary>
        public bool AnchorWasNotFound { get; private set; }

        /// <summary>Last thing that went wrong, in words for the panel; null
        /// once something succeeds.</summary>
        public string LastProblem { get; private set; }

        /// <summary>The anchor UUID piano_profiles.json currently names.</summary>
        public string SavedAnchorUuid { get; private set; }

        /// <summary>What the last Middle C fix measured and applied, in words
        /// for the panel; null until one is applied on the current anchor.</summary>
        public string CorrectionNote { get; private set; }

        /// <summary>The last Middle C touch's raw offset from where the key
        /// should be, millimetres along (right, up, toward player) -- before
        /// the vertical cap. Zero until one is measured.</summary>
        public Vector3 LastVerifyErrorMm { get; private set; }

        public AnchorPanelStatus Status => AnchorStatusText.Classify(
            Flow.State, Flow.LocalizeFailures, Flow.RecalibrateOffered, Flow.SavingNewAnchor, AnchorWasNotFound,
            Flow.HasProfile);

        /// <summary>Verifying, with the keys actually on show: A confirms.</summary>
        public bool AwaitingConfirmation => Flow.State == FlowState.Verifying && IsOverlayVisible;

        public string StatusLine => AnchorStatusText.Line(Status, Flow.LocalizeFailures, MaxLocalizeAttempts);

        /// <summary>The flow says draw, and the anchor is there and tracked.</summary>
        public bool IsOverlayVisible =>
            Flow.IsOverlayVisible && Anchor != null && Anchor.IsAlive && Anchor.IsTracked;

        /// <summary>BeginCapture: put the keyboard into two-touch capture.</summary>
        public event Action CaptureRequested;

        /// <summary>Anchor, geometry or nudge changed: rebuild the layout.</summary>
        public event Action PlacementChanged;

        /// <summary>Every handled flow event, before its commands run. For
        /// observers (the in-headset device checklist); decides nothing.</summary>
        public event Action<FlowEvent, FlowTransition> Transitioned;

        /// <summary>lastCorrection of the profile read at launch; null if none.</summary>
        public string LaunchLastCorrection { get; private set; }

        private readonly IPianoAnchorService _service;
        private readonly PianoProfileSerializer _serializer;
        private readonly double _localizeTimeoutSeconds;
        private readonly Func<DateTime> _utcNow;
        private readonly Func<string> _newId;

        private PianoProfile _savedProfile;
        private Capture? _pending;
        private bool _writeIsNewCalibration;
        /// <summary>Set by an accepted Middle C touch for the WriteProfile
        /// that follows: the applied shift, anchor-local mm.</summary>
        private Vector3? _writeCorrectionMm;
        private bool _savedAnchorGone;
        private int _loadGeneration;

        private readonly struct Capture
        {
            public readonly KeyboardGeometry World;
            public readonly int Lowest, Highest;
            public readonly float Depth;

            public Capture(KeyboardGeometry world, int lowest, int highest, float depth)
            {
                World = world; Lowest = lowest; Highest = highest; Depth = depth;
            }
        }

        public AnchoredCalibrationSession(IPianoAnchorService service, PianoProfileSerializer serializer,
                                          double localizeTimeoutSeconds = 10,
                                          int maxLocalizeAttempts = CalibrationFlow.DefaultMaxLocalizeAttempts,
                                          Func<DateTime> utcNow = null, Func<string> newId = null)
        {
            _service = service ?? throw new ArgumentNullException(nameof(service));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _localizeTimeoutSeconds = localizeTimeoutSeconds;
            MaxLocalizeAttempts = maxLocalizeAttempts < 1 ? 1 : maxLocalizeAttempts;
            Flow = new CalibrationFlow(MaxLocalizeAttempts);
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
            _newId = newId ?? (() => Guid.NewGuid().ToString("D"));
        }

        // ------------------------------------------------------------------
        // Inputs
        // ------------------------------------------------------------------

        /// <summary>Launch: read the profile and either localize it or ask
        /// for a calibration. Never creates an anchor.</summary>
        public Task StartAsync()
        {
            ProfileLoadResult loaded = _serializer.Load();
            if (loaded.Status == ProfileLoadStatus.Invalid)
                Log($"profile file invalid ({loaded.Reason}); moved to {loaded.RenamedTo ?? "(could not move)"}");

            NudgeMm = loaded.Store != null ? loaded.Store.tipNudgeKeyboardMm : Vector3.zero;

            ProfileCheck check = PianoProfileEdits.CheckForLaunch(loaded);
            if (!check.Valid)
            {
                Log($"launch: no usable profile ({check.Reason}) -- first-time setup");
                return Fire(FlowEvent.ProfileMissingOrInvalid);
            }

            _savedProfile = check.Profile;
            SavedAnchorUuid = check.Profile.PrimaryAnchorUuid;
            LaunchLastCorrection = check.Profile.lastCorrection;
            Log($"launch: profile '{check.Profile.profileId}' anchor={SavedAnchorUuid} " +
                $"notes {check.Profile.lowestNote}-{check.Profile.highestNote} nudge={Mm(NudgeMm)}");
            return Fire(FlowEvent.ProfileValid);
        }

        /// <summary>Both keys touched. World-space front edges and the
        /// toward-player direction, exactly as captured -- no nudge, no yaw.</summary>
        public Task OnCapturedAsync(Vector3 lowFront, Vector3 highFront, Vector3 towardPlayer,
                                    int lowestNote, int highestNote, float whiteKeyDepth)
        {
            if (Flow.State != FlowState.FullCalibration || Flow.SavingNewAnchor)
            {
                Log($"capture ignored in {Flow.State}");
                return Task.CompletedTask;
            }
            _pending = new Capture(new KeyboardGeometry(lowFront, highFront, AnchoredKeyboard.Horizontal(towardPlayer).normalized),
                                   lowestNote, highestNote, whiteKeyDepth);
            return Fire(FlowEvent.CaptureCompleted);
        }

        /// <summary>B. Never touches the profile or the old anchor on the way in.</summary>
        public Task RecalibrateAsync()
        {
            // A load still in flight belongs to the attempt being abandoned.
            _loadGeneration++;
            return Fire(FlowEvent.RecalibrateRequested);
        }

        /// <summary>Trigger after localization has given up on its own.</summary>
        public Task RetryAsync() =>
            Flow.State == FlowState.Localizing && Flow.RecalibrateOffered
                ? Fire(FlowEvent.RetryRequested)
                : Task.CompletedTask;

        // ------------------------------------------------------------------
        // Verifying (A, right grip, Middle C touch)
        // ------------------------------------------------------------------

        /// <summary>A while Verifying: Ready, and lastVerifiedUtc written.
        /// False (and nothing happens) in any other state, so the press can
        /// go to the transport instead.</summary>
        public bool Confirm()
        {
            if (!AwaitingConfirmation) return false;
            _ = Fire(FlowEvent.ConfirmPressed);
            return true;
        }

        /// <summary>Right grip: start a Middle C fix from Verifying or Ready,
        /// or cancel the one in progress.</summary>
        public Task ToggleCorrectionAsync()
        {
            if (Flow.State == FlowState.Correcting) return Fire(FlowEvent.CorrectionCancelled);
            if (!IsOverlayVisible) return Task.CompletedTask;

            PianoKeyLayout layout = BuildLayout();
            if (layout == null || !layout.Contains(AnchoredKeyboard.MiddleC))
            {
                LastProblem = "Middle C is not on this keyboard -- no Middle C fix.";
                Log($"Middle C fix unavailable: note 60 outside {LowestNote}-{HighestNote}");
                return Task.CompletedTask;
            }
            return Fire(FlowEvent.CorrectionStarted);
        }

        /// <summary>
        /// The trigger while Correcting: <paramref name="tipWorld"/> is where
        /// the controller tip touched Middle C's front edge. Measures it
        /// against the front edge the layout on show says Middle C has
        /// (nudge included), and applies the offset as a rigid shift of the
        /// anchor-local geometry -- vertical capped, anything over
        /// AnchoredKeyboard.MovedMetres refused with nothing written.
        /// </summary>
        public Task OnMiddleCTouchedAsync(Vector3 tipWorld)
        {
            if (Flow.State != FlowState.Correcting || Anchor == null)
            {
                Log($"Middle C touch ignored in {Flow.State}");
                return Task.CompletedTask;
            }

            PianoKeyLayout layout = BuildLayout();
            Vector3 expected = layout.FrontEdgeCenter(AnchoredKeyboard.MiddleC);
            OnePointCorrection c = AnchoredKeyboard.CorrectOnePoint(expected, tipWorld);

            Vector3 rawMm = AnchoredKeyboard.NudgeToKeyboardMm(tipWorld - expected, layout.Right, layout.Up, layout.Forward);
            LastVerifyErrorMm = rawMm;
            string error = $"verify error x/y/z mm = {rawMm.x:+0.0;-0.0;0} / {rawMm.y:+0.0;-0.0;0} / {rawMm.z:+0.0;-0.0;0} " +
                           $"(total {rawMm.magnitude:0.0} mm)";

            if (!c.Accepted)
            {
                Log($"{error} -- rejected, over {AnchoredKeyboard.MovedMetres * 1000f:0} mm");
                LastProblem = c.Reason;
                return Fire(FlowEvent.CorrectionRejectedMoved);
            }

            // The anchor is yaw-only, so its local y is world up and the
            // capped vertical stays capped.
            Vector3 local = AnchoredKeyboard.DirectionToLocal(Anchor.Pose, c.Delta);
            LocalGeometry = AnchoredKeyboard.Translate(LocalGeometry, local);
            _writeCorrectionMm = local * 1000f;

            Vector3 appliedMm = AnchoredKeyboard.NudgeToKeyboardMm(c.Delta, layout.Right, layout.Up, layout.Forward);
            bool capped = Mathf.Abs(rawMm.y - appliedMm.y) > 0.05f;
            Log($"{error} -- applied {Mm(appliedMm)}" + (capped ? $" (vertical capped from {rawMm.y:+0.0;-0.0} mm)" : ""));

            LastProblem = null;
            CorrectionNote = $"Middle C fix: x {appliedMm.x:+0.0;-0.0;0} · y {appliedMm.y:+0.0;-0.0;0} · " +
                             $"z {appliedMm.z:+0.0;-0.0;0} mm" + (capped ? $"   (y capped; measured {rawMm.y:+0.0;-0.0})" : "");
            PlacementChanged?.Invoke();
            return Fire(FlowEvent.CorrectionApplied);
        }

        /// <summary>
        /// Call every frame. State-based rather than edge-based: whatever the
        /// flow thinks, if it is drawing and the anchor is not tracked it
        /// suspends, and if it is suspended and the anchor is back it
        /// resumes. No missed edge can leave keys drawn on a lost anchor.
        /// </summary>
        public void UpdateTracking()
        {
            bool tracked = Anchor != null && Anchor.IsAlive && Anchor.IsTracked;
            if (Flow.State == FlowState.Suspended && tracked)
                _ = Fire(FlowEvent.TrackingRegained);
            else if (Flow.IsOverlayVisible && !tracked)
                _ = Fire(FlowEvent.TrackingLost);
        }

        // ------------------------------------------------------------------
        // Fine adjust (sticks)
        // ------------------------------------------------------------------

        /// <summary>Metres along (right, up, toward player), as
        /// KeyboardCalibration.Nudge has always taken them.</summary>
        public void Nudge(Vector3 deltaKeyboardMetres)
        {
            NudgeMm += deltaKeyboardMetres * 1000f;
            PlacementChanged?.Invoke();
        }

        public void AdjustYaw(float deltaDegrees)
        {
            if (Anchor == null) return;
            float target = Mathf.Clamp(SessionYawDegrees + deltaDegrees, -10f, 10f);
            float step = target - SessionYawDegrees;
            if (Mathf.Approximately(step, 0f)) return;

            // The anchor is yaw-only, so its local up is world up and
            // RotateYaw works in its frame unchanged.
            LocalGeometry = AnchoredKeyboard.RotateYaw(LocalGeometry, step);
            SessionYawDegrees = target;
            PlacementChanged?.Invoke();
        }

        public void ClearNudge()
        {
            NudgeMm = Vector3.zero;
            PlacementChanged?.Invoke();
            Persist();
        }

        /// <summary>
        /// Write the nudge, and the yaw-adjusted geometry if the profile on
        /// disk names the anchor in use. Called when the sticks come to rest.
        /// </summary>
        public bool Persist()
        {
            PianoProfileStore store = LoadForWrite(out string error);
            if (store == null) { Log("adjustment not saved: " + error); return false; }

            store.tipNudgeKeyboardMm = NudgeMm;
            bool geometry = Anchor != null && Anchor.Id == SavedAnchorUuid &&
                            PianoProfileEdits.ApplyGeometry(store, Anchor.Id, LocalGeometry);

            if (!_serializer.Save(store, out error)) { Log("adjustment not saved: " + error); return false; }
            Log($"saved nudge {Mm(NudgeMm)}" + (geometry ? $", yaw {SessionYawDegrees:+0.00;-0.00;0} deg baked into geometry" : ""));
            return true;
        }

        public PianoKeyLayout BuildLayout() =>
            Anchor == null
                ? null
                : AnchoredKeyboard.BuildLayout(Anchor.Pose, LocalGeometry, LowestNote, HighestNote, WhiteKeyDepth, NudgeMm);

        // ------------------------------------------------------------------
        // Flow
        // ------------------------------------------------------------------

        private async Task Fire(FlowEvent e)
        {
            FlowTransition t = Flow.Handle(e);
            if (!t.Handled)
            {
                Log($"{e} ignored in {t.From}");
                return;
            }
            Log($"{e}: {t}");
            Transitioned?.Invoke(e, t);

            // Read before WriteProfile replaces it.
            string oldUuid = SavedAnchorUuid;
            bool profileWritten = false;

            foreach (FlowCommand c in t.Commands)
            {
                switch (c)
                {
                    case FlowCommand.LoadAndLocalizeAnchor:
                        await LoadAndLocalize();
                        break;

                    case FlowCommand.BeginCapture:
                        Detach();
                        CaptureRequested?.Invoke();
                        break;

                    case FlowCommand.CreateAndSaveAnchor:
                        await CreateAndSave();
                        break;

                    case FlowCommand.WriteProfile:
                        profileWritten = WriteProfile();
                        break;

                    case FlowCommand.EraseOldAnchor:
                        await EraseOld(oldUuid, profileWritten);
                        break;

                    case FlowCommand.MarkVerified:
                        WriteVerified();
                        break;

                    case FlowCommand.OfferRecalibrate:
                        Log(t.From == FlowState.Correcting
                            ? "Middle C fix refused: piano may have moved -- B recalibrates"
                            : "localization gave up retrying on its own: trigger retries, B recalibrates");
                        break;
                }
            }
        }

        private async Task LoadAndLocalize()
        {
            int generation = ++_loadGeneration;
            string uuid = SavedAnchorUuid;
            Log($"load requested uuid={uuid} attempt {Flow.LocalizeFailures + 1}/{MaxLocalizeAttempts}");

            AnchorResult r = await _service.LoadAsync(uuid, _localizeTimeoutSeconds);
            Log($"load {r}");

            if (generation != _loadGeneration || Flow.State != FlowState.Localizing)
            {
                // Recalibration began meanwhile. A handle that did bind stays
                // bound and unused; EraseByUuid still finds it later.
                Log($"load result for {uuid} arrived after the flow moved on; ignored");
                return;
            }

            switch (r.Status)
            {
                case AnchorStatus.Success:
                    LastProblem = null;
                    Attach(r.Handle, _savedProfile.LocalGeometry, _savedProfile.lowestNote,
                           _savedProfile.highestNote, ConfiguredWhiteKeyDepth ?? _savedProfile.whiteKeyDepth);
                    await Fire(FlowEvent.LocalizeSucceeded);
                    break;

                case AnchorStatus.NotFound:
                    AnchorWasNotFound = true;
                    _savedAnchorGone = true;
                    LastProblem = "The saved anchor is gone (spatial data cleared?). Calibrate again.";
                    await Fire(FlowEvent.AnchorNotFound);
                    break;

                case AnchorStatus.Timeout:
                    LastProblem = "Localizing timed out.";
                    await Fire(FlowEvent.LocalizeTimedOut);
                    break;

                default:
                    LastProblem = $"Localizing failed: {r.Detail}";
                    await Fire(FlowEvent.LocalizeFailed);
                    break;
            }
        }

        private async Task CreateAndSave()
        {
            if (_pending == null)
            {
                await Fire(FlowEvent.NewAnchorSaveFailed);
                return;
            }
            Capture cap = _pending.Value;
            _pending = null;

            // Refuse before creating anything: a profile that fails this on
            // the next launch would just leak its anchor.
            string implausible = AnchoredKeyboard.CheckPlausible(cap.World, cap.Lowest, cap.Highest);
            if (implausible != null)
            {
                LastProblem = $"Not saved: {implausible}. Were those the lowest and highest keys? Touch them again.";
                Log("capture rejected: " + implausible);
                await Fire(FlowEvent.NewAnchorSaveFailed);
                return;
            }

            Pose frame = AnchoredKeyboard.Frame(cap.World.LowFront, cap.World.HighFront, cap.World.TowardPlayer);
            AnchorResult created = await _service.CreateAsync(frame.position, frame.rotation);
            Log($"create {created}");
            if (!created.Ok)
            {
                LastProblem = $"Could not create the anchor ({created.Status}). Touch the keys again.";
                await Fire(FlowEvent.NewAnchorSaveFailed);
                return;
            }

            // Relative to the pose the runtime actually gave the anchor, not
            // the one asked for.
            Pose actual = created.Handle.Pose;
            KeyboardGeometry local = AnchoredKeyboard.ToLocal(actual, cap.World.LowFront, cap.World.HighFront, cap.World.TowardPlayer);

            AnchorResult saved = await _service.SaveAsync(created.Handle);
            Log($"save {saved}");
            if (!saved.Ok)
            {
                // Unsaved, so nothing persists; it dies with the session.
                LastProblem = $"Could not save the anchor ({saved.Status}). Touch the keys again.";
                await Fire(FlowEvent.NewAnchorSaveFailed);
                return;
            }

            LastProblem = null;
            AnchorWasNotFound = false;
            Attach(created.Handle, local, cap.Lowest, cap.Highest, cap.Depth);
            _writeIsNewCalibration = true;
            await Fire(FlowEvent.NewAnchorSaved);
        }

        private bool WriteProfile()
        {
            bool newCalibration = _writeIsNewCalibration;
            _writeIsNewCalibration = false;
            Vector3? correctionMm = _writeCorrectionMm;
            _writeCorrectionMm = null;

            PianoProfileStore store = LoadForWrite(out string error);
            if (store == null)
            {
                LastProblem = "Piano position NOT saved: " + error;
                Log("profile not written: " + error);
                return false;
            }

            if (newCalibration)
            {
                string utc = _utcNow().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
                PianoProfileEdits.ApplyCalibration(store, Anchor.Id, LocalGeometry, LowestNote, HighestNote,
                                                   WhiteKeyDepth, NudgeMm, utc, _newId());
            }
            else if (Anchor != null)
            {
                bool applied = correctionMm.HasValue
                    ? PianoProfileEdits.ApplyCorrection(store, Anchor.Id, LocalGeometry, "middle-c", correctionMm.Value)
                    : PianoProfileEdits.ApplyGeometry(store, Anchor.Id, LocalGeometry);
                if (!applied && correctionMm.HasValue)
                {
                    // Geometry local to one anchor is meaningless for another.
                    LastProblem = "Middle C fix shown but NOT saved: the saved piano names a different anchor.";
                    Log("correction not written: the profile on disk names a different anchor");
                }
                store.tipNudgeKeyboardMm = NudgeMm;
            }

            if (!_serializer.Save(store, out error))
            {
                LastProblem = "Piano position NOT saved: " + error;
                Log("profile not written: " + error);
                return false;
            }

            SavedAnchorUuid = store.ActiveProfile?.PrimaryAnchorUuid;
            _savedProfile = store.ActiveProfile;
            Log($"profile written: anchor={SavedAnchorUuid}" +
                (correctionMm.HasValue ? $" correction=middle-c {Mm(correctionMm.Value)} anchor-local" : "") +
                $" -> {_serializer.Path}");
            return true;
        }

        private async Task EraseOld(string oldUuid, bool profileWritten)
        {
            if (!profileWritten)
            {
                Log($"old anchor {oldUuid ?? "-"} kept: the new profile was not written");
                return;
            }
            if (string.IsNullOrEmpty(oldUuid) || oldUuid == SavedAnchorUuid)
            {
                Log("no old anchor to erase");
                return;
            }
            if (_savedAnchorGone)
            {
                // The launch load said it no longer exists.
                _savedAnchorGone = false;
                Log($"old anchor {oldUuid} was already gone; nothing to erase");
                return;
            }

            AnchorResult r = await _service.EraseByUuidAsync(oldUuid);
            // A failed erase leaks one anchor in device storage; nothing
            // reads it again, so it is logged and left.
            Log($"erase old {r}");
        }

        private void WriteVerified()
        {
            PianoProfileStore store = LoadForWrite(out string error);
            if (store == null) { Log("verified not recorded: " + error); return; }
            string utc = _utcNow().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
            PianoProfileEdits.MarkVerified(store, utc);
            if (!_serializer.Save(store, out error)) { Log("verified not recorded: " + error); return; }
            Log($"verified by the player: lastVerifiedUtc={utc}");
        }

        // ------------------------------------------------------------------

        private void Attach(IPianoAnchorHandle handle, KeyboardGeometry local, int lowest, int highest, float depth)
        {
            Anchor = handle;
            LocalGeometry = local;
            LowestNote = lowest;
            HighestNote = highest;
            WhiteKeyDepth = depth;
            SessionYawDegrees = 0f;
            CorrectionNote = null;
            Log($"bound anchor {handle.Id} tracked={handle.IsTracked}");
            PlacementChanged?.Invoke();
        }

        private void Detach()
        {
            if (Anchor == null) return;
            Log($"released anchor {Anchor.Id} (not erased)");
            Anchor = null;
            PlacementChanged?.Invoke();
        }

        /// <summary>The store as it is on disk right now, for a
        /// read-modify-write. Null (with a reason) only when the file belongs
        /// to a newer build.</summary>
        private PianoProfileStore LoadForWrite(out string error)
        {
            error = null;
            ProfileLoadResult r = _serializer.Load();
            if (r.Status == ProfileLoadStatus.FutureVersion)
            {
                error = r.Reason;
                return null;
            }
            return r.Store ?? new PianoProfileStore();
        }

        private static string Mm(Vector3 v) => $"({v.x:+0.0;-0.0;0}, {v.y:+0.0;-0.0;0}, {v.z:+0.0;-0.0;0}) mm";

        private static void Log(string message) => Debug.Log("[AnchorFlow] " + message);
    }
}
