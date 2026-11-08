using System.Collections.Generic;
using UnityEngine;
using PianoGlass.Song;

namespace PianoGlass.Transport
{
    /// <summary>
    /// The metronome (F27): clicks on every beat, accented on the first of
    /// each bar, counting in through the lead-in. Scheduled on the DSP clock
    /// with AudioSource.PlayScheduled -- never Play() from Update, which
    /// lands wherever the frame happens to fall. MetronomeSchedule (pure,
    /// tested) decides which beats and when; this owns the AudioSources.
    ///
    /// outputLatencyMs is the headset's audio output latency: how long after
    /// a clip's scheduled start the sound actually leaves the speaker. It is
    /// a property of the hardware, measured once on video (click against the
    /// bar line reaching the hit line) and set by SceneSetup, never learned
    /// from the player -- the same rule as the input offset.
    ///
    /// On/off is toggled with the A0 + C#1 chord (SongTransport), only while
    /// stopped or paused, and saved to PlayerPrefs. ScoreKeeper records
    /// whether it clicked during each attempt.
    ///
    /// "Now" on the DSP clock comes from DspClockEstimator, not a raw
    /// AudioSettings.dspTime read: that is up to a buffer stale on Android,
    /// and scheduling from it made the first build's clicks wander +-50 ms.
    ///
    /// The clicks are generated in code (short decaying sine bursts), so
    /// there is no audio asset to import or strip.
    /// </summary>
    public class Metronome : MonoBehaviour
    {
        [SerializeField] private SongClock clock;
        [Tooltip("Headset audio output latency, ms. Measured on video; 0 until then.")]
        [SerializeField] private float outputLatencyMs = 0f;
        [Tooltip("How far ahead clicks are handed to the audio system, real seconds. " +
                 "More than the output latency plus a frame.")]
        [SerializeField] private float lookaheadSeconds = 0.3f;
        [Range(0f, 1f)] [SerializeField] private float volume = 0.6f;
        [Range(0f, 1f)] [SerializeField] private float accentVolume = 0.9f;

        private const string PrefsKey = "PianoGlass.Metronome";
        private const int Voices = 8;

        public bool IsOn { get; private set; }

        private SongData _song;
        private MetronomeSchedule _schedule;
        private readonly DspClockEstimator _dsp = new DspClockEstimator();
        private readonly List<MetronomeClick> _clicks = new List<MetronomeClick>();
        private AudioSource[] _voices;
        private int _nextVoice;
        private AudioClip _click, _accent;
        private bool _wasPlaying;
        private double _lastSongTime = double.NegativeInfinity;
        private int _lastWraps;
        private bool _wasHolding;

        private void Awake()
        {
            if (clock == null) clock = FindAnyObjectByType<SongClock>();
            _song = SongData.TwinkleTwinkleLittleStar();
            _schedule = new MetronomeSchedule(lookaheadSeconds, outputLatencyMs / 1000.0);

            _click = MakeClick("MetronomeClick", 1000f, 0.025f);
            _accent = MakeClick("MetronomeAccent", 1600f, 0.035f);
            _voices = new AudioSource[Voices];
            for (int i = 0; i < Voices; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;   // a metronome is in your head, not in the room
                source.bypassEffects = true;
                _voices[i] = source;
            }

            IsOn = PlayerPrefs.GetInt(PrefsKey, 0) == 1;
            AudioSettings.GetDSPBufferSize(out int length, out int buffers);
            int rate = AudioSettings.outputSampleRate;
            double bufferMs = rate > 0 ? 1000.0 * length * buffers / rate : 0.0;
            Debug.Log($"[Metronome] {(IsOn ? "on" : "off")}, output latency {outputLatencyMs:0} ms " +
                      $"(DSP buffer {length} x {buffers} @ {rate} Hz = {bufferMs:0.0} ms)");
        }

        public void Toggle()
        {
            IsOn = !IsOn;
            PlayerPrefs.SetInt(PrefsKey, IsOn ? 1 : 0);
            PlayerPrefs.Save();
            if (!IsOn) StopAll();
            Debug.Log($"[Metronome] {(IsOn ? "on" : "off")}");
        }

        private void Update()
        {
            // Every frame, on or off, so the estimate is warm before the
            // first click. Raw dspTime is a buffer stale on Android.
            double real = Time.realtimeSinceStartupAsDouble;
            _dsp.Observe(real, AudioSettings.dspTime);

            if (clock == null) return;
            bool playing = clock.IsPlaying;
            double songTime = clock.SongTime;

            // Paused, stopped, restarted or seeked: the clicks already handed
            // out belong to a timeline that no longer exists.
            bool wrapped = clock.Wraps != _lastWraps;
            _lastWraps = clock.Wraps;
            bool jumpedBack = songTime < _lastSongTime - 1e-6;
            if ((_wasPlaying && !playing) || (jumpedBack && !wrapped))
            {
                StopAll();
                _schedule.Reset();
            }
            else if (wrapped)
            {
                // A loop's wrap. The click for its last bar line is scheduled
                // and belongs to this timeline, the count-in's first beat: let
                // it sound, and carry on from the song time wrapped to.
                _schedule.Reset();
            }
            _wasPlaying = playing;
            _lastSongTime = songTime;

            // Wait mode: no beat while the song is held. A click past the hold
            // is taken back, and the beats pick up again when it lifts.
            bool holding = clock.IsHolding;
            if (holding && !_wasHolding)
            {
                StopAll();
                _schedule.Reset();
            }
            _wasHolding = holding;

            if (!IsOn || !playing || holding) return;

            // In a loop, clicks stop at its end: nothing past the wrap will be played.
            double earliest = -clock.LeadInSeconds, latest = _song.Duration;
            if (clock.Loop is LoopRange loop)
            {
                earliest = System.Math.Min(earliest, loop.WrapTo);
                latest = loop.NotesUntil;
            }
            latest = System.Math.Min(latest, clock.HoldAt);
            _schedule.Collect(_song.TempoMap, earliest, latest,
                              songTime, clock.TempoMultiplier, _dsp.Estimate(real), _clicks);
            foreach (MetronomeClick c in _clicks)
            {
                AudioSource v = _voices[_nextVoice];
                _nextVoice = (_nextVoice + 1) % Voices;
                v.clip = c.Accent ? _accent : _click;
                v.volume = c.Accent ? accentVolume : volume;
                v.PlayScheduled(c.DspTime);
            }
        }

        private void StopAll()
        {
            if (_voices == null) return;
            foreach (AudioSource v in _voices) if (v != null) v.Stop();
        }

        private static AudioClip MakeClick(string name, float frequency, float seconds)
        {
            int rate = AudioSettings.outputSampleRate > 0 ? AudioSettings.outputSampleRate : 48000;
            int n = Mathf.Max(1, Mathf.RoundToInt(rate * seconds));
            var data = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)rate;
                // Sharp attack, fast exponential decay: a tick, not a beep.
                float env = Mathf.Exp(-t / (seconds * 0.25f));
                data[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * env;
            }
            AudioClip clip = AudioClip.Create(name, n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
