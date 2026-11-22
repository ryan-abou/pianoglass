using UnityEngine;

namespace PianoGlass.Piano
{
    /// <summary>
    /// Keeps the status Canvas registered on the calibrated keyboard instead
    /// of sitting wherever it was authored in the scene. A fixed world
    /// position is wrong twice over: y = 0 is the floor in Quest tracking
    /// space (unreadably low), and even at the right height it lands
    /// somewhere different relative to the piano every time the headset is
    /// set up.
    ///
    /// Driven by KeyboardCalibration.Calibrated, the same event NoteHighway
    /// listens to, and the same layout basis (Right / Up / Forward) -- so a
    /// recalibration or a nudge moves the panel exactly as it moves the wall,
    /// with no separate tracking of its own to fall out of sync.
    /// </summary>
    public class CanvasCalibrationFollower : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private KeyboardCalibration calibration;

        [Header("Placement")]
        [Tooltip("Standoff behind the keys' far edge, in metres. Its own number, " +
                 "not NoteHighway's: the wall moved 40 mm toward the player and " +
                 "the panel deliberately did not, so the two no longer share one.")]
        [SerializeField] private float depthOffset = 0.05f;
        [Tooltip("Height above the key plane once calibrated, in metres. 0.65 m " +
                 "clears the top of the default 0.5 m note wall.")]
        [SerializeField] private float heightAboveKeys = 0.65f;
        [Tooltip("Degrees the panel tips its top toward the player. 0 is upright.")]
        [SerializeField] private float tiltDegrees = 0f;
        [Tooltip("Y position used before calibration has ever produced a layout " +
                 "-- there's no keyboard span to centre on yet, so this only sets " +
                 "height; X/Z and rotation are left at however the Canvas was " +
                 "authored in the scene.")]
        [SerializeField] private float fallbackHeight = 1.2f;

        /// <summary>Metres to the right of the keyboard's centre. 0 for the
        /// usual spot; the status strip (#37) moves the old status panel, now
        /// development-only test info, off to the side so the two don't overlap.</summary>
        public float ExtraRight { get; set; }

        private PianoGlass.UI.GrabbablePanel _grab;

        private void OnEnable()
        {
            ApplyFallbackHeight();

            if (calibration == null) return;
            calibration.Calibrated += OnCalibrated;
            calibration.PlacementValidChanged += OnPlacementValidChanged;
            if (calibration.IsPlacementValid) OnCalibrated(calibration.Layout);
        }

        private void OnDisable()
        {
            if (calibration == null) return;
            calibration.Calibrated -= OnCalibrated;
            calibration.PlacementValidChanged -= OnPlacementValidChanged;
        }

        /// <summary>Back to its usual spot on the keys now ("Reset panels", #5).</summary>
        public void PlaceNow()
        {
            if (calibration != null && calibration.IsPlacementValid && calibration.Layout != null)
                Place(calibration.Layout);
        }

        /// <summary>
        /// Losing the keys leaves the panel where it is. It used to jump in
        /// front of the head, and follow it whenever it was 70 degrees out of
        /// view, until the keys came back; in round 11 (2026-11-21) that read
        /// as the panel moving around by itself when the head tilted. Ryan:
        /// it shouldn't move by itself. The status strip (#37) shows the
        /// setup steps instead.
        /// </summary>
        private void OnPlacementValidChanged(bool valid)
        {
            if (valid && calibration.Layout != null) OnCalibrated(calibration.Layout);
        }

        private void ApplyFallbackHeight()
        {
            Vector3 p = transform.position;
            p.y = fallbackHeight;
            transform.position = p;
        }

        /// <summary>A spot of its own (moved by hand) wins: GrabbablePanel puts it there.</summary>
        private void OnCalibrated(PianoKeyLayout layout)
        {
            if (_grab == null) _grab = GetComponent<PianoGlass.UI.GrabbablePanel>();
            if (_grab != null && _grab.KeepsItsOwnSpot) return;
            Place(layout);
        }

        private void Place(PianoKeyLayout layout)
        {
            if (layout == null) return;
            if (!layout.TryGetKey(layout.LowestNote, out PianoKey low) ||
                !layout.TryGetKey(layout.HighestNote, out PianoKey high))
                return;

            // Key.Center is already at the mid-depth of each key's playing
            // surface, so the midpoint between the two ends is the centre of
            // the keyboard's span at that same mid-depth -- the same starting
            // point NoteHighway steps back from to find the wall's far edge.
            Vector3 spanCentre = (low.Center + high.Center) * 0.5f;
            float depthBack = layout.WhiteKeyDepth * 0.5f + depthOffset;
            Vector3 wallDepth = spanCentre - layout.Forward * depthBack;
            Vector3 pos = wallDepth + layout.Up * heightAboveKeys + layout.Right * ExtraRight;

            // Local -Z toward the player, matching the original hand-placed
            // Canvas (identity rotation, camera looking down +Z at it from
            // behind): local +Z is set to -Forward, so local -Z is +Forward,
            // which is defined as pointing at the player.
            float rad = tiltDegrees * Mathf.Deg2Rad;
            Vector3 up = layout.Up * Mathf.Cos(rad) + layout.Forward * Mathf.Sin(rad);
            Vector3 towardPlayer = layout.Forward * Mathf.Cos(rad) - layout.Up * Mathf.Sin(rad);
            Quaternion rotation = Quaternion.LookRotation(-towardPlayer, up);

            transform.SetPositionAndRotation(pos, rotation);
        }
    }
}
