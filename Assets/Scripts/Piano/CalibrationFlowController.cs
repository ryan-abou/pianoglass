using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace PianoGlass.Piano
{
    /// <summary>
    /// Anchored calibration's Unity shell: builds an AnchoredCalibrationSession
    /// (which drives the Core CalibrationFlow) and connects it to
    /// KeyboardCalibration as its placement owner. All the decisions are in
    /// the session and the flow; this only forwards input, starts the async
    /// work, and reports.
    ///
    /// Behind useAnchoredCalibration, which only takes effect in development
    /// builds (and the editor). In a release build this disables itself in
    /// Awake, before KeyboardCalibration.Start, and calibration behaves
    /// exactly as it did before Phase 4 -- keyboard_calibration.json and all.
    ///
    /// Meta-free. On the headset the real anchor service arrives through
    /// ServiceFactory, set by OvrAnchorServiceRegistration; in the editor it
    /// is EditorPianoAnchorService, which forgets its anchors when play mode
    /// stops (so each editor run sees the saved anchor as gone -- NotFound --
    /// and asks for a fresh calibration).
    ///
    /// Every log line starts with "[AnchorFlow]".
    /// </summary>
    public class CalibrationFlowController : MonoBehaviour, IKeyboardPlacementOwner
    {
        /// <summary>Set before the scene loads by OvrAnchorServiceRegistration
        /// on device. Null in the editor.</summary>
        public static Func<IPianoAnchorService> ServiceFactory;

        [SerializeField] private KeyboardCalibration calibration;
        [SerializeField] private PianoAnchorManager anchorManager;

        [Tooltip("Place the keyboard from a spatial anchor saved at calibration " +
                 "time instead of from tracking-space points. Honoured in " +
                 "development builds and the editor only; release builds always " +
                 "use the legacy calibration.")]
        [SerializeField] private bool useAnchoredCalibration = true;

        [SerializeField] private double localizeTimeoutSeconds = 10;
        [SerializeField] private int maxLocalizeAttempts = CalibrationFlow.DefaultMaxLocalizeAttempts;

        /// <summary>True when anchored calibration is running this session.</summary>
        public bool Active { get; private set; }
        public AnchoredCalibrationSession Session { get; private set; }

        private void Awake()
        {
            if (calibration == null) calibration = GetComponent<KeyboardCalibration>();
            if (anchorManager == null) anchorManager = GetComponent<PianoAnchorManager>();

            if (!useAnchoredCalibration || !Debug.isDebugBuild)
            {
                Debug.Log($"[AnchorFlow] off ({(Debug.isDebugBuild ? "useAnchoredCalibration unticked" : "release build")}) " +
                          "-- legacy calibration");
                enabled = false;
                return;
            }
            if (calibration == null || anchorManager == null)
            {
                Debug.LogError("[AnchorFlow] KeyboardCalibration or PianoAnchorManager missing -- run " +
                               "PianoGlass/Scene/Configure Anchored Calibration. Falling back to legacy calibration.");
                enabled = false;
                return;
            }

            IPianoAnchorService service = Application.isEditor ? new EditorPianoAnchorService() : ServiceFactory?.Invoke();
            if (service == null)
            {
                Debug.LogError("[AnchorFlow] no anchor service registered -- falling back to legacy calibration.");
                enabled = false;
                return;
            }

            var serializer = new PianoProfileSerializer(
                Path.Combine(Application.persistentDataPath, PianoProfileSerializer.DefaultFileName));
            Session = new AnchoredCalibrationSession(service, serializer, localizeTimeoutSeconds, maxLocalizeAttempts);
            Session.CaptureRequested += calibration.BeginExternalCapture;

            Active = true;
            calibration.SetPlacementOwner(this);
            anchorManager.Bind(Session, calibration);
            Debug.Log($"[AnchorFlow] on ({service.GetType().Name}, profile {serializer.Path})");
        }

        private void OnDestroy()
        {
            if (Session != null && calibration != null) Session.CaptureRequested -= calibration.BeginExternalCapture;
        }

        private void Start()
        {
            if (Active) Run(Session.StartAsync());
        }

        private void Update()
        {
            if (Active) Session.UpdateTracking();
        }

        /// <summary>For the panel: the anchor line, and the last problem if any.</summary>
        public string StatusLine => Active ? Session.StatusLine : "off";
        public AnchorPanelStatus Status => Session != null ? Session.Status : AnchorPanelStatus.Starting;
        public string LastProblem => Session?.LastProblem;

        // ------------------------------------------------------------------
        // IKeyboardPlacementOwner
        // ------------------------------------------------------------------

        public string WaitingPrompt => Status switch
        {
            AnchorPanelStatus.Starting       => "Reading the saved piano...",
            AnchorPanelStatus.Localizing     => "Finding your piano -- look at the keyboard.",
            AnchorPanelStatus.Retrying       => "Still looking for your piano -- look at the keyboard.",
            AnchorPanelStatus.LocalizeFailed => "Could not find the saved piano.  Trigger = try again   B = recalibrate",
            AnchorPanelStatus.Saving         => "Saving the piano's position -- hold still...",
            _                                => "",
        };

        public void OnPointsCaptured(PianoKeyLayout captured) =>
            Run(Session.OnCapturedAsync(captured.LowFrontEdge, captured.HighFrontEdge, captured.Forward,
                                        captured.LowestNote, captured.HighestNote, captured.WhiteKeyDepth));

        public void OnRecalibrateRequested() => Run(Session.RecalibrateAsync());

        public void OnTriggerWhileWaiting() => Run(Session.RetryAsync());

        public void Nudge(Vector3 deltaKeyboardMetres) => Session.Nudge(deltaKeyboardMetres);

        public void AdjustYaw(float deltaDegrees) => Session.AdjustYaw(deltaDegrees);

        public void ClearNudge(string reason)
        {
            Vector3 was = Session.NudgeMm;
            Session.ClearNudge();
            Debug.Log($"[AnchorFlow] nudge cleared ({reason}); was " +
                      $"x {was.x:+0.0;-0.0;0} y {was.y:+0.0;-0.0;0} z {was.z:+0.0;-0.0;0} mm");
        }

        public void Persist() => Session.Persist();

        public Vector3 NudgeMillimetres => Session.NudgeMm;
        public float YawDegrees => Session.SessionYawDegrees;

        // ------------------------------------------------------------------

        private static async void Run(Task task)
        {
            try { await task; }
            catch (Exception e) { Debug.LogError($"[AnchorFlow] exception {e}"); }
        }
    }
}
