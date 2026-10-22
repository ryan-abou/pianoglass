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
        [Tooltip("Y position used before calibration has ever produced a layout " +
                 "-- there's no keyboard span to centre on yet, so this only sets " +
                 "height; X/Z and rotation are left at however the Canvas was " +
                 "authored in the scene.")]
        [SerializeField] private float fallbackHeight = 1.2f;

        [Tooltip("When placement becomes invalid (recalibrating), the panel " +
                 "moves this far in front of the head, so the instructions " +
                 "stay readable instead of hanging at the old keyboard's spot.")]
        [SerializeField] private float invalidDistance = 0.8f;
        [Tooltip("...and this far below eye level, in metres.")]
        [SerializeField] private float invalidDrop = 0.15f;
        [Tooltip("While placement is invalid, re-place the panel in front of the " +
                 "head once it is more than this many degrees out of view. Wide " +
                 "enough that reaching for the lowest or highest key does not " +
                 "make it jump; there so a launch facing away from the piano " +
                 "still shows 'localizing' somewhere you can read it.")]
        [SerializeField] private float invalidRefollowDegrees = 70f;

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

        private void OnPlacementValidChanged(bool valid)
        {
            if (valid)
            {
                if (calibration.Layout != null) OnCalibrated(calibration.Layout);
                return;
            }
            PlaceInFrontOfHead();
        }

        /// <summary>
        /// Anchored mode starts every launch with placement invalid (the
        /// anchor is still localizing) and the head not yet tracked, so the
        /// one-off placement below is often behind the player or at the
        /// origin. Re-place only when the panel is well out of view -- never
        /// a panel glued to the head.
        /// </summary>
        private void LateUpdate()
        {
            if (calibration == null || calibration.IsPlacementValid) return;
            Camera head = Camera.main;
            if (head == null) return;

            Vector3 toPanel = transform.position - head.transform.position;
            Vector3 forward = head.transform.forward;
            toPanel.y = 0f;
            forward.y = 0f;
            if (toPanel.sqrMagnitude < 1e-4f || forward.sqrMagnitude < 1e-4f) return;

            if (Vector3.Angle(forward, toPanel) > invalidRefollowDegrees || toPanel.magnitude > 3f)
                PlaceInFrontOfHead();
        }

        /// <summary>Placed once at the moment placement is lost (and again by
        /// LateUpdate only if it ends up far out of view), not every frame: a
        /// panel glued to the head is uncomfortable, and during a
        /// recalibration the player is facing the piano anyway.</summary>
        private void PlaceInFrontOfHead()
        {
            Camera head = Camera.main;
            if (head == null)
            {
                ApplyFallbackHeight();
                return;
            }

            Vector3 forward = head.transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f) forward = Vector3.forward;
            forward.Normalize();

            Vector3 pos = head.transform.position + forward * invalidDistance + Vector3.down * invalidDrop;

            // Local +Z away from the player, the same convention OnCalibrated uses.
            transform.SetPositionAndRotation(pos, Quaternion.LookRotation(forward, Vector3.up));
        }

        private void ApplyFallbackHeight()
        {
            Vector3 p = transform.position;
            p.y = fallbackHeight;
            transform.position = p;
        }

        private void OnCalibrated(PianoKeyLayout layout)
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
            Vector3 pos = wallDepth + layout.Up * heightAboveKeys;

            // Local -Z toward the player, matching the original hand-placed
            // Canvas (identity rotation, camera looking down +Z at it from
            // behind): local +Z is set to -Forward, so local -Z is +Forward,
            // which is defined as pointing at the player.
            Quaternion rotation = Quaternion.LookRotation(-layout.Forward, layout.Up);

            transform.SetPositionAndRotation(pos, rotation);
        }
    }
}
