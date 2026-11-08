using System;
using System.Collections.Generic;
using System.Text;

namespace PianoGlass.Song
{
    /// <summary>A file that can't be read. The message is short enough to show
    /// on a greyed-out row in the song picker.</summary>
    public sealed class MidiFormatException : Exception
    {
        public MidiFormatException(string message) : base(message) { }
    }

    /// <summary>One note in a MIDI file, still counted in ticks.</summary>
    public readonly struct MidiNote
    {
        public readonly int Pitch;
        public readonly int Channel;
        public readonly long StartTick;
        public readonly long EndTick;

        public MidiNote(int pitch, int channel, long startTick, long endTick)
        {
            Pitch = pitch;
            Channel = channel;
            StartTick = startTick;
            EndTick = endTick;
        }
    }

    public readonly struct MidiTempoChange
    {
        public readonly long Tick;
        public readonly int MicrosPerQuarter;

        public MidiTempoChange(long tick, int microsPerQuarter)
        {
            Tick = tick;
            MicrosPerQuarter = microsPerQuarter;
        }
    }

    public readonly struct MidiTimeSignature
    {
        public readonly long Tick;
        public readonly int Numerator;
        public readonly int Denominator;

        public MidiTimeSignature(long tick, int numerator, int denominator)
        {
            Tick = tick;
            Numerator = numerator;
            Denominator = denominator;
        }
    }

    public sealed class MidiTrack
    {
        /// <summary>The track's name, or "" if it has none.</summary>
        public string Name = "";
        /// <summary>In the order the notes start.</summary>
        public readonly List<MidiNote> Notes = new List<MidiNote>();
        /// <summary>The tick of the track's last event.</summary>
        public long EndTick;
    }

    /// <summary>What was in the file, before any decision about hands or seconds.</summary>
    public sealed class MidiFile
    {
        public int Format;
        public int TicksPerBeat;
        public readonly List<MidiTrack> Tracks = new List<MidiTrack>();
        /// <summary>From every track: format 1 keeps these in the first one,
        /// but nothing in the rules says it has to.</summary>
        public readonly List<MidiTempoChange> Tempos = new List<MidiTempoChange>();
        public readonly List<MidiTimeSignature> TimeSignatures = new List<MidiTimeSignature>();
        /// <summary>Notes left out because no 88-key piano has them.</summary>
        public int NotesOutOfRange;
    }

    /// <summary>
    /// Turns the bytes of a standard MIDI file into tracks of notes. Pure, so
    /// it is tested on files typed in byte by byte from the MIDI rules.
    ///
    /// Left out on purpose: velocity, pedal, key signatures, and channel 10
    /// (drums in General MIDI). A bad file throws MidiFormatException with a
    /// reason a player can read, and never anything else.
    /// </summary>
    public static class MidiFileReader
    {
        public const int LowestPianoNote = 21;   // A0
        public const int HighestPianoNote = 108; // C8
        /// <summary>Channel 10, counted from 0 the way the file stores it.</summary>
        public const int DrumChannel = 9;

        private const string CutOff = "File is cut off";

        public static MidiFile Read(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 14 || !HasTag(bytes, 0, "MThd"))
                throw new MidiFormatException("Not a MIDI file");

            long headerLength = ReadUInt32(bytes, 4);
            if (headerLength < 6 || 8 + headerLength > bytes.Length) throw new MidiFormatException(CutOff);

            var file = new MidiFile { Format = ReadUInt16(bytes, 8) };
            int trackCount = ReadUInt16(bytes, 10);
            int division = ReadUInt16(bytes, 12);

            if (file.Format > 1) throw new MidiFormatException($"MIDI format {file.Format} isn't supported");
            if ((division & 0x8000) != 0) throw new MidiFormatException("Uses clock-based timing, not beats");
            if (division == 0) throw new MidiFormatException("Says 0 ticks per beat");
            file.TicksPerBeat = division;

            long position = 8 + headerLength;
            int read = 0;
            while (read < trackCount)
            {
                if (position + 8 > bytes.Length) throw new MidiFormatException(CutOff);
                long length = ReadUInt32(bytes, (int)position + 4);
                long end = position + 8 + length;
                if (end > bytes.Length) throw new MidiFormatException(CutOff);

                // The rules say to skip chunk types you don't know.
                if (HasTag(bytes, (int)position, "MTrk"))
                {
                    file.Tracks.Add(ReadTrack(bytes, (int)position + 8, (int)end, file));
                    read++;
                }
                position = end;
            }
            return file;
        }

        private static MidiTrack ReadTrack(byte[] b, int start, int end, MidiFile file)
        {
            var track = new MidiTrack();
            // Every struck key still waiting for its release, oldest first, so
            // the same key struck twice before a release pairs up in order.
            var open = new Dictionary<int, Queue<int>>();
            int p = start;
            long tick = 0;
            int status = 0;

            while (p < end)
            {
                tick += ReadVariable(b, ref p, end);
                if (p >= end) throw new MidiFormatException(CutOff);

                int first = b[p];
                if (first == 0xFF)
                {
                    p++;
                    if (p >= end) throw new MidiFormatException(CutOff);
                    int type = b[p++];
                    int length = ReadLength(b, ref p, end);
                    if (type == 0x2F) { p = end; status = 0; break; } // end of track
                    ReadMeta(b, p, length, type, tick, track, file);
                    p += length;
                    status = 0;
                    continue;
                }

                if (first == 0xF0 || first == 0xF7)
                {
                    p++;
                    int skip = ReadLength(b, ref p, end);
                    p += skip;
                    status = 0;
                    continue;
                }

                if (first >= 0x80)
                {
                    if (first >= 0xF0) throw new MidiFormatException("Has data that isn't MIDI");
                    status = first;
                    p++;
                }
                else if (status == 0)
                {
                    throw new MidiFormatException("Has data that isn't MIDI");
                }

                int kind = status & 0xF0;
                int channel = status & 0x0F;
                int dataBytes = kind == 0xC0 || kind == 0xD0 ? 1 : 2;
                if (p + dataBytes > end) throw new MidiFormatException(CutOff);
                int a = b[p], v = dataBytes == 2 ? b[p + 1] : 0;
                p += dataBytes;

                if (kind == 0x90 && v > 0)
                    NoteOn(track, file, open, a, channel, tick);
                else if (kind == 0x80 || kind == 0x90) // a note-on with velocity 0 is a note-off
                    NoteOff(track, open, a, channel, tick);
            }

            track.EndTick = tick;

            // A key never released ends where the track does.
            foreach (KeyValuePair<int, Queue<int>> keyQueue in open)
                foreach (int index in keyQueue.Value)
                    track.Notes[index] = WithEnd(track.Notes[index], tick);
            return track;
        }

        private static void NoteOn(MidiTrack track, MidiFile file, Dictionary<int, Queue<int>> open,
                                   int pitch, int channel, long tick)
        {
            if (channel == DrumChannel) return;
            if (pitch < LowestPianoNote || pitch > HighestPianoNote)
            {
                file.NotesOutOfRange++;
                return;
            }

            // Added now and closed later, so the list stays in start order.
            int key = channel << 8 | pitch;
            if (!open.TryGetValue(key, out Queue<int> queue)) open[key] = queue = new Queue<int>();
            queue.Enqueue(track.Notes.Count);
            track.Notes.Add(new MidiNote(pitch, channel, tick, tick));
        }

        private static void NoteOff(MidiTrack track, Dictionary<int, Queue<int>> open,
                                    int pitch, int channel, long tick)
        {
            int key = channel << 8 | pitch;
            if (!open.TryGetValue(key, out Queue<int> queue) || queue.Count == 0) return;
            int index = queue.Dequeue();
            track.Notes[index] = WithEnd(track.Notes[index], tick);
        }

        private static MidiNote WithEnd(MidiNote n, long endTick) =>
            new MidiNote(n.Pitch, n.Channel, n.StartTick, endTick);

        private static void ReadMeta(byte[] b, int p, int length, int type, long tick, MidiTrack track, MidiFile file)
        {
            switch (type)
            {
                case 0x03 when track.Name.Length == 0 && length > 0:
                    track.Name = ReadText(b, p, length);
                    break;
                case 0x51 when length == 3:
                    int micros = b[p] << 16 | b[p + 1] << 8 | b[p + 2];
                    if (micros > 0) file.Tempos.Add(new MidiTempoChange(tick, micros));
                    break;
                case 0x58 when length >= 2 && b[p] > 0 && b[p + 1] <= 7:
                    file.TimeSignatures.Add(new MidiTimeSignature(tick, b[p], 1 << b[p + 1]));
                    break;
            }
        }

        /// <summary>Byte by byte: MIDI text has no set encoding, and IL2CPP
        /// builds can strip the encodings that would guess at one.</summary>
        private static string ReadText(byte[] b, int p, int length)
        {
            var text = new StringBuilder(length);
            for (int i = 0; i < length; i++) text.Append((char)b[p + i]);
            return text.ToString().Trim();
        }

        /// <summary>A number stored 7 bits to a byte, 1 to 4 bytes long, where
        /// the top bit means "there's more".</summary>
        private static int ReadVariable(byte[] b, ref int p, int end)
        {
            int value = 0;
            for (int i = 0; i < 4; i++)
            {
                if (p >= end) throw new MidiFormatException(CutOff);
                int next = b[p++];
                value = value << 7 | next & 0x7F;
                if ((next & 0x80) == 0) return value;
            }
            throw new MidiFormatException("Has a number too long to be MIDI");
        }

        private static int ReadLength(byte[] b, ref int p, int end)
        {
            int length = ReadVariable(b, ref p, end);
            if (p + (long)length > end) throw new MidiFormatException(CutOff);
            return length;
        }

        private static bool HasTag(byte[] b, int p, string tag)
        {
            if (p + tag.Length > b.Length) return false;
            for (int i = 0; i < tag.Length; i++)
                if (b[p + i] != tag[i]) return false;
            return true;
        }

        private static int ReadUInt16(byte[] b, int p) => b[p] << 8 | b[p + 1];

        private static long ReadUInt32(byte[] b, int p) =>
            (long)b[p] << 24 | (long)b[p + 1] << 16 | (long)b[p + 2] << 8 | b[p + 3];
    }
}
