using System;
using System.Collections.Generic;

namespace PianoGlass.Song
{
    /// <summary>A song file, read from the app or from the Songs folder.</summary>
    public sealed class SongFile
    {
        public readonly string FileName;
        public readonly byte[] Bytes;
        /// <summary>Why the file itself couldn't be opened; null if it could.</summary>
        public readonly string ReadError;

        public SongFile(string fileName, byte[] bytes, string readError = null)
        {
            FileName = fileName;
            Bytes = bytes;
            ReadError = readError;
        }
    }

    /// <summary>One line in the song picker.</summary>
    public sealed class SongRow
    {
        public string Key;
        public string FileName;
        public bool IsBuiltIn;
        public string Title;
        /// <summary>"3/4 · 120 BPM · 32 bars · Hands guessed", or why it
        /// couldn't load.</summary>
        public string Detail;
        public SongLoadReport Report;

        public bool CanLoad => Report != null && Report.Ok;
        public SongData Song => Report?.Song;

        /// <summary>Whether Left and Right mean anything for this song.</summary>
        public bool OffersHands => CanLoad && Song.Hands != HandsStatus.Unknown;

        /// <summary>The hand this row will really play when that one is
        /// chosen: a song with unknown hands is always All notes, and one with
        /// known hands has no All notes.</summary>
        public PracticeHand HandFor(PracticeHand wanted)
        {
            if (!OffersHands) return PracticeHand.All;
            return wanted == PracticeHand.All ? PracticeHand.Right : wanted;
        }
    }

    /// <summary>
    /// The songs on offer: the built-in ones, in the order they were given,
    /// then the player's own, sorted by file name so the list doesn't shuffle.
    /// Pure, so the rules are tested without a headset or a folder.
    /// </summary>
    public sealed class SongLibrary
    {
        public const int RowsPerPage = 5;

        public IReadOnlyList<SongRow> Rows => _rows;
        public int BuiltInCount { get; private set; }
        public int OwnCount { get; private set; }

        private readonly List<SongRow> _rows = new List<SongRow>();

        /// <param name="swapHands">Whether a file name has "swap hands" on.
        /// Null means none do.</param>
        public static SongLibrary Build(IEnumerable<SongFile> builtIn, IEnumerable<SongFile> own,
                                        Func<string, bool> swapHands = null)
        {
            var library = new SongLibrary();

            foreach (SongFile file in builtIn)
            {
                library._rows.Add(MakeRow(file, true, swapHands));
                library.BuiltInCount++;
            }

            var sorted = new List<SongFile>(own);
            sorted.Sort((a, b) => string.Compare(a.FileName, b.FileName, StringComparison.OrdinalIgnoreCase));
            foreach (SongFile file in sorted)
            {
                library._rows.Add(MakeRow(file, false, swapHands));
                library.OwnCount++;
            }
            return library;
        }

        public static string KeyFor(string fileName, bool isBuiltIn) => (isBuiltIn ? "builtin/" : "own/") + fileName;

        private static SongRow MakeRow(SongFile file, bool isBuiltIn, Func<string, bool> swapHands)
        {
            bool swap = swapHands != null && swapHands(file.FileName);
            SongLoadReport report = file.ReadError != null
                ? new SongLoadReport { FileName = file.FileName, Error = file.ReadError }
                : MidiSongLoader.Load(file.Bytes, file.FileName, swap);

            return new SongRow
            {
                Key = KeyFor(file.FileName, isBuiltIn),
                FileName = file.FileName,
                IsBuiltIn = isBuiltIn,
                Report = report,
                Title = report.Ok ? report.Song.Title : MidiSongLoader.TitleFromFileName(file.FileName),
                Detail = report.Ok ? DetailOf(report.Song) : report.Error,
            };
        }

        private static string DetailOf(SongData song)
        {
            var parts = new List<string>
            {
                song.TempoMap.TimeSignatureText,
                $"{song.Bpm:0} BPM",
                song.BarCount == 1 ? "1 bar" : $"{song.BarCount} bars",
            };

            if (song.Hands == HandsStatus.Guessed) parts.Add("Hands guessed");
            if (song.Hands == HandsStatus.Unknown)
            {
                parts.Add("Hands unknown");
                parts.Add("All notes");
            }
            if (song.TempoMap.BarLinesMayBeOff) parts.Add("Bar lines may be off");
            return string.Join(" · ", parts);
        }

        public SongRow Find(string key)
        {
            foreach (SongRow row in _rows)
                if (row.Key == key) return row;
            return null;
        }

        /// <summary>The remembered song, or the first built-in song if that
        /// file is gone or broken. Null only if nothing can load at all.</summary>
        public SongRow Restore(string rememberedKey)
        {
            SongRow remembered = rememberedKey != null ? Find(rememberedKey) : null;
            if (remembered != null && remembered.CanLoad) return remembered;

            foreach (SongRow row in _rows)
                if (row.CanLoad) return row;
            return null;
        }

        public int PageCount => Math.Max(1, (_rows.Count + RowsPerPage - 1) / RowsPerPage);

        /// <summary>Which page a row is on, 0 for the first.</summary>
        public int PageOf(SongRow row)
        {
            int index = _rows.IndexOf(row);
            return index < 0 ? 0 : index / RowsPerPage;
        }
    }
}
