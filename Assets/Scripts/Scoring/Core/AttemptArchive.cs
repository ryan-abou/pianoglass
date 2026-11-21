using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace PianoGlass.Scoring
{
    /// <summary>
    /// Every saved attempt, kept in memory as headers only (no notes), so the
    /// results card can compare with earlier attempts without touching the
    /// disk. The files are read once, on a worker thread, at launch; each new
    /// attempt is added as it is saved.
    ///
    /// Before this, the card read every attempt file on the main thread as it
    /// opened: 146 files took 150-200 ms on the Quest 3 and froze 17-20 frames
    /// after every attempt (#4, round 9, 2026-11-21).
    /// </summary>
    public sealed class AttemptArchive
    {
        /// <summary>The longest the first results card waits for the launch
        /// read to finish (it takes well under a second; an attempt takes longer).</summary>
        public const int WaitForLoadMs = 2000;

        private readonly object _lock = new object();
        private readonly Dictionary<string, AttemptRecord> _byPath =
            new Dictionary<string, AttemptRecord>(StringComparer.OrdinalIgnoreCase);
        private Task _loading = Task.CompletedTask;

        public int Count { get { lock (_lock) return _byPath.Count; } }
        public bool IsLoaded => _loading.IsCompleted;
        /// <summary>Files that could not be read or were not an attempt.</summary>
        public int Unreadable { get; private set; }
        public long LoadMilliseconds { get; private set; }
        /// <summary>Why the launch read stopped, or null. The card then shows
        /// whatever was read before it, like a first attempt.</summary>
        public string LoadError { get; private set; }

        /// <summary>Reads <paramref name="directory"/> on a worker thread and
        /// calls <paramref name="done"/> there when it is finished. The path
        /// must be worked out on the main thread (persistentDataPath).</summary>
        public void StartLoading(string directory, Action<AttemptArchive> done = null)
        {
            _loading = Task.Run(() =>
            {
                try { Load(directory); }
                catch (Exception e) { LoadError = e.Message; }
                done?.Invoke(this);
            });
        }

        /// <summary>Reads every attempt file in <paramref name="directory"/>,
        /// here and now. A file already added by <see cref="Put"/> is not
        /// replaced: the saved one is newer than what was on disk.</summary>
        public void Load(string directory)
        {
            var watch = Stopwatch.StartNew();
            int unreadable = 0;
            string[] files = Directory.Exists(directory) ? Directory.GetFiles(directory, "*.json") : new string[0];
            foreach (string file in files)
            {
                AttemptRecord r;
                try { r = AttemptRecord.Parse(File.ReadAllText(file)); }
                catch (IOException) { r = null; }
                catch (UnauthorizedAccessException) { r = null; }
                if (r == null) { unreadable++; continue; }
                AttemptRecord header = r.HeaderOnly();
                string key = Key(file);
                lock (_lock)
                    if (!_byPath.ContainsKey(key)) _byPath[key] = header;
            }
            Unreadable = unreadable;
            LoadMilliseconds = watch.ElapsedMilliseconds;
        }

        /// <summary>An attempt just written to <paramref name="path"/>. It
        /// replaces an earlier save of the same attempt (an abandoned attempt
        /// is rewritten if it goes on).</summary>
        public void Put(string path, AttemptRecord record)
        {
            if (string.IsNullOrEmpty(path) || record == null) return;
            AttemptRecord header = record.HeaderOnly();
            lock (_lock) _byPath[Key(path)] = header;
        }

        /// <summary>Every attempt but the one saved at <paramref name="exceptPath"/>
        /// (null for all). Waits for the launch read if it is still going.</summary>
        public List<AttemptRecord> AllExcept(string exceptPath)
        {
            if (!_loading.IsCompleted) _loading.Wait(WaitForLoadMs);
            string skip = string.IsNullOrEmpty(exceptPath) ? null : Key(exceptPath);
            var list = new List<AttemptRecord>();
            lock (_lock)
                foreach (KeyValuePair<string, AttemptRecord> kv in _byPath)
                    if (skip == null || !string.Equals(kv.Key, skip, StringComparison.OrdinalIgnoreCase))
                        list.Add(kv.Value);
            return list;
        }

        private static string Key(string path) => Path.GetFullPath(path);
    }
}
