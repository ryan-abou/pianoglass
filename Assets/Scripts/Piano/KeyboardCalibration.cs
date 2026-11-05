using System;
using System.IO;
using UnityEngine;

namespace PianoGlass.Piano
{
    /// <summary>
    /// Two-point keyboard calibration.
    ///
    /// The user touches the front edge of the lowest key with the controller
    /// tip and pulls the trigger, then does the same on the highest key. That's
    /// it — every other key is derived by PianoKeyLayout.
    ///
    /// Touching the key is deliberate. Pointing a laser at it from across the
    /// room amplifies every degree of hand wobble into centimetres of error at
    /// the keyboard, and on a Quest 2 you can't see the keys well enough to aim
    /// anyway. Touching is accurate to a few millimetres because you can feel
    /// when you have arrived.
    /// </summary>
    public class KeyboardCalibration : MonoBehaviour
    {
        /// <summary>Waiting: anchored mode only -- the placement owner is
        /// busy (localizing, saving a new anchor) and the trigger does not
        /// capture.</summary>
        public enum Stage { Idle, AwaitingLowest, AwaitingHighest, Calibrated, Waiting }

        [Header("Keyboard")]
        [Tooltip("Number of keys on your keyboard. Sets the note range below.")]
        [SerializeField] private int keyCount = 61;
        [SerializeField] private int lowestNote = 36;    // C2 on a 61-key
        [SerializeField] private int highestNote = 96;   // C7 on a 61-key
        [Tooltip("Front-to-back length of a white key, in metres. 147.64 mm on Ryan's piano " +
                 "(measured 2026-11-03). A property of the piano: saved calibrations do not override it.")]
        [SerializeField] private float whiteKeyDepth = PianoKeyLayout.MeasuredWhiteKeyDepth;

        [Header("Pointing")]
        [Tooltip("Transform at the tip of the controller. Create an empty child of " +
                 "the right controller anchor and move it to where the plastic tip is.")]
        [SerializeField] private Transform pointerTip;
        [Tooltip("The player's head, used to work out which side of the keyboard they are on.")]
        [SerializeField] private Transform headTransform;

        [Header("Persistence")]
        [SerializeField] private bool loadOnStart = true;
        [SerializeField] private string fileName = "keyboard_calibration.json";

        public Stage CurrentStage { get; private set; } = Stage.Idle;
        public PianoKeyLayout Layout { get; private set; }

        /// <summary>The transform whose position is captured. Exposed so
        /// ControllerTipCalibration can move it onto the real plastic tip.</summary>
        public Transform PointerTip => pointerTip;
        public bool IsCalibrated => Layout != null;

        /// <summary>The configured white key depth, in metres.</summary>
        public float WhiteKeyDepth => whiteKeyDepth;

        /// <summary>
        /// Whether anything may be drawn at Layout's coordinates right now.
        /// Separate from IsCalibrated on purpose: a layout can exist and still
        /// be untrustworthy (mid-recalibration today; an untracked or
        /// unlocalized anchor later). Every overlay -- keys, highway, edge
        /// markers -- hides while this is false, so nothing is ever drawn at
        /// stale coordinates.
        /// </summary>
        public bool IsPlacementValid { get; private set; }

        /// <summary>Fires whenever a new layout becomes available, including on load.</summary>
        public event Action<PianoKeyLayout> Calibrated;

        /// <summary>Fires when IsPlacementValid changes. Not folded into
        /// Calibrated(null): CanvasCalibrationFollower would throw on a null
        /// layout, and PianoKeyHighlighter would destroy all 88 keys.</summary>
        public event Action<bool> PlacementValidChanged;

        /// <summary>Short line to show the user during setup.</summary>
        public string Prompt => CurrentStage switch
        {
            Stage.AwaitingLowest  => $"Touch the front edge of the LOWEST key ({NoteName(lowestNote)}) and pull the trigger",
            Stage.AwaitingHighest => $"Now the HIGHEST key ({NoteName(highestNote)})",
            Stage.Calibrated      => _owner?.PlacedPrompt ??
                                     "Calibrated.  sticks = nudge   left stick L/R = yaw   B = redo   stick click = clear nudge",
            Stage.Waiting         => _owner != null ? _owner.WaitingPrompt : "",
            _                     => "Press the trigger to start calibration"
        };

        private IKeyboardPlacementOwner _owner;

        /// <summary>True when an anchor (via its owner) decides placement,
        /// not this component's own captured points.</summary>
        public bool IsExternallyPlaced => _owner != null;

        /// <summary>Anchored mode: the keys are on show and the owner wants
        /// A to confirm them. SongTransport leaves A alone meanwhile.</summary>
        public bool AwaitingConfirmation => _owner != null && _owner.AwaitingConfirmation;

        /// <summary>Calibration has instructions for the player to read:
        /// capturing, localizing, Verifying, or a Middle C fix. Nudging does
        /// not count; it needs no instructions.</summary>
        public bool NeedsPlayer => !IsPlacementValid || CurrentStage != Stage.Calibrated || AwaitingConfirmation ||
                                   (_owner != null && _owner.IsCorrecting);

        private int _confirmedFrame = -1;

        /// <summary>A confirmed the placement this frame. Lets SongTransport
        /// ignore that same press whichever Update runs first.</summary>
        public bool ConfirmedThisFrame => _confirmedFrame == Time.frameCount;

        /// <summary>A pressed. True if it confirmed the placement, in which
        /// case nothing else should act on that press.</summary>
        public bool Confirm()
        {
            if (!AwaitingConfirmation) return false;
            _confirmedFrame = Time.frameCount;
            _owner.Confirm();
            return true;
        }

        /// <summary>Right grip: start or cancel a Middle C fix (anchored mode).</summary>
        public void ToggleCorrection() => _owner?.ToggleCorrection();

        private Vector3 _lowPoint;
        private Vector3 _highPoint;

        /// <summary>World-space correction applied to both captured points.
        /// Absorbs the difference between the controller anchor and where the
        /// tip physically touched — a constant error that shifts the whole
        /// keyboard without distorting the spacing between keys.</summary>
        private Vector3 _nudge;

        /// <summary>Fine rotation about the vertical axis through the
        /// keyboard's centre, degrees. Replaces holding the Meta button to
        /// recenter until the piano happens to line up. Unlike the nudge it
        /// belongs to the captured points, so a fresh capture resets it.</summary>
        private float _yawDegrees;

        public float YawDegrees => _owner != null ? _owner.YawDegrees : _yawDegrees;

        private string FilePath => Path.Combine(Application.persistentDataPath, fileName);

        private void Reset()
        {
            var range = PianoKeyLayout.RangeForKeyCount(keyCount);
            lowestNote = range.low;
            highestNote = range.high;
        }

        private void OnValidate()
        {
            var range = PianoKeyLayout.RangeForKeyCount(keyCount);
            lowestNote = range.low;
            highestNote = range.high;
        }

        private void Start()
        {
            if (headTransform == null && Camera.main != null)
                headTransform = Camera.main.transform;

            // Anchored mode: the owner decides between localizing and a
            // fresh capture. The legacy file's tracking-space points must not
            // be drawn first -- nothing shows until the anchor is found.
            if (_owner != null) return;

            if (loadOnStart && Load()) return;
            CurrentStage = Stage.AwaitingLowest;
        }

        // ------------------------------------------------------------------
        // Anchored mode -- see IKeyboardPlacementOwner
        // ------------------------------------------------------------------

        /// <summary>Hand placement to <paramref name="owner"/>. Call in Awake,
        /// before this component's Start, so the legacy file is never loaded.</summary>
        public void SetPlacementOwner(IKeyboardPlacementOwner owner)
        {
            _owner = owner;
            Layout = null;
            CurrentStage = Stage.Waiting;
            SetPlacementValid(false);
            Debug.Log("[Calibration] placement owned by the anchor flow; keyboard_calibration.json not used");
        }

        /// <summary>The owner wants two fresh touches. Hides everything.</summary>
        public void BeginExternalCapture()
        {
            Layout = null;
            CurrentStage = Stage.AwaitingLowest;
            SetPlacementValid(false);
            Debug.Log("[Calibration] capture started (anchored)");
        }

        /// <summary>The owner's layout for this frame. Fires Calibrated, so
        /// overlays follow it exactly as they follow a nudge. Visibility is
        /// separate: SetExternalPlacementValid.</summary>
        public void PublishExternalLayout(PianoKeyLayout layout)
        {
            if (_owner == null || layout == null) return;
            Layout = layout;
            CurrentStage = Stage.Calibrated;
            Calibrated?.Invoke(layout);
        }

        public void SetExternalPlacementValid(bool valid)
        {
            if (_owner == null) return;
            SetPlacementValid(valid && Layout != null);
        }

        /// <summary>
        /// Call this when the capture button is pressed. `recalibrateModifier`
        /// (the other trigger held down) restarts calibration instead.
        ///
        /// This class deliberately contains no Meta-specific code, so it compiles
        /// before the Meta XR SDK is installed. OvrCalibrationInput drives it on
        /// the headset; a UI button or the context menu drives it anywhere else.
        /// </summary>
        public void OnCaptureButton(bool recalibrateModifier = false)
        {
            if (recalibrateModifier && CurrentStage == Stage.Calibrated)
            {
                Recalibrate();
                return;
            }
            CapturePoint();
        }

        /// <summary>Capture whichever point we're waiting for. Also callable from
        /// a UI button, or from the inspector context menu while testing.</summary>
        [ContextMenu("Capture point")]
        public void CapturePoint()
        {
            if (pointerTip == null)
            {
                Debug.LogError("[Calibration] pointerTip is not assigned.");
                return;
            }

            switch (CurrentStage)
            {
                case Stage.Idle:
                    CurrentStage = Stage.AwaitingLowest;
                    break;

                case Stage.Waiting:
                    _owner?.OnTriggerWhileWaiting();
                    break;

                case Stage.Calibrated:
                    _owner?.OnTriggerWhilePlaced(pointerTip.position);
                    break;

                case Stage.AwaitingLowest:
                    _lowPoint = pointerTip.position;
                    CurrentStage = Stage.AwaitingHighest;
                    Debug.Log($"[Calibration] lowest key at {_lowPoint}");
                    break;

                case Stage.AwaitingHighest:
                    if (_owner != null)
                    {
                        HandOverCapture(_lowPoint, pointerTip.position);
                        break;
                    }
                    Apply(_lowPoint, pointerTip.position);
                    Save();
                    break;
            }
        }

        /// <summary>Anchored mode's end of capture: the raw layout goes to the
        /// owner, which creates and saves the anchor. Nothing is shown here --
        /// placement becomes valid only once that anchor exists.</summary>
        private void HandOverCapture(Vector3 low, Vector3 high)
        {
            float span = Vector3.Distance(low, high);
            if (span < 0.10f)
            {
                Debug.LogWarning($"[Calibration] the two points are only {span * 100f:0.0} cm apart. " +
                                 "Did you capture the same key twice? Starting over.");
                CurrentStage = Stage.AwaitingLowest;
                return;
            }

            Vector3 head = headTransform != null ? headTransform.position : low + Vector3.back;
            var captured = new PianoKeyLayout(low, high, lowestNote, highestNote, head, whiteKeyDepth);
            if (captured.Warning != null) Debug.LogWarning("[Calibration] " + captured.Warning);
            Debug.Log($"[Calibration] highest key at {high}; white key width {captured.WhiteKeySpacing * 1000f:0.0} mm");

            CurrentStage = Stage.Waiting;
            _owner.OnPointsCaptured(captured);
        }

        [ContextMenu("Recalibrate")]
        public void Recalibrate()
        {
            if (_owner != null)
            {
                // Anchored: the flow decides, and keeps the old anchor until
                // a new one is saved.
                _owner.OnRecalibrateRequested();
                return;
            }

            // Read the nudge before dropping the layout — NudgeMillimetres needs
            // the layout's axes to express it.
            Vector3 keptNudge = NudgeMillimetres;
            Layout = null;
            CurrentStage = Stage.AwaitingLowest;
            Debug.Log($"[Calibration] restarting (keeping nudge {keptNudge} mm)");
            SetPlacementValid(false);
        }

        private void SetPlacementValid(bool valid)
        {
            if (IsPlacementValid == valid) return;
            IsPlacementValid = valid;
            Debug.Log($"[Calibration] placement {(valid ? "valid" : "invalid")}");
            PlacementValidChanged?.Invoke(valid);
        }

        /// <summary>Throw away the fine-adjust and go back to the raw captured
        /// points. Use when the correction is plainly wrong rather than slightly
        /// off — after switching controllers, say.</summary>
        [ContextMenu("Clear nudge")]
        public void ClearNudge() => ClearNudge("requested");

        /// <param name="reason">Goes in the log line, so a nudge that
        /// vanished can be traced to what cleared it.</param>
        public void ClearNudge(string reason)
        {
            if (_owner != null)
            {
                _owner.ClearNudge(reason);
                return;
            }

            // In keyboard-frame millimetres when there is a layout to express
            // them in; mid-recalibration there is none, so world millimetres.
            Vector3 was = Layout != null ? NudgeMillimetres : _nudge * 1000f;
            _nudge = Vector3.zero;
            if (Layout != null) RebuildLayout();
            Save();
            Debug.Log($"[Calibration] nudge cleared ({reason}); was " +
                      $"x {was.x:+0.0;-0.0;0} y {was.y:+0.0;-0.0;0} z {was.z:+0.0;-0.0;0} mm");
        }

        private void Apply(Vector3 low, Vector3 high)
        {
            float span = Vector3.Distance(low, high);
            if (span < 0.10f)
            {
                Debug.LogError($"[Calibration] the two points are only {span * 100f:0.0} cm apart. " +
                               "Did you capture the same key twice? Starting over.");
                Recalibrate();
                return;
            }

            _lowPoint = low;
            _highPoint = high;

            // Yaw trimmed the old points; it means nothing for new ones.
            _yawDegrees = 0f;

            // Deliberately NOT clearing the nudge here. It mostly compensates
            // for the fixed gap between the controller's tracked origin and the
            // plastic tip you actually touch with — a property of the hardware,
            // not of this particular calibration. Keeping it means the next
            // calibration lands correctly straight away instead of making you
            // re-discover the same number. ClearNudge() exists for when it is
            // genuinely wrong.
            RebuildLayout();

            if (Layout.Warning != null) Debug.LogWarning("[Calibration] " + Layout.Warning);

            CurrentStage = Stage.Calibrated;
            Debug.Log($"[Calibration] done — {Layout.KeyCount} keys, " +
                      $"white key width {Layout.WhiteKeySpacing * 1000f:0.0} mm");
        }

        /// <summary>
        /// Shift the whole keyboard, in the keyboard's own frame:
        /// x along the keys (low to high), y up, z toward the player. Metres.
        ///
        /// Cheap enough to call every frame — it rebuilds the layout maths for
        /// 88 keys, which is arithmetic on structs, not GameObjects.
        /// </summary>
        public void Nudge(Vector3 delta)
        {
            if (Layout == null) return;
            if (_owner != null) { _owner.Nudge(delta); return; }

            _nudge += Layout.Right * delta.x
                    + Layout.Up * delta.y
                    + Layout.Forward * delta.z;

            RebuildLayout();
        }

        /// <summary>
        /// Turn the whole keyboard about the vertical axis through its
        /// centre. Vertical only -- a keyboard on a stand does not roll.
        /// Cheap enough to call every frame while the stick is held.
        /// </summary>
        public void AdjustYaw(float deltaDegrees)
        {
            if (Layout == null) return;
            if (_owner != null) { _owner.AdjustYaw(deltaDegrees); return; }
            _yawDegrees = Mathf.Clamp(_yawDegrees + deltaDegrees, -10f, 10f);
            RebuildLayout();
        }

        [ContextMenu("Clear yaw")]
        public void ClearYaw()
        {
            if (_owner != null)
            {
                _owner.AdjustYaw(-_owner.YawDegrees);
                _owner.Persist();
                return;
            }
            _yawDegrees = 0f;
            if (Layout != null) RebuildLayout();
            Save();
        }

        /// <summary>Total correction applied so far, in millimetres, for display.</summary>
        public Vector3 NudgeMillimetres =>
            _owner != null ? _owner.NudgeMillimetres
            : Layout == null ? Vector3.zero
            : new Vector3(Vector3.Dot(_nudge, Layout.Right),
                          Vector3.Dot(_nudge, Layout.Up),
                          Vector3.Dot(_nudge, Layout.Forward)) * 1000f;

        private void RebuildLayout()
        {
            Vector3 head = headTransform != null ? headTransform.position : _lowPoint + Vector3.back;

            // Yaw first, about the captured points' own centre, then the
            // nudge -- so the nudge, which Nudge() builds from the layout's
            // axes, is never itself rotated a second time.
            var turned = AnchoredKeyboard.RotateYaw(new KeyboardGeometry(_lowPoint, _highPoint, Vector3.zero), _yawDegrees);

            Layout = new PianoKeyLayout(turned.LowFront + _nudge, turned.HighFront + _nudge,
                                        lowestNote, highestNote, head, whiteKeyDepth);
            Calibrated?.Invoke(Layout);

            // After Calibrated, so consumers already hold the new layout when
            // they are told they may show it.
            SetPlacementValid(true);
        }

        // ------------------------------------------------------------------
        // Persistence — so you calibrate once, not once per launch
        // ------------------------------------------------------------------

        [Serializable]
        private class SavedCalibration
        {
            public Vector3 low;
            public Vector3 high;
            public Vector3 head;
            public Vector3 nudge;
            public int lowestNote;
            public int highestNote;
            public float whiteKeyDepth;
            // Added with yaw fine-adjust. Absent in older files, which
            // JsonUtility reads as 0 -- exactly "no yaw".
            public float yawDegrees;
        }

        public void Save()
        {
            if (_owner != null)
            {
                // Anchored: the adjustment goes to piano_profiles.json, and
                // the legacy file is left exactly as it was.
                _owner.Persist();
                return;
            }
            if (Layout == null) return;
            var data = new SavedCalibration
            {
                low = _lowPoint,
                high = _highPoint,
                nudge = _nudge,
                head = headTransform != null ? headTransform.position : Vector3.zero,
                lowestNote = lowestNote,
                highestNote = highestNote,
                whiteKeyDepth = whiteKeyDepth,
                yawDegrees = _yawDegrees
            };

            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(data, true));
                Debug.Log($"[Calibration] saved to {FilePath}");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Calibration] could not save: " + e.Message);
            }
        }

        public bool Load()
        {
            try
            {
                if (!File.Exists(FilePath)) return false;
                var data = JsonUtility.FromJson<SavedCalibration>(File.ReadAllText(FilePath));

                lowestNote = data.lowestNote;
                highestNote = data.highestNote;
                // whiteKeyDepth is deliberately not read back: it is a property
                // of the piano, set in the scene, and older files hold the
                // unmeasured 145 mm default.
                _lowPoint = data.low;
                _highPoint = data.high;
                _nudge = data.nudge;
                _yawDegrees = float.IsNaN(data.yawDegrees) ? 0f : Mathf.Clamp(data.yawDegrees, -10f, 10f);

                if (headTransform == null && Camera.main != null) headTransform = Camera.main.transform;
                RebuildLayout();
                CurrentStage = Stage.Calibrated;
                Debug.Log("[Calibration] loaded saved calibration");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Calibration] could not load: " + e.Message);
                return false;
            }
        }

        [ContextMenu("Delete saved calibration")]
        public void DeleteSaved()
        {
            if (File.Exists(FilePath)) File.Delete(FilePath);
            Recalibrate();
        }

        // ------------------------------------------------------------------

        private static readonly string[] Names =
            { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

        private static string NoteName(int note) => Names[note % 12] + (note / 12 - 1);

        private void OnDrawGizmos()
        {
            if (Layout == null) return;
            foreach (var kv in Layout.Keys)
            {
                var key = kv.Value;
                Gizmos.color = key.IsBlack ? Color.black : Color.white;
                Gizmos.matrix = Matrix4x4.TRS(key.Center, key.Rotation, Vector3.one);
                Gizmos.DrawWireCube(Vector3.zero, new Vector3(key.Width, 0.005f, key.Depth));
            }
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
