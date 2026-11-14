using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace PianoGlass.Piano
{
    /// <summary>
    /// #17: asks the headset for a recenter once the saved anchor has loaded
    /// at launch (LaunchResyncPolicy says when), because until a recenter the
    /// anchor and the head are in different frames and the keys show up about
    /// a piano width off. Meta's OVRDisplay.RecenterPose first; if the runtime
    /// reports no recenter within <see cref="WaitSeconds"/>, Unity's
    /// XRInputSubsystem.TryRecenter. Every step is logged as "[LaunchResync]",
    /// so a round shows whether the Quest 3 honours an app-asked recenter.
    ///
    /// Only does anything while the anchored calibration flow is active.
    /// Adds itself after the scene loads; no scene change.
    /// </summary>
    public sealed class LaunchResync : MonoBehaviour
    {
        /// <summary>How long to wait for the runtime to report a recenter.</summary>
        public const float WaitSeconds = 1.5f;

        private readonly LaunchResyncPolicy _policy = new LaunchResyncPolicy();
        private readonly List<XRInputSubsystem> _inputs = new List<XRInputSubsystem>();
        private CalibrationFlowController _flow;
        private AnchoredCalibrationSession _session;
        private float _askedAt = -1f;
        private int _attempt;
        private bool _recentered;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Add()
        {
            if (Application.isEditor) return; // the editor's anchors are fake
            var go = new GameObject(nameof(LaunchResync));
            DontDestroyOnLoad(go);
            go.AddComponent<LaunchResync>();
        }

        private void Start()
        {
            _flow = FindAnyObjectByType<CalibrationFlowController>();
            SubsystemManager.GetSubsystems(_inputs);
            foreach (XRInputSubsystem s in _inputs) s.trackingOriginUpdated += OnOriginUpdated;
            if (OVRManager.display != null) OVRManager.display.RecenteredPose += OnRecentered;
        }

        private void OnDestroy()
        {
            if (_session != null) _session.Transitioned -= OnTransitioned;
            foreach (XRInputSubsystem s in _inputs) s.trackingOriginUpdated -= OnOriginUpdated;
            if (OVRManager.display != null) OVRManager.display.RecenteredPose -= OnRecentered;
        }

        private void Update()
        {
            if (_flow == null || !_flow.Active)
            {
                // Release builds, or no anchored flow: the legacy path keeps its old behaviour.
                if (Time.realtimeSinceStartup > 10f) enabled = false;
                return;
            }

            if (_session == null && _flow.Session != null)
            {
                _session = _flow.Session;
                _session.Transitioned += OnTransitioned;
                // Loaded before this first looked: within seconds of launch, an
                // anchor already showing in Verifying can only be the saved one.
                if (_session.Anchor != null && _session.Flow.State == FlowState.Verifying &&
                    Time.realtimeSinceStartup < 10f)
                    _policy.OnFlowEvent(FlowEvent.LocalizeSucceeded, Time.realtimeSinceStartupAsDouble);
            }

            if (_policy.Due(Time.realtimeSinceStartupAsDouble)) Ask();

            if (_askedAt >= 0f && !_recentered && Time.realtimeSinceStartup - _askedAt > WaitSeconds)
            {
                if (_attempt == 1) Ask();
                else
                {
                    Debug.LogWarning("[LaunchResync] the runtime did not recenter on request; " +
                                     "the keys stay off until the player recenters (hold the Meta button)");
                    _askedAt = -1f;
                }
            }
        }

        private void OnTransitioned(FlowEvent e, FlowTransition t) =>
            _policy.OnFlowEvent(e, Time.realtimeSinceStartupAsDouble);

        private void Ask()
        {
            _attempt++;
            _askedAt = Time.realtimeSinceStartup;
            if (_attempt == 1)
            {
                if (OVRManager.display != null) OVRManager.display.RecenterPose();
                Debug.Log("[LaunchResync] saved anchor loaded: asked Meta for a recenter (OVRDisplay.RecenterPose)");
            }
            else
            {
                bool any = false;
                foreach (XRInputSubsystem s in _inputs) any |= s.TryRecenter();
                Debug.Log($"[LaunchResync] no recenter after {WaitSeconds} s; asked Unity (TryRecenter) = {any}");
            }
        }

        private void OnRecentered() => Note("OVR recentered");
        private void OnOriginUpdated(XRInputSubsystem _) => Note("XR tracking origin updated");

        private void Note(string what)
        {
            if (_askedAt < 0f || _recentered) return;
            _recentered = true;
            Debug.Log($"[LaunchResync] {what} {Time.realtimeSinceStartup - _askedAt:0.00} s after asking " +
                      $"(attempt {_attempt}): the anchor and head are in one frame now");
        }
    }
}
