namespace PianoGlass.Piano
{
    /// <summary>
    /// Whether controller buttons count right now. While the Quest tracks
    /// hands it reports a pinch as a trigger pull, so with the controllers put
    /// down, pinching a menu from afar captured calibration points and a left
    /// pinch restarted the calibration (2026-11-14). Every script that reads
    /// controller buttons checks this first; hands have their own input
    /// (HandPokePointer, PanelGrabber, HandCalibration).
    /// </summary>
    public static class ControllerInput
    {
        /// <summary>Hands are tracked: controller buttons are ignored.</summary>
        public static bool Ignored => TrackedHands.Instance != null && TrackedHands.Instance.AnyTracked;
    }
}
