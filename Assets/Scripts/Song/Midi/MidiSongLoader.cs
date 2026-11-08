using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace PianoGlass.Song
{
    /// <summary>How loading one file went.</summary>
    public sealed class SongLoadReport
    {
        public string FileName;
        /// <summary>Null when the file couldn't be loaded.</summary>
        public SongData Song;
        /// <summary>Why it couldn't, in words for a greyed-out row.</summary>
        public string Error;
        public int NotesMoved;
        public int NotesMerged;
        public int NotesOutOfRange;

        public bool Ok => Song != null;
    }

    /// <summary>
    /// Puts the reader, the tempo map, the hand assigner and the chord tidy
    /// together: file bytes in, a SongData and a report out. Pure, and never
    /// throws on a bad file: the reason goes in the report.
    /// </summary>
    public static class MidiSongLoader
    {
        /// <summary>A note shorter than this would never be visible.</summary>
        public const double MinNoteSeconds = 0.050;

        public static SongLoadReport Load(byte[] bytes, string fileName, bool swapHands = false)
        {
            var report = new SongLoadReport { FileName = fileName };

            MidiFile file;
            try
            {
                file = MidiFileReader.Read(bytes);
            }
            catch (MidiFormatException e)
            {
                report.Error = e.Message;
                return report;
            }
            report.NotesOutOfRange = file.NotesOutOfRange;

            var noteTracks = new List<MidiTrack>();
            var infos = new List<TrackHandInfo>();
            long endTick = 0;
            foreach (MidiTrack track in file.Tracks)
            {
                if (track.Notes.Count == 0) continue;
                long pitchSum = 0;
                foreach (MidiNote n in track.Notes)
                {
                    pitchSum += n.Pitch;
                    if (n.EndTick > endTick) endTick = n.EndTick;
                }
                noteTracks.Add(track);
                infos.Add(new TrackHandInfo(track.Name, (double)pitchSum / track.Notes.Count));
            }

            if (noteTracks.Count == 0)
            {
                report.Error = "Has no notes to play";
                return report;
            }

            TempoMap map = TempoMap.FromMidi(file.TicksPerBeat, file.Tempos, file.TimeSignatures, endTick);
            if (map.TooLong)
            {
                report.Error = "Too long to load";
                return report;
            }

            HandAssignment hands = HandAssigner.Assign(infos, swapHands);
            var notes = new List<SongNote>();
            for (int t = 0; t < noteTracks.Count; t++)
            {
                foreach (MidiNote n in noteTracks[t].Notes)
                {
                    double start = map.TicksToSeconds(n.StartTick);
                    double length = Math.Max(map.TicksToSeconds(n.EndTick) - start, MinNoteSeconds);
                    notes.Add(new SongNote(n.Pitch, start, length, hands.Hands[t]));
                }
            }
            notes.Sort((a, b) =>
            {
                int c = a.StartTime.CompareTo(b.StartTime);
                return c != 0 ? c : a.Pitch.CompareTo(b.Pitch);
            });

            ChordTidyResult tidy = ChordTidy.Run(notes);
            report.NotesMoved = tidy.Moved;
            report.NotesMerged = tidy.Merged;
            report.Song = new SongData(TitleOf(file, fileName), tidy.Notes, map, hands.Status);
            return report;
        }

        /// <summary>The name of a first track with no notes, which is where
        /// files keep their title, or else one made from the file name.</summary>
        private static string TitleOf(MidiFile file, string fileName)
        {
            if (file.Tracks.Count > 0 && file.Tracks[0].Notes.Count == 0 && IsTitle(file.Tracks[0].Name))
                return file.Tracks[0].Name;
            return TitleFromFileName(fileName);
        }

        private static bool IsTitle(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return false;
            string lower = name.ToLowerInvariant();
            return !lower.Contains("tempo") && !lower.Contains("untitled") &&
                   !lower.Contains("conductor") && !lower.StartsWith("track", StringComparison.Ordinal);
        }

        private static readonly HashSet<string> SmallWords =
            new HashSet<string> { "a", "an", "and", "in", "of", "on", "the", "to", "for" };

        /// <summary>"MinuetInG.mid" becomes "Minuet in G", and "my_song.mid"
        /// becomes "my song".</summary>
        public static string TitleFromFileName(string fileName)
        {
            string stem = Path.GetFileNameWithoutExtension(fileName ?? "");
            var spaced = new StringBuilder(stem.Length + 8);
            for (int i = 0; i < stem.Length; i++)
            {
                char c = stem[i];
                if (c == '_' || c == '-') { spaced.Append(' '); continue; }
                if (i > 0 && char.IsUpper(c) && (char.IsLower(stem[i - 1]) || char.IsDigit(stem[i - 1]))) spaced.Append(' ');
                spaced.Append(c);
            }

            string[] words = spaced.ToString().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 1; i < words.Length; i++)
                if (SmallWords.Contains(words[i].ToLowerInvariant())) words[i] = words[i].ToLowerInvariant();

            string title = string.Join(" ", words);
            return title.Length > 0 ? title : "Untitled";
        }
    }
}
