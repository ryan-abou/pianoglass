using UnityEngine;

namespace PianoGlass.DevTools
{
    /// <summary>
    /// The one part of the development-build overlays (checklist panel,
    /// recording mode) that code shipping in every build can see.
    ///
    /// Holding the left grip turns X and Y into the overlay toggles, so the
    /// display-mode cycle (X) and restart (Y) step aside while it is held.
    /// Nothing else in the app reads the left grip (checked 2026-11-14).
    /// In a release build the overlays are compiled out and the answer is
    /// fixed: never held, so X and Y always do their usual jobs.
    /// </summary>
    public static class DevOverlays
    {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        /// <summary>How far the left grip must be squeezed to count as held.</summary>
        public const float GripThreshold = 0.7f;

        /// <summary>Left grip held: X and Y belong to the overlays right now.
        /// Read live, so it does not matter which script updates first.</summary>
        public static bool ModifierHeld =>
            Debug.isDebugBuild &&
            OVRInput.Get(OVRInput.Axis1D.PrimaryHandTrigger, OVRInput.Controller.LTouch) >= GripThreshold;
#else
        public static bool ModifierHeld => false;
#endif
    }
}
