using UnityEngine;
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
    /// Tempo (F26), 25-200% in 5% steps (TempoStep, pure), the metronome
    /// (F27), wait mode (F22) and memory fade (F36) change only while the song
    /// is stopped or paused, and are saved. A tempo change while paused
    /// part-way keeps the song where it is and the attempt going; the attempt
    /// then records the slowest tempo it was played at
    /// (ScoringSession.LowestTempo, Ryan 2026-11-03). Turning wait mode on or
    /// off while paused part-way starts a new attempt from there.
    ///
    /// The song panel's buttons and the controllers call these. The A0 piano
    /// chords that did the same were removed (#41, 2026-11-21): every one had
    /// a button, and nobody new would ever find them.
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
        [SerializeField] private CurrentSong current;

        [Header("Metronome")]
        [SerializeField] private Metronome metronome;

        private const string TempoPrefsKey = "PianoGlass.TempoPercent";
        private const string WaitPrefsKey = "PianoGlass.WaitMode";
        private const string MemoryFadePrefsKey = "PianoGlass.MemoryFade";

        /// <summary>The clock holds at each note until it is played (F22).</summary>
        public bool WaitMode { get; private set; }

        /// <summary>The tempo the player chose, which auto-tempo in a loop
        /// leaves alone: leaving the loop goes back to it.</summary>
        private float _chosenTempo = 1f;

        /// <summary>The song loaded.</summary>
        public SongData Song => current != null ? current.Song : null;

        private void Awake()
        {
            if (clock == null) clock = FindAnyObjectByType<SongClock>();
            if (calibration == null) calibration = FindAnyObjectByType<KeyboardCalibration>();
            if (metronome == null) metronome = FindAnyObjectByType<Metronome>();
            if (current == null) current = FindAnyObjectByType<CurrentSong>();
            WaitMode = PlayerPrefs.GetInt(WaitPrefsKey, 0) == 1;

            if (clock != null)
            {
                int stored = PlayerPrefs.GetInt(TempoPrefsKey, 100);
                clock.TempoMultiplier = _chosenTempo = TempoStep.Sanitize(stored / 100f);
                Debug.Log($"[Transport] {TempoStep.Label(clock.TempoMultiplier)} (saved {stored}%)");
                clock.MemoryFadeOn = PlayerPrefs.GetInt(MemoryFadePrefsKey, 0) == 1;
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
                if (Song != null && clock.SongTime >= Song.Duration) return State.Finished;
                if (!clock.IsPlaying) return State.Stopped;
                return clock.SongTime < 0.0 ? State.CountingIn : State.Playing;
            }
        }

        private void OnEnable()
        {
            if (current != null) current.SongChanged += StopAtTop;
        }

        private void OnDisable()
        {
            if (current != null) current.SongChanged -= StopAtTop;
        }

        /// <summary>False while the keyboard's placement isn't trustworthy --
        /// mid-calibration, or (later) while an anchor is unresolved. There's
        /// no visible highway to control then, and calibration owns the
        /// controllers' buttons and the player's attention.</summary>
        private bool CanAcceptInput => calibration != null && calibration.IsPlacementValid;

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

        /// <summary>Stops the clock where it is.</summary>
        public void Pause()
        {
            if (clock != null) clock.Pause();
        }

        /// <summary>A new song is loaded: stopped at the top, out of any loop.</summary>
        private void StopAtTop()
        {
            if (clock == null) return;
            if (clock.Loop.HasValue) clock.TempoMultiplier = _chosenTempo;
            clock.Pause();
            clock.Restart();
        }

        public void RestartAndPlay()
        {
            if (!CanAcceptInput || clock == null) return;
            if (clock.Loop.HasValue) clock.TempoMultiplier = _chosenTempo;
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
            clock.TempoMultiplier = _chosenTempo = tempo;
            PlayerPrefs.SetInt(TempoPrefsKey, TempoStep.ToPercent(tempo));
            PlayerPrefs.Save();
            Debug.Log($"[Transport] restarting at {TempoStep.Label(tempo)}");
            RestartAndPlay();
        }

        /// <summary>The results card's "Play in time": wait mode off, and the
        /// song again from the top. Like RestartAtTempo it works while the
        /// clock is still running past the last note, which ToggleWaitMode
        /// would refuse, because restarting ends that anyway.</summary>
        public void PlayInTime()
        {
            if (!CanAcceptInput || clock == null) return;
            if (WaitMode)
            {
                WaitMode = false;
                PlayerPrefs.SetInt(WaitPrefsKey, 0);
                PlayerPrefs.Save();
                Debug.Log("[Transport] wait mode off: playing in time");
            }
            RestartAndPlay();
        }

        /// <summary>Loop these bars, starting from the count-in before them.
        /// False if the keys are not ready. Restarting from the top leaves
        /// the loop (SongClock.Restart).</summary>
        public bool StartLoop(int fromBar, int toBar)
        {
            if (!CanAcceptInput || clock == null || Song == null) return false;
            clock.StartLoop(LoopRange.Between(Song, fromBar, toBar));
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

            clock.TempoMultiplier = _chosenTempo = after;
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

        /// <summary>Wait mode on/off. Ignored while playing, so what the
        /// clock is doing never changes under the player's hands.</summary>
        public void ToggleWaitMode()
        {
            if (!CanAcceptInput) return;
            if (clock != null && clock.IsPlaying)
            {
                Debug.Log("[Transport] wait mode toggle ignored while playing");
                return;
            }
            WaitMode = !WaitMode;
            PlayerPrefs.SetInt(WaitPrefsKey, WaitMode ? 1 : 0);
            PlayerPrefs.Save();
            Debug.Log($"[Transport] wait mode {(WaitMode ? "on" : "off")}");
        }

        /// <summary>Memory fade on/off, only while stopped or paused.</summary>
        public void ToggleMemoryFade()
        {
            if (!CanAcceptInput || clock == null) return;
            if (clock.IsPlaying)
            {
                Debug.Log("[Transport] memory fade toggle ignored while playing");
                return;
            }
            clock.MemoryFadeOn = !clock.MemoryFadeOn;
            PlayerPrefs.SetInt(MemoryFadePrefsKey, clock.MemoryFadeOn ? 1 : 0);
            PlayerPrefs.Save();
            Debug.Log($"[Transport] memory fade {(clock.MemoryFadeOn ? "on" : "off")}");
        }
    }
}
