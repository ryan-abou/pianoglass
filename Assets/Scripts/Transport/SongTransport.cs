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
    ///
    /// Tempo (F26) is on the same bottom key: hold A0 and press B0 for -5%,
    /// C1 for +5%, 25-200% (TransportChord and TempoStep, both pure). Only
    /// while the song is stopped or paused. A change while paused part-way
    /// keeps the song where it is and the attempt going; the attempt then
    /// records the slowest tempo it was played at (ScoringSession.LowestTempo,
    /// Ryan 2026-11-03). The tempo is saved to PlayerPrefs and applied to
    /// the clock on launch.
    ///
    /// Metronome (F27): hold A0 and press C#1 to turn it on or off, also
    /// only while stopped or paused.
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

        [Header("Tempo chord (hold chordNoteA)")]
        [SerializeField] private int slowerNote = TransportChord.B0;
        [SerializeField] private int fasterNote = TransportChord.C1;
        [SerializeField] private int metronomeNote = TransportChord.CSharp1;

        [Header("Metronome")]
        [SerializeField] private Metronome metronome;

        private const string TempoPrefsKey = "PianoGlass.TempoPercent";

        private bool _chordLatched;
        private SongData _song;

        private void Awake()
        {
            if (clock == null) clock = FindAnyObjectByType<SongClock>();
            if (calibration == null) calibration = FindAnyObjectByType<KeyboardCalibration>();
            if (metronome == null) metronome = FindAnyObjectByType<Metronome>();
            _song = SongData.TwinkleTwinkleLittleStar();

            if (clock != null)
            {
                int stored = PlayerPrefs.GetInt(TempoPrefsKey, 100);
                clock.TempoMultiplier = TempoStep.Sanitize(stored / 100f);
                Debug.Log($"[Transport] {TempoStep.Label(clock.TempoMultiplier)} (saved {stored}%)");
            }
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

        /// <summary>One of the two keys of the MIDI restart chord. Scoring
        /// leaves these out unless the song itself uses them.</summary>
        public bool IsRestartChordNote(int note) => note == chordNoteA || note == chordNoteB;

        public void TogglePlayPause()
        {
            if (!CanAcceptInput || clock == null) return;
            // That A belongs to "looks right". Both checks, because which of
            // OvrCalibrationInput and OvrSongTransport updates first is not
            // fixed: before the confirm it is still awaited, after it the
            // confirm happened this frame.
            if (calibration.AwaitingConfirmation || calibration.ConfirmedThisFrame) return;
            if (clock.IsPlaying) clock.Pause(); else clock.Play();
        }

        public void RestartAndPlay()
        {
            if (!CanAcceptInput || clock == null) return;
            clock.Restart();
            clock.Play();
        }

        /// <summary>Restart from the top at this tempo, saved like any tempo
        /// change: the results card's "Try 90% tempo". It works while the
        /// clock is still running past the last note, which StepTempo would
        /// refuse, because restarting ends that anyway.</summary>
        public void RestartAtTempo(int percent)
        {
            if (!CanAcceptInput || clock == null) return;
            float tempo = TempoStep.Sanitize(percent / 100f);
            clock.TempoMultiplier = tempo;
            PlayerPrefs.SetInt(TempoPrefsKey, TempoStep.ToPercent(tempo));
            PlayerPrefs.Save();
            Debug.Log($"[Transport] restarting at {TempoStep.Label(tempo)}");
            RestartAndPlay();
        }

        /// <summary>Loop these bars, starting from the count-in before them.
        /// False if the keys are not ready. Restarting from the top leaves
        /// the loop (SongClock.Restart).</summary>
        public bool StartLoop(int fromBar, int toBar)
        {
            if (!CanAcceptInput || clock == null || _song == null) return false;
            clock.StartLoop(LoopRange.Between(_song, fromBar, toBar));
            clock.Play();
            return true;
        }

        /// <summary>One 5% tempo step (direction -1 or +1). Ignored while
        /// playing; while paused part-way the song stays where it is.</summary>
        public void StepTempo(int direction)
        {
            if (!CanAcceptInput || clock == null) return;
            if (clock.IsPlaying)
            {
                Debug.Log($"[Transport] tempo change ignored while playing ({TempoStep.Label(clock.TempoMultiplier)})");
                return;
            }

            float before = clock.TempoMultiplier;
            float after = TempoStep.Step(before, direction);
            if (Mathf.Approximately(before, after))
            {
                Debug.Log($"[Transport] {TempoStep.Label(after)} -- already at the limit");
                return;
            }

            clock.TempoMultiplier = after;
            PlayerPrefs.SetInt(TempoPrefsKey, TempoStep.ToPercent(after));
            PlayerPrefs.Save();
            Debug.Log($"[Transport] {TempoStep.Label(before)} -> {TempoStep.Label(after)} " +
                      $"at t = {clock.SongTime:0.0}s");
        }

        /// <summary>Metronome on/off. Ignored while playing, so an attempt
        /// either has it or does not.</summary>
        public void ToggleMetronome()
        {
            if (!CanAcceptInput || metronome == null) return;
            if (clock != null && clock.IsPlaying)
            {
                Debug.Log("[Transport] metronome toggle ignored while playing");
                return;
            }
            metronome.Toggle();
        }

        // ------------------------------------------------------------------

        private void OnNoteOn(int note, int velocity, double time)
        {
            if (MidiInput.Instance == null) return;

            // Ignored entirely while mid-calibration -- not even latched --
            // so finishing calibration with the chord still held requires a
            // fresh press before it can fire.
            if (!CanAcceptInput) return;

            ChordAction action = TransportChord.OnNoteOn(note, MidiInput.Instance.IsHeld,
                                                         chordNoteA, chordNoteB, slowerNote, fasterNote, metronomeNote);
            switch (action)
            {
                case ChordAction.Restart:
                    if (_chordLatched) return;
                    _chordLatched = true; // fire once per press, not every frame it's held
                    RestartAndPlay();
                    break;
                case ChordAction.Slower:
                    StepTempo(-1);
                    break;
                case ChordAction.Faster:
                    StepTempo(+1);
                    break;
                case ChordAction.ToggleMetronome:
                    ToggleMetronome();
                    break;
            }
        }

        private void OnNoteOff(int note, double time)
        {
            if (note == chordNoteA || note == chordNoteB) _chordLatched = false;
        }
    }
}
