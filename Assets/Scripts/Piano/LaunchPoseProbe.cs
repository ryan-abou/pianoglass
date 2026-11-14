using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace PianoGlass.Piano
{
    /// <summary>
    /// Development builds only, for #17: at launch the keys start about a piano
    /// width to the right until a recenter, yet at that recenter the anchor's
    /// world pose moves only ~130 mm. So before the recenter the head and the
    /// anchor disagree by about a metre. This logs where the head, the rig's
    /// tracking space and the anchor are -- in the world, in the rig, and as
    /// seen from the head -- plus the tracking origin each side thinks it uses:
    /// at fixed times after launch, and before and after every recenter,
    /// tracking-origin change, anchor jump and placement change.
    ///
    /// Adds itself after the scene loads; no scene change. Lines start with
    /// "[LaunchPose]".
    /// </summary>
    public sealed class LaunchPoseProbe : MonoBehaviour
    {
        private static readonly float[] LaunchMarks = { 0.5f, 1f, 2f, 3f, 5f, 8f, 12f, 20f, 30f, 45f, 60f };

        /// <summary>A world-pose change bigger than this in one frame is a
        /// re-base (recenter or relocalization), not the runtime refining.</summary>
        private const float JumpMetres = 0.05f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AddInDevelopmentBuilds()
        {
            // The editor's anchors are fake; only a headset says anything here.
            if (!Debug.isDebugBuild || Application.isEditor) return;
            var go = new GameObject(nameof(LaunchPoseProbe));
            DontDestroyOnLoad(go);
            go.AddComponent<LaunchPoseProbe>();
        }

        private struct Snapshot
        {
            public bool HasHead, HasRig, HasAnchor, AnchorTracked, HasLayout;
            public Pose Head, Rig, Anchor;
            public Vector3 A0, C8;
        }

        private readonly List<XRInputSubsystem> _inputs = new List<XRInputSubsystem>();
        private PianoAnchorManager _anchors;
        private KeyboardCalibration _calibration;
        private float _start;
        private int _nextMark;
        private float _followUpAt = -1f;
        private string _followUpWhy;
        private IPianoAnchorHandle _lastBound;
        private bool _lastValid;
        private Snapshot _previous;
        private bool _hasPrevious;

        private void Start()
        {
            _start = Time.realtimeSinceStartup;
            SubsystemManager.GetSubsystems(_inputs);
            foreach (XRInputSubsystem s in _inputs) s.trackingOriginUpdated += OnOriginUpdated;
            if (OVRManager.display != null) OVRManager.display.RecenteredPose += OnRecentered;
            Debug.Log($"[LaunchPose] probe on: {_inputs.Count} XR input subsystem(s), {Origins()}");
        }

        private void OnDestroy()
        {
            foreach (XRInputSubsystem s in _inputs) s.trackingOriginUpdated -= OnOriginUpdated;
            if (OVRManager.display != null) OVRManager.display.RecenteredPose -= OnRecentered;
        }

        private void OnOriginUpdated(XRInputSubsystem _) => Event("XR tracking origin updated", Take());
        private void OnRecentered() => Event("OVR recentered", Take());

        private void LateUpdate()
        {
            if (_anchors == null) _anchors = FindAnyObjectByType<PianoAnchorManager>();
            if (_calibration == null) _calibration = FindAnyObjectByType<KeyboardCalibration>();

            Snapshot now = Take();
            float t = Time.realtimeSinceStartup - _start;

            if (_nextMark < LaunchMarks.Length && t >= LaunchMarks[_nextMark])
            {
                Log($"launch +{LaunchMarks[_nextMark]:0.#} s", now);
                _nextMark++;
            }

            IPianoAnchorHandle bound = _anchors != null ? _anchors.BoundAnchor : null;
            if (bound != _lastBound)
            {
                _lastBound = bound;
                Event(bound == null ? "anchor unbound" : "anchor bound", now);
            }

            bool valid = _calibration != null && _calibration.IsPlacementValid;
            if (valid != _lastValid)
            {
                _lastValid = valid;
                Event("placement valid=" + valid, now);
            }

            if (_hasPrevious && (Jumped(_previous.HasHead, _previous.Head, now.HasHead, now.Head, false) ||
                                 Jumped(_previous.HasRig, _previous.Rig, now.HasRig, now.Rig, false) ||
                                 Jumped(_previous.HasAnchor, _previous.Anchor, now.HasAnchor, now.Anchor, true)))
            {
                Log("jump, frame before", _previous);
                Event("jump, frame after", now);
            }

            if (_followUpAt > 0f && Time.realtimeSinceStartup >= _followUpAt)
            {
                _followUpAt = -1f;
                Log(_followUpWhy, now);
            }

            _previous = now;
            _hasPrevious = true;
        }

        /// <summary>The head moves on its own, so only a rig or anchor jump, or
        /// a head move far beyond what a frame of real motion allows, counts.</summary>
        private static bool Jumped(bool hadBefore, Pose before, bool hasNow, Pose now, bool anchor)
        {
            if (!hadBefore || !hasNow) return false;
            float limit = anchor ? JumpMetres : JumpMetres * 4f;
            return (now.position - before.position).magnitude > limit;
        }

        private void Event(string why, Snapshot s)
        {
            Log(why, s);
            _followUpAt = Time.realtimeSinceStartup + 1f;
            _followUpWhy = why + ", 1 s later";
        }

        private Snapshot Take()
        {
            var s = new Snapshot();
            Camera cam = Camera.main;
            if (cam != null)
            {
                s.HasHead = true;
                s.Head = new Pose(cam.transform.position, cam.transform.rotation);
                Transform rig = cam.transform.parent;
                if (rig != null)
                {
                    s.HasRig = true;
                    s.Rig = new Pose(rig.position, rig.rotation);
                }
            }

            IPianoAnchorHandle anchor = _anchors != null ? _anchors.BoundAnchor : null;
            if (anchor != null && anchor.IsAlive)
            {
                s.HasAnchor = true;
                s.Anchor = anchor.Pose;
                s.AnchorTracked = anchor.IsTracked;
            }

            PianoKeyLayout layout = _calibration != null ? _calibration.Layout : null;
            if (layout != null)
            {
                s.HasLayout = true;
                s.A0 = layout.FrontEdgeCenter(layout.LowestNote);
                s.C8 = layout.FrontEdgeCenter(layout.HighestNote);
            }
            return s;
        }

        private void Log(string why, Snapshot s)
        {
            float t = Time.realtimeSinceStartup - _start;
            string head = s.HasHead ? P(s.Head) : "none";
            string rig = s.HasRig ? P(s.Rig) : "none";
            string anchor = "none";
            if (s.HasAnchor)
            {
                anchor = $"{P(s.Anchor)} tracked={s.AnchorTracked}";
                if (s.HasRig) anchor += $" inRig {P(Relative(s.Rig, s.Anchor))}";
                if (s.HasHead) anchor += $" fromHead {P(Relative(s.Head, s.Anchor))}";
            }
            string keys = "none";
            if (s.HasLayout)
            {
                keys = $"A0 {V(s.A0)} C8 {V(s.C8)}";
                if (s.HasHead) keys += $" A0 fromHead {V(Quaternion.Inverse(s.Head.rotation) * (s.A0 - s.Head.position))}";
            }
            Debug.Log($"[LaunchPose] {why} t={t:0.00}s | head {head} | rig {rig} | anchor {anchor} | keys {keys} | {Origins()}");
        }

        /// <summary><paramref name="child"/> in <paramref name="parent"/>'s frame.</summary>
        private static Pose Relative(Pose parent, Pose child)
        {
            Quaternion inv = Quaternion.Inverse(parent.rotation);
            return new Pose(inv * (child.position - parent.position), inv * child.rotation);
        }

        private string Origins()
        {
            string xr = _inputs.Count > 0 ? _inputs[0].GetTrackingOriginMode().ToString() : "no subsystem";
            string ovr = OVRManager.instance != null ? OVRManager.instance.trackingOriginType.ToString() : "no OVRManager";
            return $"origin XR={xr} OVR={ovr}";
        }

        private static string V(Vector3 v) => $"({v.x:+0.000;-0.000}, {v.y:+0.000;-0.000}, {v.z:+0.000;-0.000})";

        private static string P(Pose p) => $"{V(p.position)} yaw {p.rotation.eulerAngles.y:0.0}";
    }
}
