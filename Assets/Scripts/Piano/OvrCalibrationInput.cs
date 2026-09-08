using UnityEngine;

namespace PianoGlass.Piano
{
    /// <summary>
    /// Meta-specific input for calibration.
    ///
    ///   Right trigger        capture the next point
    ///   B                    start calibration over
    ///   Left trigger         start calibration over
    ///   Right thumbstick     slide the keyboard left/right and nearer/further
    ///   Left thumbstick up   raise or lower the keyboard
    ///
    /// The thumbstick nudging exists because two-point calibration gets the
    /// *scale* right immediately — spacing comes from the distance between the
    /// points — but inherits a constant offset from wherever the controller
    /// anchor sits relative to the plastic tip you actually touched with. That
    /// offset shifts the whole keyboard without distorting it, so it can be
    /// dialled out by eye in a few seconds, in the headset, at the piano.
    ///
    /// Fixing it by editing a PointerTip transform in Unity would mean a build
    /// and a walk back to the keyboard for every 2 mm.
    ///
    /// Note the explicit OVRInput.Controller arguments. Without them,
    /// PrimaryIndexTrigger resolves against whichever controller is "active"
    /// and SecondaryIndexTrigger may resolve to nothing, so two-hand chords
    /// silently never fire.
    /// </summary>
    [RequireComponent(typeof(KeyboardCalibration))]
    public class OvrCalibrationInput : MonoBehaviour
    {
        [Tooltip("Metres per second of nudge at full stick deflection. 2 cm/s " +
                 "is slow enough to land on a millimetre.")]
        [SerializeField] private float nudgeSpeed = 0.02f;

        [Tooltip("Ignore tiny stick drift below this magnitude.")]
        [SerializeField] private float stickDeadzone = 0.15f;

        [SerializeField] private bool logInput = false;

        private KeyboardCalibration _calibration;
        private bool _nudgedSinceSave;

        private void Awake() => _calibration = GetComponent<KeyboardCalibration>();

        private void Update()
        {
            if (_calibration == null) return;

            if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
            {
                if (logInput) Debug.Log("[Calibration] right trigger — capture");
                _calibration.CapturePoint();
                return;
            }

            // Start over. B is the primary way; the left trigger is a spare for
            // when the controller is balanced against a key and B is awkward.
            if (OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.RTouch) ||
                OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.LTouch))
            {
                if (logInput) Debug.Log("[Calibration] recalibrate");
                _calibration.Recalibrate();
                return;
            }

            // Clear the fine-adjust. Separate from recalibrating, because the
            // nudge now survives a recalibration on purpose.
            if (OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick, OVRInput.Controller.RTouch))
            {
                if (logInput) Debug.Log("[Calibration] stick click — clear nudge");
                _calibration.ClearNudge();
                return;
            }

            if (_calibration.IsCalibrated) HandleNudge();
        }

        private void HandleNudge()
        {
            Vector2 right = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch);
            Vector2 left = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LTouch);

            if (right.magnitude < stickDeadzone) right = Vector2.zero;
            if (Mathf.Abs(left.y) < stickDeadzone) left.y = 0f;

            if (right == Vector2.zero && Mathf.Approximately(left.y, 0f))
            {
                // Write the file once, when the sticks come to rest, rather than
                // every frame while adjusting.
                if (_nudgedSinceSave)
                {
                    _calibration.Save();
                    _nudgedSinceSave = false;
                    if (logInput) Debug.Log($"[Calibration] saved nudge {_calibration.NudgeMillimetres}");
                }
                return;
            }

            // x: along the keys.  y: up.  z: toward the player.
            Vector3 delta = new Vector3(right.x, left.y, right.y) * (nudgeSpeed * Time.deltaTime);
            _calibration.Nudge(delta);
            _nudgedSinceSave = true;
        }
    }
}
