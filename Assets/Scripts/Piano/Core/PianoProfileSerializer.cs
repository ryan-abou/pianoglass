using System;
using System.Globalization;
using System.IO;
using UnityEngine;

namespace PianoGlass.Piano
{
    public enum ProfileLoadStatus
    {
        /// <summary>No file: first-time setup.</summary>
        Missing,
        Loaded,
        /// <summary>Unparseable or missing a required root field. The file
        /// has been renamed aside (never deleted); start first-time setup.</summary>
        Invalid,
        /// <summary>Written by a newer build. Usable in memory, but Save()
        /// refuses to overwrite it.</summary>
        FutureVersion,
    }

    public sealed class ProfileLoadResult
    {
        public ProfileLoadStatus Status { get; }
        /// <summary>Null for Missing and Invalid.</summary>
        public PianoProfileStore Store { get; }
        public string Reason { get; }
        /// <summary>Where an invalid file was moved to, for the log.</summary>
        public string RenamedTo { get; }

        public ProfileLoadResult(ProfileLoadStatus status, PianoProfileStore store, string reason, string renamedTo = null)
        {
            Status = status; Store = store; Reason = reason; RenamedTo = renamedTo;
        }
    }

    /// <summary>
    /// Reads, validates and atomically writes piano_profiles.json.
    ///
    /// Two rules shape everything here:
    ///   * Nothing is ever deleted. A bad file is renamed aside; an invalid
    ///     profile stays in the file until a replacement has been saved.
    ///   * A write either lands whole or not at all: the new JSON goes to a
    ///     temp file first and replaces the real one only once it is
    ///     completely written, so a crash mid-save leaves the old file intact.
    ///
    /// The path is injected, so tests point it at a temp directory.
    /// </summary>
    public sealed class PianoProfileSerializer
    {
        public const string DefaultFileName = "piano_profiles.json";
        public const string LegacyFileName = "keyboard_calibration.json";

        public string Path { get; }

        /// <summary>Swappable for tests: lets a test make the temp write
        /// throw half-way and check the real file survived.</summary>
        public Action<string, string> WriteAllText { get; set; } = File.WriteAllText;

        /// <summary>Swappable for tests: deterministic timestamps.</summary>
        public Func<DateTime> UtcNow { get; set; } = () => DateTime.UtcNow;

        public PianoProfileSerializer(string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("path is empty", nameof(path));
            Path = path;
        }

        private string TempPath => Path + ".tmp";

        // ------------------------------------------------------------------
        // Load
        // ------------------------------------------------------------------

        public ProfileLoadResult Load()
        {
            if (!File.Exists(Path))
                return new ProfileLoadResult(ProfileLoadStatus.Missing, null, "no profile file");

            string json;
            try
            {
                json = File.ReadAllText(Path);
            }
            catch (Exception e)
            {
                // Unreadable is not the same as corrupt: leave it where it is.
                return new ProfileLoadResult(ProfileLoadStatus.Invalid, null, "could not read: " + e.Message);
            }

            string problem = ParseRoot(json, out PianoProfileStore store);
            if (problem != null)
            {
                string renamed = RenameAside();
                return new ProfileLoadResult(ProfileLoadStatus.Invalid, null, problem, renamed);
            }

            if (store.schemaVersion > PianoProfileStore.CurrentSchemaVersion)
                return new ProfileLoadResult(ProfileLoadStatus.FutureVersion, store,
                    $"schemaVersion {store.schemaVersion} is newer than this build understands " +
                    $"({PianoProfileStore.CurrentSchemaVersion}); running from memory, not saving");

            return new ProfileLoadResult(ProfileLoadStatus.Loaded, store, null);
        }

        /// <summary>Null when the root is usable; otherwise why not. Profiles
        /// are not judged here -- see ValidateProfile.</summary>
        public static string ParseRoot(string json, out PianoProfileStore store)
        {
            store = null;
            if (string.IsNullOrWhiteSpace(json)) return "file is empty";

            // JsonUtility fills a missing field with its default, so the only
            // way to tell "missing" from "zero" is to look for the key.
            if (!HasKey(json, "schemaVersion")) return "schemaVersion is missing";
            if (!HasKey(json, "profiles")) return "profiles is missing";

            try
            {
                store = JsonUtility.FromJson<PianoProfileStore>(json);
            }
            catch (Exception e)
            {
                return "JSON did not parse: " + e.Message;
            }

            if (store == null) return "JSON did not parse";
            if (store.schemaVersion < PianoProfileStore.CurrentSchemaVersion)
                return $"schemaVersion {store.schemaVersion} is not a v2 file";

            if (store.profiles == null) store.profiles = new System.Collections.Generic.List<PianoProfile>();
            if (store.activeProfileId == null) store.activeProfileId = "";

            if (store.hasTipOffset && !AnchoredKeyboard.IsFinite(store.tipOffsetController))
                return "tip offset is not a finite number";
            if (!AnchoredKeyboard.IsFinite(store.tipNudgeKeyboardMm))
                return "tip nudge is not a finite number";

            return null;
        }

        /// <summary>
        /// Null when a profile can be trusted to place the keyboard;
        /// otherwise why not. An invalid profile means full calibration --
        /// but it is not removed from the file until its replacement saves.
        /// </summary>
        public static string ValidateProfile(PianoProfile p)
        {
            if (p == null) return "profile is null";
            if (string.IsNullOrEmpty(p.profileId)) return "profileId is empty";

            string uuid = p.PrimaryAnchorUuid;
            if (string.IsNullOrEmpty(uuid)) return "no anchor UUID";
            if (!Guid.TryParse(uuid, out Guid g) || g == Guid.Empty) return $"anchor UUID '{uuid}' does not parse";

            if (float.IsNaN(p.whiteKeyDepth) || p.whiteKeyDepth < 0.08f || p.whiteKeyDepth > 0.2f)
                return $"white key depth {p.whiteKeyDepth} m is implausible";

            return AnchoredKeyboard.CheckPlausible(p.LocalGeometry, p.lowestNote, p.highestNote);
        }

        // ------------------------------------------------------------------
        // Save
        // ------------------------------------------------------------------

        /// <summary>
        /// Writes the store atomically. Refuses (returns false, file
        /// untouched) if the file on disk was written by a newer build, or if
        /// the store contains numbers that could not be read back.
        /// </summary>
        public bool Save(PianoProfileStore store, out string error)
        {
            error = null;
            if (store == null) { error = "store is null"; return false; }

            if (File.Exists(Path))
            {
                try
                {
                    if (ParseRoot(File.ReadAllText(Path), out PianoProfileStore onDisk) == null &&
                        onDisk.schemaVersion > PianoProfileStore.CurrentSchemaVersion)
                    {
                        error = $"file on disk is schemaVersion {onDisk.schemaVersion}; not overwriting a newer build's data";
                        return false;
                    }
                }
                catch (Exception)
                {
                    // Unreadable existing file: replacing it is the right thing.
                }
            }

            store.schemaVersion = PianoProfileStore.CurrentSchemaVersion;
            string json = JsonUtility.ToJson(store, true);

            if (ParseRoot(json, out _) is string bad)
            {
                error = "refusing to write data that would not load back: " + bad;
                return false;
            }

            try
            {
                string dir = System.IO.Path.GetDirectoryName(Path);
                if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

                WriteAllText(TempPath, json);

                if (File.Exists(Path)) File.Replace(TempPath, Path, null);
                else File.Move(TempPath, Path);
                return true;
            }
            catch (Exception e)
            {
                error = "write failed: " + e.Message;
                TryDelete(TempPath);
                return false;
            }
        }

        // ------------------------------------------------------------------
        // Legacy v1
        // ------------------------------------------------------------------

        /// <summary>A v1 file is recognised by having low and high and no
        /// schemaVersion. Detection only: the file is never modified here.</summary>
        public static bool TryParseLegacyV1(string json, out LegacyCalibrationV1 v1)
        {
            v1 = null;
            if (string.IsNullOrWhiteSpace(json)) return false;
            if (HasKey(json, "schemaVersion")) return false;
            if (!HasKey(json, "low") || !HasKey(json, "high")) return false;

            try
            {
                v1 = JsonUtility.FromJson<LegacyCalibrationV1>(json);
            }
            catch (Exception)
            {
                return false;
            }

            return v1 != null && AnchoredKeyboard.IsFinite(v1.low) && AnchoredKeyboard.IsFinite(v1.high);
        }

        // ------------------------------------------------------------------

        /// <summary>piano_profiles.invalid-20261013T200000Z.json, next to the
        /// original. Returns the new path, or null if it could not be moved.</summary>
        private string RenameAside()
        {
            string dir = System.IO.Path.GetDirectoryName(Path) ?? "";
            string name = System.IO.Path.GetFileNameWithoutExtension(Path);
            string stamp = UtcNow().ToString("yyyyMMdd'T'HHmmss'Z'", CultureInfo.InvariantCulture);
            string target = System.IO.Path.Combine(dir, $"{name}.invalid-{stamp}.json");

            for (int i = 1; File.Exists(target); i++)
                target = System.IO.Path.Combine(dir, $"{name}.invalid-{stamp}-{i}.json");

            try
            {
                File.Move(Path, target);
                return target;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool HasKey(string json, string key) => json.Contains("\"" + key + "\"");

        private static void TryDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); }
            catch (Exception) { /* a stray .tmp is harmless */ }
        }
    }
}
