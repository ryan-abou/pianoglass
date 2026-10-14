using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PianoGlass.Piano
{
    /// <summary>
    /// Finds where the controller's plastic tip actually is, and puts
    /// PointerTip there.
    ///
    /// PointerTip has always sat at the controller's tracked origin, with the
    /// thumbstick nudge absorbing the difference as a world-space shift. But
    /// the tip offset is fixed in the *controller's* frame, so it points a
    /// different way whenever the wrist is angled differently -- and reaching
    /// for A0 versus C8, it always is. That leaves a yaw and width error no
    /// shift can remove. Measure the offset once and every capture is right
    /// whatever the wrist angle.
    ///
    /// The flow: start it, hold the tip on one fixed spot (a key's front
    /// edge), and rock the controller in circles for ~10 s. The point that
    /// stays still is the tip. PivotCalibration does the maths.
    ///
    /// The result is saved at the root of piano_profiles.json, not with the
    /// piano: it belongs to the controller and is right at every keyboard.
    ///
    /// Meta-free, like KeyboardCalibration: OvrCalibrationInput (left menu
    /// button) drives it on the headset; the context menu drives it anywhere.
    /// </summary>
    public class ControllerTipCalibration : MonoBehaviour
    {
        public enum Phase { Idle, Settling, Recording }

        [Tooltip("PointerTip -- an empty child of the right controller anchor. " +
                 "Its parent's pose is what gets sampled; its localPosition is " +
                 "what gets set.")]
        [SerializeField] private Transform pointerTip;

        [Tooltip("Seconds to get the tip onto the spot before recording starts.")]
        [SerializeField] private float settleSeconds = 1.5f;
        [Tooltip("Seconds of rocking to record.")]
        [SerializeField] private float recordSeconds = 10f;
        [Tooltip("Samples per second while recording.")]
        [SerializeField] private float sampleRate = 30f;
        [Tooltip("Reject a result whose RMS residual is above this, in millimetres. " +
                 "Under ~2 mm is good; above ~3 mm the tip slipped.")]
        [SerializeField] private float maxRmsMm = 3f;
        [Tooltip("Reject an offset longer than this, in metres -- the Touch tip " +
                 "is a few centimetres from the tracked origin, not decimetres.")]
        [SerializeField] private float maxOffsetMetres = 0.15f;

        public Phase CurrentPhase { get; private set; } = Phase.Idle;
        public bool IsBusy => CurrentPhase != Phase.Idle;

        /// <summary>0..1 through the recording, for the panel.</summary>
        public float Progress { get; private set; }

        public bool HasOffset { get; private set; }
        public Vector3 OffsetMetres { get; private set; }
        public float RmsMm { get; private set; }

        /// <summary>One line for the panel: instructions while running, the
        /// last result or rejection afterwards.</summary>
        public string Status { get; private set; } = "";

        private readonly List<Pose> _samples = new List<Pose>(512);
        private float _phaseStart;
        private float _nextSample;

        private PianoProfileSerializer Serializer =>
            new PianoProfileSerializer(Path.Combine(Application.persistentDataPath, PianoProfileSerializer.DefaultFileName));

        private void Awake()
        {
            if (pointerTip == null)
            {
                var calibration = GetComponent<KeyboardCalibration>();
                if (calibration != null) pointerTip = calibration.PointerTip;
            }
        }

        private void Start() => LoadSaved();

        // ------------------------------------------------------------------

        /// <summary>Start a measurement, or abandon one in progress.</summary>
        [ContextMenu("Start or cancel tip calibration")]
        public void BeginOrCancel()
        {
            if (IsBusy)
            {
                CurrentPhase = Phase.Idle;
                Status = "Tip calibration cancelled -- previous tip kept.";
                Debug.Log("[TipCal] cancelled");
                return;
            }

            if (pointerTip == null || pointerTip.parent == null)
            {
                Status = "Tip calibration unavailable: PointerTip is not a child of the controller.";
                Debug.LogError("[TipCal] pointerTip missing or has no parent controller transform");
                return;
            }

            _samples.Clear();
            Progress = 0f;
            CurrentPhase = Phase.Settling;
            _phaseStart = Time.time;
            Status = "Put the tip on a key's front edge and hold it there...";
            Debug.Log("[TipCal] started");
        }

        private void Update()
        {
            switch (CurrentPhase)
            {
                case Phase.Settling:
                    if (Time.time - _phaseStart < settleSeconds) return;
                    CurrentPhase = Phase.Recording;
                    _phaseStart = Time.time;
                    _nextSample = Time.time;
                    Status = "Keep the tip still and rock the controller in circles...";
                    break;

                case Phase.Recording:
                    Record();
                    break;
            }
        }

        private void Record()
        {
            float elapsed = Time.time - _phaseStart;
            Progress = Mathf.Clamp01(elapsed / recordSeconds);

            if (Time.time >= _nextSample)
            {
                // The controller's own pose, not PointerTip's: the tip offset
                // is exactly the thing being solved for.
                Transform controller = pointerTip.parent;
                _samples.Add(new Pose(controller.position, controller.rotation));
                _nextSample += 1f / Mathf.Max(1f, sampleRate);
            }

            if (elapsed < recordSeconds) return;

            CurrentPhase = Phase.Idle;
            Finish(PivotCalibration.Solve(_samples));
        }

        private void Finish(PivotResult r)
        {
            float rmsMm = r.RmsMetres * 1000f;
            Debug.Log($"[TipCal] samples={_samples.Count} success={r.Success} " +
                      $"offset={Mm(r.TipOffset)} rms={rmsMm:0.00}mm spread={r.SpreadDegrees:0}deg " +
                      $"weakAxis={r.WeakAxisDegrees:0.0}deg error={r.Error ?? "-"}");

            if (!r.Success)
            {
                Status = $"Tip not saved: {r.Error}. Previous tip kept.";
                return;
            }
            if (rmsMm > maxRmsMm)
            {
                Status = $"Tip not saved: RMS {rmsMm:0.0} mm (over {maxRmsMm:0} mm) -- the tip slipped. Try again.";
                return;
            }
            if (r.TipOffset.magnitude > maxOffsetMetres)
            {
                Status = $"Tip not saved: offset {r.TipOffset.magnitude * 1000f:0} mm is implausibly long. Try again.";
                return;
            }

            if (!Save(r.TipOffset, rmsMm, out string error))
            {
                Status = $"Tip measured but not saved: {error}";
                Debug.LogWarning("[TipCal] " + error);
                Apply(r.TipOffset, rmsMm); // still use it this session
                return;
            }

            Apply(r.TipOffset, rmsMm);
            Status = $"Tip saved: {r.TipOffset.magnitude * 1000f:0.0} mm from the tracked origin, RMS {rmsMm:0.0} mm. " +
                     "Clear the nudge (right stick click) and recalibrate (B).";
        }

        private void Apply(Vector3 offset, float rmsMm)
        {
            pointerTip.localPosition = offset;
            HasOffset = true;
            OffsetMetres = offset;
            RmsMm = rmsMm;
            Debug.Log($"[TipCal] PointerTip.localPosition = {Mm(offset)}");
        }

        // ------------------------------------------------------------------
        // Persistence
        // ------------------------------------------------------------------

        private void LoadSaved()
        {
            if (pointerTip == null) return;

            ProfileLoadResult r = Serializer.Load();
            if (r.Status == ProfileLoadStatus.Invalid)
                Debug.LogWarning($"[TipCal] profile file invalid ({r.Reason}); moved to {r.RenamedTo ?? "(could not move)"}");

            PianoProfileStore store = r.Store;
            if (store == null || !store.hasTipOffset) return;

            if (store.tipOffsetController.magnitude > maxOffsetMetres)
            {
                Debug.LogWarning($"[TipCal] saved tip offset {Mm(store.tipOffsetController)} is implausible; ignoring it");
                return;
            }

            Apply(store.tipOffsetController, store.tipOffsetRmsMm);
            Debug.Log($"[TipCal] loaded saved tip (RMS {store.tipOffsetRmsMm:0.0} mm, {store.tipOffsetUtc})");
        }

        private bool Save(Vector3 offset, float rmsMm, out string error)
        {
            PianoProfileSerializer serializer = Serializer;
            ProfileLoadResult existing = serializer.Load();

            if (existing.Status == ProfileLoadStatus.FutureVersion)
            {
                error = "the profile file was written by a newer build; not overwriting it";
                return false;
            }

            // Missing or (already renamed-aside) invalid: start a fresh store.
            // Loaded: keep every profile in it untouched.
            PianoProfileStore store = existing.Store ?? new PianoProfileStore();
            store.hasTipOffset = true;
            store.tipOffsetController = offset;
            store.tipOffsetRmsMm = rmsMm;
            store.tipOffsetUtc = System.DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");

            if (!serializer.Save(store, out error)) return false;
            Debug.Log($"[TipCal] saved to {serializer.Path}");
            return true;
        }

        [ContextMenu("Forget saved tip (back to the controller origin)")]
        public void ForgetSaved()
        {
            if (IsBusy) return;

            PianoProfileSerializer serializer = Serializer;
            ProfileLoadResult existing = serializer.Load();
            if (existing.Store != null && existing.Status == ProfileLoadStatus.Loaded)
            {
                existing.Store.hasTipOffset = false;
                serializer.Save(existing.Store, out _);
            }

            if (pointerTip != null) pointerTip.localPosition = Vector3.zero;
            HasOffset = false;
            Status = "Tip reset to the controller origin.";
            Debug.Log("[TipCal] forgotten; PointerTip back at local origin");
        }

        private static string Mm(Vector3 v) => $"({v.x * 1000f:0.0}, {v.y * 1000f:0.0}, {v.z * 1000f:0.0}) mm";
    }
}
