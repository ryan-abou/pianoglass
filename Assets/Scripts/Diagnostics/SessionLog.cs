using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace PianoGlass.Diagnostics
{
    /// <summary>
    /// Every build on the headset, release too, writes every log line to a
    /// file of its own, files/logs/&lt;start time&gt;.txt, as well as logcat.
    /// Release builds were left out until round 11 (2026-11-21), when logcat
    /// held only the last 20 seconds and the reason for a bad recenter (#38)
    /// was lost. The file stays on the headset. The
    /// Quest's logcat is cut back to 256 KB whenever the headset restarts
    /// (an OS update did, 2026-11-19), and round 6's results were lost with
    /// it. The newest <see cref="KeepFiles"/> files are kept. Pull them with
    /// adb pull /sdcard/Android/data/com.ryanabourjeili.pianoglass/files/logs
    /// Stack traces only for errors. Lines are buffered and written once a
    /// second, and when the app is paused or closed.
    /// </summary>
    public sealed class SessionLog : MonoBehaviour
    {
        private const int KeepFiles = 10;
        private const float FlushSeconds = 1f;

        private readonly object _lock = new object();
        private readonly StringBuilder _pending = new StringBuilder();
        private string _path;
        private float _nextFlush;
        // Time.* is for the main thread only; log lines can come from any thread.
        private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Add()
        {
            if (Application.isEditor) return;
            var go = new GameObject(nameof(SessionLog));
            DontDestroyOnLoad(go);
            go.AddComponent<SessionLog>();
        }

        private void Awake()
        {
            try
            {
                string dir = Path.Combine(Application.persistentDataPath, "logs");
                Directory.CreateDirectory(dir);
                // UtcNow: the first DateTime.Now on the headset takes ~380 ms (traps.md).
                DateTime start = DateTime.UtcNow;
                _path = Path.Combine(dir, start.ToString("yyyyMMdd-HHmmss") + "Z.txt");
                string[] old = Directory.GetFiles(dir, "*.txt");
                Array.Sort(old, StringComparer.Ordinal);
                for (int i = 0; i < old.Length - (KeepFiles - 1); i++) File.Delete(old[i]);
                File.WriteAllText(_path, $"PianoGlass {Application.version}, started {start:yyyy-MM-dd HH:mm:ss} UTC\n");
                Application.logMessageReceivedThreaded += OnLog;
            }
            catch (Exception e)
            {
                _path = null;
                Debug.LogWarning($"[SessionLog] off: {e.Message}");
            }
        }

        private void OnDestroy()
        {
            Application.logMessageReceivedThreaded -= OnLog;
            Flush();
        }

        private void OnLog(string message, string stack, LogType type)
        {
            lock (_lock)
            {
                _pending.Append(_clock.Elapsed.TotalSeconds.ToString("0000.000"))
                        .Append(type == LogType.Log ? " " : $" {type}: ")
                        .Append(message).Append('\n');
                if ((type == LogType.Error || type == LogType.Exception) && !string.IsNullOrEmpty(stack))
                    _pending.Append(stack).Append('\n');
            }
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextFlush) return;
            _nextFlush = Time.unscaledTime + FlushSeconds;
            Flush();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused) Flush();
        }

        private void OnApplicationQuit() => Flush();

        private void Flush()
        {
            if (_path == null) return;
            string text;
            lock (_lock)
            {
                if (_pending.Length == 0) return;
                text = _pending.ToString();
                _pending.Clear();
            }
            try { File.AppendAllText(_path, text); }
            catch (Exception) { /* storage full or gone: logcat still has it */ }
        }
    }
}
