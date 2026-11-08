using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace PianoGlass.EditorTools
{
    /// <summary>
    /// Writes Twinkle and Ode to Joy as real .mid files from the note lists
    /// below. The files are committed, so this only has to run again to change
    /// them. The Minuet is not made here: it came from its own source.
    /// </summary>
    public static class StarterSongWriter
    {
        public const string SongsFolder = "Assets/Songs";
        public const int TicksPerBeat = 480;

        private const int G2 = 43, C3 = 48, F3 = 53, G3 = 55;
        private const int C4 = 60, D4 = 62, E4 = 64, F4 = 65, G4 = 67, A4 = 69;

        public readonly struct Note
        {
            public readonly int Pitch;
            public readonly double Beat, Length;

            public Note(int pitch, double beat, double length)
            {
                Pitch = pitch;
                Beat = beat;
                Length = length;
            }
        }

        /// <summary>A tempo that starts at a beat of the song.</summary>
        public readonly struct Tempo
        {
            public readonly double Beat;
            public readonly int Bpm;

            public Tempo(double beat, int bpm)
            {
                Beat = beat;
                Bpm = bpm;
            }
        }

        [MenuItem("PianoGlass/Songs/Write starter songs")]
        public static void WriteAll()
        {
            Directory.CreateDirectory(SongsFolder);
            Write("TwinkleTwinkle.mid", Encode("Twinkle, Twinkle, Little Star", new[] { new Tempo(0.0, 120) }, 4,
                                               ("Right Hand", TwinkleRight()), ("Left Hand", TwinkleLeft())));
            Write("OdeToJoy.mid", Encode("Ode to Joy", new[] { new Tempo(0.0, 120) }, 4,
                                         ("Right Hand", OdeRight()), ("Left Hand", OdeLeft())));
            AssetDatabase.Refresh();
            Debug.Log("[StarterSongs] wrote Twinkle and Ode to Joy");
        }

        private static void Write(string file, byte[] bytes) =>
            File.WriteAllBytes(Path.Combine(SongsFolder, file), bytes);

        /// <summary>One after another, each (pitch, beats long).</summary>
        private static List<Note> Run(params (int pitch, double beats)[] notes)
        {
            var list = new List<Note>(notes.Length);
            double beat = 0.0;
            foreach ((int pitch, double beats) in notes)
            {
                list.Add(new Note(pitch, beat, beats));
                beat += beats;
            }
            return list;
        }

        private static List<Note> TwinkleRight() => Run(
            (C4, 1), (C4, 1), (G4, 1), (G4, 1),
            (A4, 1), (A4, 1), (G4, 2),
            (F4, 1), (F4, 1), (E4, 1), (E4, 1),
            (D4, 1), (D4, 1), (C4, 2),
            (G4, 1), (G4, 1), (F4, 1), (F4, 1),
            (E4, 1), (E4, 1), (D4, 2),
            (G4, 1), (G4, 1), (F4, 1), (F4, 1),
            (E4, 1), (E4, 1), (D4, 2));

        private static List<Note> TwinkleLeft() => Run(
            (C3, 4),
            (F3, 2), (C3, 2),
            (F3, 2), (C3, 2),
            (G3, 2), (C3, 2),
            (C3, 2), (F3, 2),
            (C3, 2), (G3, 2),
            (C3, 2), (F3, 2),
            (C3, 2), (G3, 2));

        private static List<Note> OdeRight() => Run(
            (E4, 1), (E4, 1), (F4, 1), (G4, 1),
            (G4, 1), (F4, 1), (E4, 1), (D4, 1),
            (C4, 1), (C4, 1), (D4, 1), (E4, 1),
            (E4, 1.5), (D4, 0.5), (D4, 2),

            (E4, 1), (E4, 1), (F4, 1), (G4, 1),
            (G4, 1), (F4, 1), (E4, 1), (D4, 1),
            (C4, 1), (C4, 1), (D4, 1), (E4, 1),
            (D4, 1.5), (C4, 0.5), (C4, 2),

            (D4, 1), (D4, 1), (E4, 1), (C4, 1),
            (D4, 1), (E4, 0.5), (F4, 0.5), (E4, 1), (C4, 1),
            (D4, 1), (E4, 0.5), (F4, 0.5), (E4, 1), (D4, 1),
            (C4, 1), (D4, 1), (G3, 2),

            (E4, 1), (E4, 1), (F4, 1), (G4, 1),
            (G4, 1), (F4, 1), (E4, 1), (D4, 1),
            (C4, 1), (C4, 1), (D4, 1), (E4, 1),
            (D4, 1.5), (C4, 0.5), (C4, 2));

        private static List<Note> OdeLeft() => Run(
            (C3, 4), (G2, 4), (C3, 4), (C3, 2), (G2, 2),
            (C3, 4), (G2, 4), (C3, 4), (G2, 2), (C3, 2),
            (G2, 2), (C3, 2), (G2, 2), (C3, 2), (G2, 4), (C3, 2), (G2, 2),
            (C3, 4), (G2, 4), (C3, 4), (G2, 2), (C3, 2));

        /// <summary>A format 1 file: a first track with the title, the tempos
        /// and the time signature, then one named track per staff.</summary>
        public static byte[] Encode(string title, IReadOnlyList<Tempo> tempos, int beatsPerBar,
                                    params (string name, List<Note> notes)[] tracks)
        {
            var file = new List<byte>();
            file.AddRange(Encoding.ASCII.GetBytes("MThd"));
            AddUInt32(file, 6);
            AddUInt16(file, 1);
            AddUInt16(file, tracks.Length + 1);
            AddUInt16(file, TicksPerBeat);

            AddChunk(file, ConductorTrack(title, tempos, beatsPerBar));
            foreach ((string name, List<Note> notes) in tracks) AddChunk(file, NoteTrack(name, notes));
            return file.ToArray();
        }

        private static List<byte> ConductorTrack(string title, IReadOnlyList<Tempo> tempos, int beatsPerBar)
        {
            var events = new List<byte>();
            if (title != null) AddName(events, title);

            AddVariable(events, 0);
            events.AddRange(new byte[] { 0xFF, 0x58, 0x04, (byte)beatsPerBar, 2, 24, 8 });

            long tick = 0;
            foreach (Tempo tempo in tempos)
            {
                long at = Ticks(tempo.Beat);
                int micros = 60000000 / tempo.Bpm;
                AddVariable(events, at - tick);
                events.AddRange(new byte[] { 0xFF, 0x51, 0x03, (byte)(micros >> 16), (byte)(micros >> 8), (byte)micros });
                tick = at;
            }

            AddEnd(events);
            return events;
        }

        private static List<byte> NoteTrack(string name, List<Note> notes)
        {
            var moments = new List<(long tick, bool on, int pitch)>(notes.Count * 2);
            foreach (Note n in notes)
            {
                moments.Add((Ticks(n.Beat), true, n.Pitch));
                moments.Add((Ticks(n.Beat + n.Length), false, n.Pitch));
            }

            // Releases go before presses at the same tick, so a repeated key
            // ends before it starts again.
            moments.Sort((a, b) =>
            {
                int c = a.tick.CompareTo(b.tick);
                if (c != 0) return c;
                if (a.on != b.on) return a.on ? 1 : -1;
                return a.pitch.CompareTo(b.pitch);
            });

            var events = new List<byte>();
            if (name != null) AddName(events, name);

            long tick = 0;
            foreach ((long at, bool on, int pitch) in moments)
            {
                AddVariable(events, at - tick);
                events.Add(on ? (byte)0x90 : (byte)0x80);
                events.Add((byte)pitch);
                events.Add(on ? (byte)80 : (byte)0);
                tick = at;
            }

            AddEnd(events);
            return events;
        }

        private static long Ticks(double beats) => (long)System.Math.Round(beats * TicksPerBeat);

        private static void AddName(List<byte> events, string name)
        {
            byte[] text = Encoding.ASCII.GetBytes(name);
            AddVariable(events, 0);
            events.Add(0xFF);
            events.Add(0x03);
            AddVariable(events, text.Length);
            events.AddRange(text);
        }

        private static void AddEnd(List<byte> events)
        {
            AddVariable(events, 0);
            events.AddRange(new byte[] { 0xFF, 0x2F, 0x00 });
        }

        private static void AddChunk(List<byte> file, List<byte> events)
        {
            file.AddRange(Encoding.ASCII.GetBytes("MTrk"));
            AddUInt32(file, events.Count);
            file.AddRange(events);
        }

        private static void AddUInt16(List<byte> bytes, int value)
        {
            bytes.Add((byte)(value >> 8));
            bytes.Add((byte)value);
        }

        private static void AddUInt32(List<byte> bytes, int value)
        {
            bytes.Add((byte)(value >> 24));
            bytes.Add((byte)(value >> 16));
            bytes.Add((byte)(value >> 8));
            bytes.Add((byte)value);
        }

        private static void AddVariable(List<byte> bytes, long value)
        {
            var groups = new Stack<byte>();
            groups.Push((byte)(value & 0x7F));
            while ((value >>= 7) > 0) groups.Push((byte)(value & 0x7F | 0x80));
            bytes.AddRange(groups);
        }
    }
}
