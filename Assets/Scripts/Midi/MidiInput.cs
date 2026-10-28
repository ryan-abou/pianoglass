using System;
using System.Collections.Generic;
using UnityEngine;

namespace PianoGlass.Midi
{
    /// <summary>
    /// Drains MIDI events from the Android bridge once per frame and turns them
    /// into ordinary C# events on the main thread.
    ///
    /// Put this on one GameObject in your scene. Everything else in the project
    /// listens to it; nothing else should talk to the Java side.
    /// </summary>
    [DefaultExecutionOrder(-100)]      // run before anything that reads notes
    public class MidiInput : MonoBehaviour
    {
        public static MidiInput Instance { get; private set; }

        [Tooltip("How often to re-read the connection status string, in seconds.")]
        [SerializeField] private float statusPollInterval = 0.5f;

        [Tooltip("Log every note to the Unity console. Useful on day three, noisy after that.")]
        [SerializeField] private bool logEvents = false;

        /// <summary>note (0-127), velocity (1-127), seconds since app start.</summary>
        public event Action<int, int, double> NoteOn;

        /// <summary>note (0-127), seconds since app start.</summary>
        public event Action<int, double> NoteOff;

        /// <summary>Raw messages, if you want pedal, pitch bend, etc.</summary>
        public event Action<MidiMessage> Message;

        public bool IsSupported { get; private set; }
        public bool IsConnected { get; private set; }
        public string Status { get; private set; } = "starting...";
        public int TotalNotesReceived { get; private set; }

        private readonly HashSet<int> _heldNotes = new HashSet<int>();
        private float _nextStatusPoll;

#if UNITY_ANDROID && !UNITY_EDITOR
        private AndroidJavaClass _bridge;
#endif

        /// <summary>Notes currently held down. Read-only view for wait-mode etc.</summary>
        public IReadOnlyCollection<int> HeldNotes => _heldNotes;

        public bool IsHeld(int note) => _heldNotes.Contains(note);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try
            {
                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    _bridge = new AndroidJavaClass("com.pianoglass.midi.MidiBridge");
                    IsSupported = _bridge.CallStatic<bool>("init", activity);
                }
                Status = _bridge.CallStatic<string>("getStatus");
                Debug.Log($"[MidiInput] bridge init, supported={IsSupported}, status={Status}");
            }
            catch (Exception e)
            {
                IsSupported = false;
                Status = "bridge failed to load: " + e.Message;
                Debug.LogError("[MidiInput] " + Status);
            }
#else
            IsSupported = false;
            Status = "editor — use MidiEditorSimulator or build to the headset";
#endif
        }

        /// <summary>
        /// A press's timestamp (the seconds NoteOn/NoteOff carry) as
        /// Time.realtimeSinceStartupAsDouble. The timestamps come from the
        /// bridge's own clock, taken when the event arrived; scoring needs
        /// that moment, not the frame that happened to drain it -- at 72 Hz
        /// the frame alone is up to 14 ms of error.
        /// </summary>
        public double ToRealtime(double eventSeconds) =>
            double.IsNaN(_clockOffset) ? Time.realtimeSinceStartupAsDouble : eventSeconds - _clockOffset;

        /// <summary>Event clock minus Unity realtime, refreshed every frame;
        /// NaN when there is no bridge clock (presses then count as "now").</summary>
        private double _clockOffset;
        private bool _bridgeClockFailed;

        private void SyncClock()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (_bridge != null && !_bridgeClockFailed)
            {
                try
                {
                    _clockOffset = _bridge.CallStatic<int>("nowMs") / 1000.0 - Time.realtimeSinceStartupAsDouble;
                    return;
                }
                catch (Exception e)
                {
                    // An APK built without nowMs(). Presses then read as
                    // arriving now: frame-accurate, no worse than before.
                    _bridgeClockFailed = true;
                    Debug.LogWarning("[MidiInput] bridge clock unavailable, timing presses by frame: " + e.Message);
                }
            }
            _clockOffset = double.NaN;
#else
            // The simulator stamps events with Time.time.
            _clockOffset = Time.timeAsDouble - Time.realtimeSinceStartupAsDouble;
#endif
        }

        private void Update()
        {
            SyncClock();
#if UNITY_ANDROID && !UNITY_EDITOR
            if (_bridge != null)
            {
                int[] packed = _bridge.CallStatic<int[]>("drain");
                if (packed != null)
                {
                    for (int i = 0; i + 3 < packed.Length; i += 4)
                    {
                        Dispatch(new MidiMessage(packed[i], packed[i + 1], packed[i + 2], packed[i + 3]));
                    }
                }
            }
#endif

            // The poll timer lives outside the platform guard so the interval
            // field is used on every platform. Otherwise the editor build warns
            // that it is assigned but never read, which is noise you learn to
            // ignore — and ignoring warnings is how you miss a real one.
            if (Time.unscaledTime >= _nextStatusPoll)
            {
                _nextStatusPoll = Time.unscaledTime + statusPollInterval;
                RefreshStatus();
            }
        }

        private void RefreshStatus()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (_bridge == null) return;
            Status = _bridge.CallStatic<string>("getStatus");
            IsConnected = _bridge.CallStatic<bool>("isConnected");
#endif
        }

        private void OnDestroy()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            try { _bridge?.CallStatic("shutdown"); }
            catch (Exception e) { Debug.LogWarning("[MidiInput] shutdown: " + e.Message); }
            _bridge?.Dispose();
            _bridge = null;
#endif
            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------------
        // Dispatch — also the entry point for the editor simulator and tests
        // ------------------------------------------------------------------

        public void Dispatch(MidiMessage msg)
        {
            double time = msg.TimestampMs / 1000.0;
            Message?.Invoke(msg);

            switch (msg.Type)
            {
                // A note-on with velocity 0 IS a note-off. Most keyboards send
                // these instead of real note-offs, using running status. Miss
                // this and every note you play sticks on forever.
                case MidiMessage.NoteOnType when msg.Data2 > 0:
                    if (_heldNotes.Add(msg.Data1)) TotalNotesReceived++;
                    if (logEvents) Debug.Log($"[MidiInput] on  {NoteName(msg.Data1)} vel {msg.Data2}");
                    NoteOn?.Invoke(msg.Data1, msg.Data2, time);
                    break;

                case MidiMessage.NoteOnType:
                case MidiMessage.NoteOffType:
                    _heldNotes.Remove(msg.Data1);
                    if (logEvents) Debug.Log($"[MidiInput] off {NoteName(msg.Data1)}");
                    NoteOff?.Invoke(msg.Data1, time);
                    break;
            }
        }

        /// <summary>Convenience for the simulator and for scripted tests.</summary>
        public void InjectNoteOn(int note, int velocity = 90)
            => Dispatch(new MidiMessage(0x90, note, Mathf.Clamp(velocity, 1, 127), (int)(Time.time * 1000)));

        public void InjectNoteOff(int note)
            => Dispatch(new MidiMessage(0x80, note, 0, (int)(Time.time * 1000)));

        // ------------------------------------------------------------------

        private static readonly string[] Names =
            { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };

        /// <summary>60 -> "C4". Middle C is 60 in the scientific convention.</summary>
        public static string NoteName(int note) => Names[note % 12] + (note / 12 - 1);
    }

    public readonly struct MidiMessage
    {
        public const int NoteOffType = 0x80;
        public const int NoteOnType = 0x90;
        public const int ControlChangeType = 0xB0;

        public readonly int Status;
        public readonly int Data1;
        public readonly int Data2;
        public readonly int TimestampMs;

        public MidiMessage(int status, int data1, int data2, int timestampMs)
        {
            Status = status;
            Data1 = data1;
            Data2 = data2;
            TimestampMs = timestampMs;
        }

        public int Type => Status & 0xF0;
        public int Channel => Status & 0x0F;

        /// <summary>Sustain pedal down (CC 64 >= 64).</summary>
        public bool IsSustainDown => Type == ControlChangeType && Data1 == 64 && Data2 >= 64;

        public override string ToString() => $"{Type:X2} {Data1} {Data2} @{TimestampMs}ms";
    }
}
