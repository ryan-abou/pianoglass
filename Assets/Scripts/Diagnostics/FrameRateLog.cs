using UnityEngine;
using PianoGlass.Song;
using PianoGlass.Transport;

namespace PianoGlass.Diagnostics
{
    /// <summary>
    /// One "[Perf]" line in the log each time a song stops playing (finished,
    /// paused or restarted): its average frame rate, how many frames were
    /// slow and the worst one (#38). Every build, so a release build gives a
    /// frame rate reading from the log file without logcat. Adds itself after
    /// the scene loads; no scene change.
    /// </summary>
    public sealed class FrameRateLog : MonoBehaviour
    {
        private readonly FrameStats _stats = new FrameStats();
        private SongTransport _transport;
        private CurrentSong _current;
        private bool _wasPlaying;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Add()
        {
            var go = new GameObject(nameof(FrameRateLog));
            DontDestroyOnLoad(go);
            go.AddComponent<FrameRateLog>();
        }

        private void Start()
        {
            _transport = FindAnyObjectByType<SongTransport>();
            _current = FindAnyObjectByType<CurrentSong>();
            if (_transport == null) enabled = false;
        }

        private void Update()
        {
            bool playing = _transport.CurrentState == SongTransport.State.Playing;
            // The first frame's time includes whatever came before the song started.
            if (playing && _wasPlaying) _stats.Add(Time.unscaledDeltaTime);
            else if (!playing && _wasPlaying) Report();
            _wasPlaying = playing;
        }

        private void Report()
        {
            if (_stats.Frames == 0) return;
            string title = _current != null && _current.Song != null ? _current.Song.Title : "song";
            Debug.Log($"[Perf] {_stats.Summary(title)}");
            _stats.Clear();
        }
    }
}
