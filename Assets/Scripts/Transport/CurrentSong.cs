using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using PianoGlass.Song;

namespace PianoGlass.Transport
{
    /// <summary>
    /// The song and hand being practiced, and the one place that changes them.
    /// Everything that needs the song reads it here and rebuilds on
    /// <see cref="SongChanged"/>. Runs before the other scripts, so the song
    /// is there when their Awake asks for it.
    ///
    /// Songs come from the assets SceneSetup assigns, and from the Songs
    /// folder in the app's own storage, re-read whenever the picker opens.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class CurrentSong : MonoBehaviour
    {
        [Tooltip("The songs that come with the app, first one first. Assigned by SceneSetup.")]
        [SerializeField] private MidiSongAsset[] builtInSongs = new MidiSongAsset[0];
        [Tooltip("Assets/UI/NotePalette.asset. Assigned by SceneSetup.")]
        [SerializeField] private NotePalette palette;

        public const string SongsFolderName = "Songs";

        private const string LastSongKey = "PianoGlass.LastSong";
        private const string LastHandKey = "PianoGlass.LastHand";
        private const string SwapHandsPrefix = "PianoGlass.SwapHands.";

        public SongData Song { get; private set; }
        public PracticeHand Hand { get; private set; } = PracticeHand.Right;
        /// <summary>The row the song came from; null if nothing could load.</summary>
        public SongRow Row { get; private set; }
        public SongLibrary Library { get; private set; }
        public NotePalette Palette => palette;

        /// <summary>A different song or hand was picked.</summary>
        public event Action SongChanged;

        public static string SongsFolder => Path.Combine(Application.persistentDataPath, SongsFolderName);

        private void Awake()
        {
            NotePalette.Active = palette;
            if (palette == null)
            {
                palette = ScriptableObject.CreateInstance<NotePalette>();
                Debug.LogError("[Songs] No note palette assigned -- run PianoGlass/Scene/Configure Songs.");
            }

            MakeSongsFolder();
            RefreshLibrary();

            Row = Library.Restore(PlayerPrefs.GetString(LastSongKey, ""));
            PracticeHand remembered = ParseHand(PlayerPrefs.GetString(LastHandKey, ""));
            if (Row == null)
            {
                Debug.LogError("[Songs] No song could be loaded -- run PianoGlass/Scene/Configure Songs.");
                Song = new SongData("No songs", 120f, new SongNote[0]);
                Hand = PracticeHand.All;
                return;
            }

            Song = Row.Song;
            Hand = Row.HandFor(remembered);
            Debug.Log($"[Songs] restored {Describe()}");
        }

        private static void MakeSongsFolder()
        {
            try
            {
                Directory.CreateDirectory(SongsFolder);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Songs] could not make {SongsFolder}: {e.Message}");
            }
        }

        /// <summary>Reads the song list again, so a file copied in while the
        /// app is open shows up without a restart.</summary>
        public void RefreshLibrary()
        {
            var builtIn = new List<SongFile>();
            foreach (MidiSongAsset asset in builtInSongs)
                if (asset != null) builtIn.Add(new SongFile(asset.FileName, asset.Bytes));

            Library = SongLibrary.Build(builtIn, ReadOwnFiles(), SwapHandsOn);
            Debug.Log($"[Songs] {Library.BuiltInCount} built-in, {Library.OwnCount} own");
        }

        private static List<SongFile> ReadOwnFiles()
        {
            var files = new List<SongFile>();
            string[] paths;
            try
            {
                if (!Directory.Exists(SongsFolder)) return files;
                paths = Directory.GetFiles(SongsFolder);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Songs] could not read {SongsFolder}: {e.Message}");
                return files;
            }

            foreach (string path in paths)
            {
                string extension = Path.GetExtension(path).ToLowerInvariant();
                if (extension != ".mid" && extension != ".midi") continue;

                string name = Path.GetFileName(path);
                try
                {
                    files.Add(new SongFile(name, File.ReadAllBytes(path)));
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Songs] could not read {name}: {e.Message}");
                    files.Add(new SongFile(name, null, "Couldn't open this file"));
                }
            }
            return files;
        }

        /// <summary>Loads this song with this hand, stopped at the top. False
        /// if the row can't load.</summary>
        public bool Pick(SongRow row, PracticeHand hand)
        {
            if (row == null || !row.CanLoad) return false;

            Row = row;
            Song = row.Song;
            Hand = row.HandFor(hand);

            PlayerPrefs.SetString(LastSongKey, row.Key);
            PlayerPrefs.SetString(LastHandKey, Hand.ToString());
            PlayerPrefs.Save();

            Debug.Log($"[Songs] picked {Describe()}");
            SongChanged?.Invoke();
            return true;
        }

        private string Describe()
        {
            SongLoadReport report = Row.Report;
            string warning = Song.TempoMap.BarLinesMayBeOff ? ", bar lines may be off" : "";
            return $"'{Song.Title}' {Hand.ToString().ToLowerInvariant()}, {Song.BarCount} bars, " +
                   $"{Song.TempoMap.TimeSignatureText}, {Song.Bpm:0} BPM " +
                   $"({report.NotesMoved} moved, {report.NotesMerged} merged, " +
                   $"{report.NotesOutOfRange} off the piano{warning})";
        }

        private static PracticeHand ParseHand(string saved) =>
            Enum.TryParse(saved, out PracticeHand hand) && Enum.IsDefined(typeof(PracticeHand), hand)
                ? hand
                : PracticeHand.Right;

        private static bool SwapHandsOn(string fileName) => PlayerPrefs.GetInt(SwapHandsPrefix + fileName, 0) == 1;

#if UNITY_EDITOR
        /// <summary>Flips left and right for this song. No menu button yet.</summary>
        [ContextMenu("Swap hands for this song")]
        private void ToggleSwapHands()
        {
            if (Row == null) return;
            string key = SwapHandsPrefix + Row.FileName;
            PlayerPrefs.SetInt(key, SwapHandsOn(Row.FileName) ? 0 : 1);
            PlayerPrefs.Save();
            RefreshLibrary();
            Pick(Library.Find(Row.Key), Hand);
        }
#endif
    }
}
