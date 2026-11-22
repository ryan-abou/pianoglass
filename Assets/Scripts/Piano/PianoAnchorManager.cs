using UnityEngine;

namespace PianoGlass.Piano
{
    /// <summary>
    /// Keeps the overlays on the anchor. Holds the anchor the session has
    /// bound, reads its world pose once a frame, and republishes the layout
    /// into KeyboardCalibration whenever that pose has moved more than
    /// ~1 mm / 0.1° from the pose the current layout was built from -- a
    /// recenter, or the runtime refining its estimate -- or the geometry or
    /// nudge changed. It also sets KeyboardCalibration.IsPlacementValid from
    /// the session every frame, which is what hides every overlay while the
    /// anchor is localizing or untracked.
    ///
    /// LateUpdate, and ordered after OVRSpatialAnchor (default order 0),
    /// which writes the runtime's pose into its transform in Update: by the
    /// time this reads it, it is this frame's.
    ///
    /// Meta-free: the anchor is an IPianoAnchorHandle.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class PianoAnchorManager : MonoBehaviour
    {
        [Tooltip("A per-frame jump bigger than this is logged as a tracking-space " +
                 "change (recenter or relocalization). Logging only.")]
        [SerializeField] private float jumpMetres = 0.05f;
        [SerializeField] private float jumpDegrees = 2f;

        private AnchoredCalibrationSession _session;
        private KeyboardCalibration _calibration;

        private IPianoAnchorHandle _bound;
        private Pose _builtFrom;
        private Pose _lastFrame;
        private bool _dirty;
        private bool _lastTracked;
        private Pose _lastLogged;

        /// <summary>Slow movement adds up to this before it is logged.</summary>
        private const float RefinedMetres = 0.005f;

        /// <summary>After a jump, the keys are checked this much later, once
        /// the runtime has settled (#38).</summary>
        private const float CheckDelaySeconds = 1f;
        private float _checkAt = -1f;

        public IPianoAnchorHandle BoundAnchor => _bound;

        /// <summary>Set when a recenter or relocalization left the keys
        /// somewhere no piano can be (KeyPlacementCheck): what to tell the
        /// player. Null otherwise, and cleared by a new anchor.</summary>
        public string PlacementWarning { get; private set; }

        public const string LookWrongLine = "The keys look wrong. Press B to set up again.";

        /// <summary>The player asked to set up again: the warning has done its job.</summary>
        public void ClearPlacementWarning() => PlacementWarning = null;

        public void Bind(AnchoredCalibrationSession session, KeyboardCalibration calibration)
        {
            if (_session != null) _session.PlacementChanged -= MarkDirty;
            _session = session;
            _calibration = calibration;
            _session.PlacementChanged += MarkDirty;
            _dirty = true;
        }

        private void OnDestroy()
        {
            if (_session != null) _session.PlacementChanged -= MarkDirty;
        }

        private void MarkDirty() => _dirty = true;

        private void LateUpdate()
        {
            if (_session == null || _calibration == null) return;

            IPianoAnchorHandle anchor = _session.Anchor;
            if (anchor != _bound)
            {
                _bound = anchor;
                _dirty = true;
                _checkAt = -1f;
                if (PlacementWarning != null) Debug.Log("[AnchorFlow] new anchor: keys warning cleared");
                PlacementWarning = null;
                if (anchor != null)
                {
                    _lastFrame = anchor.Pose;
                    _lastLogged = _lastFrame;
                    _lastTracked = anchor.IsTracked;
                }
            }

            if (_bound != null && _bound.IsAlive)
            {
                Pose pose = _bound.Pose;
                LogJumps(pose);

                if (_dirty || AnchoredKeyboard.PoseMoved(_builtFrom, pose))
                {
                    _calibration.PublishExternalLayout(_session.BuildLayout());
                    _builtFrom = pose;
                    _dirty = false;
                }
            }

            _calibration.SetExternalPlacementValid(_session.IsOverlayVisible);

            if (_checkAt > 0f && Time.realtimeSinceStartup >= _checkAt && _session.IsOverlayVisible)
            {
                _checkAt = -1f;
                CheckPlacement();
            }
        }

        private void CheckPlacement()
        {
            PianoKeyLayout layout = _calibration.Layout;
            Camera head = Camera.main;
            if (layout == null || head == null) return;
            Vector3 centre = (layout.LowFrontEdge + layout.HighFrontEdge) * 0.5f;
            bool wrong = KeyPlacementCheck.LooksWrong(centre, layout.Forward, layout.Up, head.transform.position,
                                                      out string measured);
            Debug.Log($"[AnchorFlow] keys after the jump: {measured}: {(wrong ? "LOOK WRONG, asking to set up again" : "fine")}");
            PlacementWarning = wrong ? LookWrongLine : null;
        }

        private void LogJumps(Pose pose)
        {
            float moved = (pose.position - _lastFrame.position).magnitude;
            float turned = Quaternion.Angle(pose.rotation, _lastFrame.rotation);
            if (moved > jumpMetres || turned > jumpDegrees)
            {
                Debug.Log($"[AnchorFlow] anchor world pose jumped {moved * 1000f:0} mm / {turned:0.0} deg in one frame " +
                          "(tracking space re-based -- recenter or relocalization); layout rebuilt");
                _checkAt = Time.realtimeSinceStartup + CheckDelaySeconds;
                _lastLogged = pose;
            }
            else
            {
                // Whether the runtime keeps refining it after a launch: a jump
                // above would hide this, and the keys follow it either way.
                float crept = (pose.position - _lastLogged.position).magnitude;
                if (crept >= RefinedMetres)
                {
                    Debug.Log($"[AnchorFlow] anchor refined {crept * 1000f:0.0} mm at t={Time.realtimeSinceStartup:0.0}s");
                    _lastLogged = pose;
                }
            }

            bool tracked = _bound.IsTracked;
            if (tracked != _lastTracked) Debug.Log($"[AnchorFlow] anchor tracked={tracked}");

            _lastFrame = pose;
            _lastTracked = tracked;
        }
    }
}
