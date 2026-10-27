using UnityEngine;

namespace PianoGlass.Piano
{
    /// <summary>
    /// Meta-specific input for calibration.
    ///
    ///   Right trigger        capture the next point; during a Middle C fix,
    ///                        the Middle C touch
    ///   A                    anchored mode, Verifying: "looks right" (the
    ///                        same press does not reach the transport)
    ///   Right grip           anchored mode: start / cancel a Middle C fix
    ///   B                    start calibration over
    ///   Left trigger         start calibration over
    ///   Right thumbstick     slide the keyboard left/right and nearer/further
    ///   Left thumbstick up   raise or lower the keyboard
    ///   Left thumbstick L/R  turn the keyboard about its centre (yaw)
    ///   Left menu button     measure the controller tip (ControllerTipCalibration);
    ///                        press again to cancel
    ///
    /// The left stick is axis-locked to whichever of up/down or left/right
    /// is pushed further, so a slightly diagonal push turns *or* raises,
    /// never both.
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

        [Tooltip("Degrees per second of yaw at full left-stick deflection. " +
                 "Slow on purpose: half a degree is already ~5 mm at each end " +
                 "of an 88-key keyboard.")]
        [SerializeField] private float yawSpeed = 0.5f;

        [Tooltip("Ignore tiny stick drift below this magnitude.")]
        [SerializeField] private float stickDeadzone = 0.15f;

        [SerializeField] private bool logInput = false;

        private KeyboardCalibration _calibration;
        private ControllerTipCalibration _tip;
        private bool _nudgedSinceSave;

        private void Awake()
        {
            _calibration = GetComponent<KeyboardCalibration>();
            _tip = GetComponent<ControllerTipCalibration>();
        }

        private void Update()
        {
            if (_calibration == null) return;

            if (_tip != null && OVRInput.GetDown(OVRInput.Button.Start, OVRInput.Controller.LTouch))
            {
                if (logInput) Debug.Log("[Calibration] left menu — tip calibration");
                _tip.BeginOrCancel();
                return;
            }

            // While the tip is being measured the right controller is held
            // against a key and rocked; a stray trigger pull must not
            // capture a point, nor a stick knock nudge the keyboard.
            if (_tip != null && _tip.IsBusy) return;

            if (OVRInput.GetDown(OVRInput.Button.PrimaryIndexTrigger, OVRInput.Controller.RTouch))
            {
                if (logInput) Debug.Log("[Calibration] right trigger — capture");
                _calibration.CapturePoint();
                return;
            }

            // Only acts while the placement is waiting to be confirmed;
            // otherwise A is OvrSongTransport's play/pause.
            if (_calibration.AwaitingConfirmation &&
                OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch))
            {
                if (logInput) Debug.Log("[Calibration] A — looks right");
                _calibration.Confirm();
                return;
            }

            if (OVRInput.GetDown(OVRInput.Button.PrimaryHandTrigger, OVRInput.Controller.RTouch))
            {
                if (logInput) Debug.Log("[Calibration] right grip — Middle C fix");
                _calibration.ToggleCorrection();
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

            // Axis-lock the left stick: height or yaw, whichever dominates.
            if (Mathf.Abs(left.x) > Mathf.Abs(left.y)) left.y = 0f; else left.x = 0f;
            if (Mathf.Abs(left.y) < stickDeadzone) left.y = 0f;
            if (Mathf.Abs(left.x) < stickDeadzone) left.x = 0f;

            if (right == Vector2.zero && left == Vector2.zero)
            {
                // Write the file once, when the sticks come to rest, rather than
                // every frame while adjusting.
                if (_nudgedSinceSave)
                {
                    _calibration.Save();
                    _nudgedSinceSave = false;
                    Debug.Log($"[Calibration] saved nudge {_calibration.NudgeMillimetres} mm, " +
                              $"yaw {_calibration.YawDegrees:+0.00;-0.00;0} deg");
                }
                return;
            }

            if (right != Vector2.zero || left.y != 0f)
            {
                // x: along the keys.  y: up.  z: toward the player.
                Vector3 delta = new Vector3(right.x, left.y, right.y) * (nudgeSpeed * Time.deltaTime);
                _calibration.Nudge(delta);
            }

            // Stick right turns the keyboard clockwise seen from above.
            if (left.x != 0f)
                _calibration.AdjustYaw(left.x * yawSpeed * Time.deltaTime);

            _nudgedSinceSave = true;
        }
    }
}
