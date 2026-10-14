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
        public enum Stage { Idle, AwaitingLowest, AwaitingHighest, Calibrated }

        [Header("Keyboard")]
        [Tooltip("Number of keys on your keyboard. Sets the note range below.")]
        [SerializeField] private int keyCount = 61;
        [SerializeField] private int lowestNote = 36;    // C2 on a 61-key
        [SerializeField] private int highestNote = 96;   // C7 on a 61-key
        [Tooltip("Front-to-back length of a white key, in metres. 145 mm is typical.")]
        [SerializeField] private float whiteKeyDepth = 0.145f;

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
            Stage.Calibrated      => "Calibrated.  sticks = nudge   left stick L/R = yaw   B = redo   stick click = clear nudge",
            _                     => "Press the trigger to start calibration"
        };

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

        public float YawDegrees => _yawDegrees;

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

            if (loadOnStart && Load()) return;
            CurrentStage = Stage.AwaitingLowest;
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

                case Stage.AwaitingLowest:
                    _lowPoint = pointerTip.position;
                    CurrentStage = Stage.AwaitingHighest;
                    Debug.Log($"[Calibration] lowest key at {_lowPoint}");
                    break;

                case Stage.AwaitingHighest:
                    Apply(_lowPoint, pointerTip.position);
                    Save();
                    break;
            }
        }

        [ContextMenu("Recalibrate")]
        public void Recalibrate()
        {
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
        public void ClearNudge()
        {
            _nudge = Vector3.zero;
            if (Layout != null) RebuildLayout();
            Save();
            Debug.Log("[Calibration] nudge cleared");
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
            _yawDegrees = Mathf.Clamp(_yawDegrees + deltaDegrees, -10f, 10f);
            RebuildLayout();
        }

        [ContextMenu("Clear yaw")]
        public void ClearYaw()
        {
            _yawDegrees = 0f;
            if (Layout != null) RebuildLayout();
            Save();
        }

        /// <summary>Total correction applied so far, in millimetres, for display.</summary>
        public Vector3 NudgeMillimetres => Layout == null
            ? Vector3.zero
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
                whiteKeyDepth = data.whiteKeyDepth;
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
