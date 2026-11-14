using UnityEngine;

namespace PianoGlass.Piano
{
    /// <summary>
    /// #17: after the saved anchor loads at launch, asks the player to recenter
    /// (hold the Meta button) until they do, because until a recenter the
    /// anchor and the head are in different frames and the keys show up about
    /// a piano width off. LaunchRecenterPrompt decides; this feeds it the head
    /// pose at the load and every frame's head step, and CalibrationPanel shows
    /// <see cref="PromptLine"/> while <see cref="WaitingForRecenter"/>.
    ///
    /// The app cannot recenter by itself: on the Quest 3, OVRDisplay
    /// .RecenterPose fires the recentered events but moves nothing (round 3,
    /// 2026-11-14), so it is not called. Logs as "[LaunchResync]".
    ///
    /// Only while the anchored calibration flow is active. Adds itself after
    /// the scene loads; no scene change.
    /// </summary>
    public sealed class LaunchResync : MonoBehaviour
    {
        public const string PromptLine = "Hold the Meta button to line up the keys";

        /// <summary>Show <see cref="PromptLine"/> now.</summary>
        public static bool WaitingForRecenter { get; private set; }

        private readonly LaunchRecenterPrompt _prompt = new LaunchRecenterPrompt();
        private CalibrationFlowController _flow;
        private AnchoredCalibrationSession _session;
        private Transform _head;
        private Pose _lastHead;
        private bool _hasLastHead;
        private float _askedAt;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Add()
        {
            WaitingForRecenter = false;
            if (Application.isEditor) return; // the editor's anchors are fake
            var go = new GameObject(nameof(LaunchResync));
            DontDestroyOnLoad(go);
            go.AddComponent<LaunchResync>();
        }

        private void Start() => _flow = FindAnyObjectByType<CalibrationFlowController>();

        private void OnDestroy()
        {
            if (_session != null) _session.Transitioned -= OnTransitioned;
            WaitingForRecenter = false;
        }

        private void LateUpdate()
        {
            if (_flow == null || !_flow.Active)
            {
                // Release builds, or no anchored flow: the legacy path keeps its old behaviour.
                if (Time.realtimeSinceStartup > 10f) enabled = false;
                return;
            }

            if (_head == null && Camera.main != null) _head = Camera.main.transform;
            if (_head == null) return;
            var head = new Pose(_head.localPosition, _head.localRotation); // tracking space

            if (_session == null && _flow.Session != null)
            {
                _session = _flow.Session;
                _session.Transitioned += OnTransitioned;
                // Loaded before this first looked: within seconds of launch, an
                // anchor already showing in Verifying can only be the saved one.
                if (_session.Anchor != null && _session.Flow.State == FlowState.Verifying &&
                    Time.realtimeSinceStartup < 10f)
                    OnLaunchLoad(head);
            }

            if (_hasLastHead && WaitingForRecenter &&
                _prompt.OnHeadStep((head.position - _lastHead.position).magnitude,
                                   Mathf.DeltaAngle(_lastHead.rotation.eulerAngles.y, head.rotation.eulerAngles.y)))
            {
                WaitingForRecenter = false;
                Debug.Log($"[LaunchResync] recentered {Time.realtimeSinceStartup - _askedAt:0.0} s after asking: " +
                          "prompt cleared, the keys line up now");
            }

            _lastHead = head;
            _hasLastHead = true;
        }

        private void OnTransitioned(FlowEvent e, FlowTransition t)
        {
            if (e == FlowEvent.LocalizeSucceeded && _head != null)
                OnLaunchLoad(new Pose(_head.localPosition, _head.localRotation));
        }

        private void OnLaunchLoad(Pose head)
        {
            _prompt.OnLaunchLoad(head.position.x, head.position.z, head.rotation.eulerAngles.y);
            if (!_prompt.Waiting || WaitingForRecenter) return;
            WaitingForRecenter = true;
            _askedAt = Time.realtimeSinceStartup;
            Debug.Log($"[LaunchResync] saved anchor loaded with the head " +
                      $"{new Vector2(head.position.x, head.position.z).magnitude:0.00} m / " +
                      $"{head.rotation.eulerAngles.y:0} deg from the origin: asking the player to recenter");
        }
    }
}
