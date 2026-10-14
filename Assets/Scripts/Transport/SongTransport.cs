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
        /// <summary>What the panel shows so an empty highway (silent lead-in,
        /// or paused) doesn't read as broken. Finished takes priority over
        /// everything else -- even a manual pause after the last note still
        /// reads as Finished, since there's nothing left to play.</summary>
        public enum State { Stopped, CountingIn, Playing, Finished }

        [Header("Wiring")]
        [SerializeField] private SongClock clock;
        [SerializeField] private KeyboardCalibration calibration;

        [Header("MIDI restart chord")]
        [Tooltip("Both of these held together restart the song from the top and " +
                 "play. Defaults to the bottom two keys of an 88-key keyboard.")]
        [SerializeField] private int chordNoteA = 21; // A0
        [SerializeField] private int chordNoteB = 22; // A#0

        private bool _chordLatched;
        private SongData _song;

        private void Awake()
        {
            if (clock == null) clock = FindAnyObjectByType<SongClock>();
            if (calibration == null) calibration = FindAnyObjectByType<KeyboardCalibration>();
            _song = SongData.TwinkleTwinkleLittleStar();
        }

        /// <summary>Derived fresh from clock + song each read, not cached --
        /// cheap (a couple of comparisons) and the only alternative is another
        /// piece of state that could fall out of sync with the clock.</summary>
        public State CurrentState
        {
            get
            {
                if (clock == null) return State.Stopped;
                if (_song != null && clock.SongTime >= _song.Duration) return State.Finished;
                if (!clock.IsPlaying) return State.Stopped;
                return clock.SongTime < 0.0 ? State.CountingIn : State.Playing;
            }
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

        /// <summary>False while the keyboard's placement isn't trustworthy --
        /// mid-calibration, or (later) while an anchor is unresolved. There's
        /// no visible highway to control then, and calibration owns the
        /// controllers' buttons and the player's attention.</summary>
        private bool CanAcceptInput => calibration != null && calibration.IsPlacementValid;

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
