using UnityEngine;

namespace PianoGlass.Transport
{
    /// <summary>
    /// Controller bindings for song playback -- a spare, not the primary
    /// control. While actually playing piano the controllers are on the
    /// floor, and the MIDI restart chord in SongTransport is what gets used;
    /// this is here for setup and for testing without touching the keyboard.
    ///
    ///   Right A     play / pause toggle -- except while anchored calibration
    ///               is waiting for A to confirm the keys (SongTransport
    ///               ignores that press)
    ///   Left Y      restart from the top and play
    ///
    /// B and both triggers belong to OvrCalibrationInput and are deliberately
    /// left alone.
    ///
    /// Explicit OVRInput.Controller arguments throughout, same reason as
    /// OvrCalibrationInput: an unqualified Primary/Secondary button resolves
    /// against whichever controller is "active" and can silently resolve to
    /// nothing on the other hand.
    ///
    /// Meta-specific input isolated in its own file, same split as
    /// KeyboardCalibration / OvrCalibrationInput: all the actual logic lives
    /// in the Meta-free SongTransport, and this file is just OVRInput calls
    /// forwarding into it.
    /// </summary>
    [RequireComponent(typeof(SongTransport))]
    public class OvrSongTransport : MonoBehaviour
    {
        private SongTransport _transport;

        private void Awake() => _transport = GetComponent<SongTransport>();

        private void Update()
        {
            if (OVRInput.GetDown(OVRInput.Button.One, OVRInput.Controller.RTouch))
                _transport.TogglePlayPause();

            // Left grip + Y is the development recording-mode toggle.
            if (OVRInput.GetDown(OVRInput.Button.Two, OVRInput.Controller.LTouch) &&
                !PianoGlass.DevTools.DevOverlays.ModifierHeld)
                _transport.RestartAndPlay();
        }
    }
}
