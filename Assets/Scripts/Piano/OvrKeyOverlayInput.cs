using UnityEngine;

namespace PianoGlass.Piano
{
    /// <summary>
    /// Meta-specific input for the key overlay's display modes.
    ///
    ///   Left X             cycle PianoKeyHighlighter's display mode
    ///   Left stick click   toggle passthrough on/off, so the void can be
    ///                      A/B'd against passthrough on the same build
    ///
    /// Same split as OvrCalibrationInput / KeyboardCalibration and
    /// OvrSongTransport / SongTransport: the actual behaviour lives in a
    /// Meta-free component (PianoKeyHighlighter) and this file is just the
    /// OVRInput calls forwarding into it, so PianoKeyHighlighter keeps
    /// compiling before the Meta XR SDK is installed.
    ///
    /// Explicit OVRInput.Controller.LTouch throughout -- an unqualified
    /// Button.One/PrimaryThumbstick resolves against whichever controller is
    /// "active" and can silently resolve to nothing on the other hand.
    ///
    /// Passthrough initialises asynchronously: setting OVRPassthroughLayer
    /// .enabled = true does not mean passthrough is visible yet. The combined
    /// log line is written immediately on request (so a logcat trail shows
    /// intent right away) and again from OVRPassthroughLayer
    /// .PassthroughLayerResumed, which is Meta's own signal that it actually
    /// came up -- nothing here assumes success before that fires.
    /// </summary>
    public class OvrKeyOverlayInput : MonoBehaviour
    {
        [SerializeField] private PianoKeyHighlighter highlighter;

        [Tooltip("Left unassigned if the scene has no passthrough layer yet -- " +
                 "the stick click then logs a warning instead of toggling anything.")]
        [SerializeField] private OVRPassthroughLayer passthroughLayer;

        private void Awake()
        {
            if (highlighter == null) highlighter = GetComponent<PianoKeyHighlighter>();
            if (passthroughLayer == null) passthroughLayer = FindAnyObjectByType<OVRPassthroughLayer>();
        }

        private void OnEnable()
        {
            if (passthroughLayer != null) passthroughLayer.PassthroughLayerResumed += OnPassthroughResumed;
        }

        private void OnDisable()
        {
            if (passthroughLayer != null) passthroughLayer.PassthroughLayerResumed -= OnPassthroughResumed;
        }

        private void Update()
        {
            if (highlighter == null) return;

            // Left grip + X is the development checklist's toggle.
            if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.LTouch) &&
                !PianoGlass.DevTools.DevOverlays.ModifierHeld)
            {
                highlighter.CycleDisplayMode();
                Log("mode cycled");
            }

            if (OVRInput.GetDown(OVRInput.Button.PrimaryThumbstick, OVRInput.Controller.LTouch))
                TogglePassthrough();
        }

        private void TogglePassthrough()
        {
            if (passthroughLayer == null)
            {
                Debug.LogWarning("[KeyOverlay] stick click — no OVRPassthroughLayer wired, nothing to toggle");
                return;
            }

            passthroughLayer.enabled = !passthroughLayer.enabled;

            // Async on the way up -- OnPassthroughResumed logs the real
            // confirmation. Synchronous on the way down: there is nothing to
            // wait for when hiding it.
            Log(passthroughLayer.enabled ? "passthrough requested on" : "passthrough off");
        }

        private void OnPassthroughResumed() => Log("passthrough resumed");

        /// <summary>Always both: mode and passthrough, in one line, whichever
        /// one changed -- that's the whole point of logging it here instead
        /// of from each half separately.</summary>
        private void Log(string reason)
        {
            string passthroughState = passthroughLayer != null && passthroughLayer.enabled ? "on" : "off";
            Debug.Log($"[KeyOverlay] {reason} — mode={highlighter.Mode} passthrough={passthroughState}");
        }
    }
}
