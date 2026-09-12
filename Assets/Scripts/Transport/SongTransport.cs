using UnityEngine;
using PianoGlass.Midi;
using PianoGlass.Piano;
using PianoGlass.Song;

namespace PianoGlass.Transport
{
    /// <summary>
    /// Play/pause/restart for the song clock, callable from whatever input
    /// wants to drive it -- OvrSongTransport for controllers, and this
    /// class's own MIDI listener for the keyboard itself.
    ///
    /// Deliberately Meta-free, like KeyboardCalibration: it compiles and runs
    /// in the editor with MidiEditorSimulator, no headset required.
    ///
    /// The MIDI binding is the one that matters in practice -- while actually
    /// playing, the controllers are on the floor. The two lowest keys of an
    /// 88 (A0 + A#0 by default) held together restart from the top and play.
    /// No song here uses that chord, and it can't be hit by accident.
    /// </summary>
    public class SongTransport : MonoBehaviour
    {
        [Header("Wiring")]
        [SerializeField] private SongClock clock;
        [SerializeField] private KeyboardCalibration calibration;

        [Header("MIDI restart chord")]
        [Tooltip("Both of these held together restart the song from the top and " +
                 "play. Defaults to the bottom two keys of an 88-key keyboard.")]
        [SerializeField] private int chordNoteA = 21; // A0
        [SerializeField] private int chordNoteB = 22; // A#0

        private bool _chordLatched;

        private void Awake()
        {
            if (clock == null) clock = FindAnyObjectByType<SongClock>();
            if (calibration == null) calibration = FindAnyObjectByType<KeyboardCalibration>();
        }

        private void OnEnable()
        {
            if (MidiInput.Instance == null) return;
            MidiInput.Instance.NoteOn += OnNoteOn;
            MidiInput.Instance.NoteOff += OnNoteOff;
        }

        private void OnDisable()
        {
            if (MidiInput.Instance == null) return;
            MidiInput.Instance.NoteOn -= OnNoteOn;
            MidiInput.Instance.NoteOff -= OnNoteOff;
        }

        /// <summary>False while KeyboardCalibration hasn't finished -- there's
        /// no meaningful highway to control yet, and calibration owns the
        /// controllers' buttons and the player's attention during setup.</summary>
        private bool CanAcceptInput => calibration != null && calibration.IsCalibrated;

        public void TogglePlayPause()
        {
            if (!CanAcceptInput || clock == null) return;
            if (clock.IsPlaying) clock.Pause(); else clock.Play();
        }

        public void RestartAndPlay()
        {
            if (!CanAcceptInput || clock == null) return;
            clock.Restart();
            clock.Play();
        }

        // ------------------------------------------------------------------

        private void OnNoteOn(int note, int velocity, double time)
        {
            if (note != chordNoteA && note != chordNoteB) return;
            if (_chordLatched) return;

            // Ignored entirely while mid-calibration -- not even latched --
            // so finishing calibration with the chord still held requires a
            // fresh press before it can fire.
            if (!CanAcceptInput) return;

            bool bothHeld = MidiInput.Instance != null &&
                            MidiInput.Instance.IsHeld(chordNoteA) &&
                            MidiInput.Instance.IsHeld(chordNoteB);
            if (!bothHeld) return;

            _chordLatched = true; // fire once per press, not every frame it's held
            RestartAndPlay();
        }

        private void OnNoteOff(int note, double time)
        {
            if (note == chordNoteA || note == chordNoteB) _chordLatched = false;
        }
    }
}
