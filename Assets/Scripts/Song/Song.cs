using System.Collections.Generic;

namespace PianoGlass.Song
{
    /// <summary>One note: which key, when it starts, how long it's held. Seconds
    /// throughout -- SongClock is what those seconds are measured against.</summary>
    public readonly struct SongNote
    {
        public readonly int Pitch;
        public readonly double StartTime;
        public readonly double Duration;

        public SongNote(int pitch, double startTime, double duration)
        {
            Pitch = pitch;
            StartTime = startTime;
            Duration = duration;
        }

        public double EndTime => StartTime + Duration;
    }

    /// <summary>
    /// A song is just a title, a tempo and a list of notes. No file format, no
    /// parser -- MIDI file import is a later problem. Right now the highway,
    /// hit detection and wait-mode only need (pitch, start, duration), and
    /// hand-typing eight bars is faster and has less to get wrong than writing
    /// and debugging a parser this early in the slice.
    ///
    /// Named SongData, not Song: a type with the same name as its own
    /// namespace's last segment (PianoGlass.Song) resolves the wrong way from
    /// a sibling namespace -- C# finds the nested namespace PianoGlass.Song
    /// before it considers "using PianoGlass.Song" for the type, and
    /// "'Song' is a namespace but is used like a type" is what a first build
    /// says about it.
    /// </summary>
    public class SongData
    {
        public string Title { get; }
        public float Bpm { get; }
        public IReadOnlyList<SongNote> Notes { get; }

        /// <summary>When the last note ends. Not just the last entry's EndTime
        /// -- Notes isn't contractually ordered -- so this is the max over all
        /// of them, computed once rather than on every read.</summary>
        public double Duration { get; }

        /// <summary>Where every beat and bar falls. Everything that counts
        /// beats or bars asks this, so tempo changes work everywhere at once.</summary>
        public TempoMap TempoMap { get; }

        /// <summary>Beats in the first bar. Later bars can differ: ask the
        /// tempo map.</summary>
        public int BeatsPerBar { get; }

        /// <summary>Song seconds in the first bar.</summary>
        public double SecondsPerBar => TempoMap.FirstBarSeconds;

        /// <summary>Bars up to the one holding the last note's end. A note
        /// ending exactly on a bar line does not start another bar.</summary>
        public int BarCount => Duration <= 0.0 ? 0 : BarOf(Duration - 1e-6);

        /// <summary>Song time at which a 1-based bar starts.</summary>
        public double BarStartTime(int bar) => TempoMap.BarStartTime(bar);

        /// <summary>One tempo and one time signature for the whole song.</summary>
        public SongData(string title, float bpm, IReadOnlyList<SongNote> notes, int beatsPerBar = 4)
            : this(title, notes, TempoMap.Constant(bpm, beatsPerBar))
        {
        }

        /// <summary>A song with its own tempo map. Bpm is the first tempo.</summary>
        public SongData(string title, IReadOnlyList<SongNote> notes, TempoMap tempoMap)
        {
            Title = title;
            Notes = notes;
            TempoMap = tempoMap;
            Bpm = tempoMap.FirstBpm;
            BeatsPerBar = tempoMap.FirstBeatsPerBar;

            double duration = 0.0;
            foreach (SongNote note in notes)
                if (note.EndTime > duration) duration = note.EndTime;
            Duration = duration;
        }

        /// <summary>1-based bar number of a song time; anything before 0
        /// (the lead-in) is bar 1. "Bar 9 is where you fall apart" is in these
        /// units, so they match how a player counts.</summary>
        public int BarOf(double songTime) => TempoMap.BarOf(songTime);

        // Middle C = 60, scientific pitch notation -- matches MidiInput.NoteName.
        private const int C4 = 60, D4 = 62, E4 = 64, F4 = 65, G4 = 67, A4 = 69;

        /// <summary>
        /// "Twinkle, Twinkle, Little Star" (the tune is "Ah! vous dirai-je,
        /// Maman", public domain), first eight bars, in C major at 120 BPM.
        /// Monophonic and rest-free, so every note starts exactly where the
        /// last one ends and the start times fall straight out of a running
        /// beat count -- no separate rest entries to get wrong.
        /// </summary>
        public static SongData TwinkleTwinkleLittleStar()
        {
            const float bpm = 120f;
            double secondsPerBeat = 60.0 / bpm;

            // (pitch, length in beats). One quarter note = one beat.
            (int pitch, float beats)[] melody =
            {
                (C4, 1), (C4, 1), (G4, 1), (G4, 1),   // bar 1: Twin-kle twin-kle
                (A4, 1), (A4, 1), (G4, 2),             // bar 2: lit-tle star
                (F4, 1), (F4, 1), (E4, 1), (E4, 1),   // bar 3: How I won-der
                (D4, 1), (D4, 1), (C4, 2),             // bar 4: what you are
                (G4, 1), (G4, 1), (F4, 1), (F4, 1),   // bar 5: Up a-bove the
                (E4, 1), (E4, 1), (D4, 2),             // bar 6: world so high
                (G4, 1), (G4, 1), (F4, 1), (F4, 1),   // bar 7: Like a dia-mond
                (E4, 1), (E4, 1), (D4, 2),             // bar 8: in the sky
            };

            var notes = new List<SongNote>(melody.Length);
            double t = 0.0;
            foreach ((int pitch, float beats) in melody)
            {
                double duration = beats * secondsPerBeat;
                notes.Add(new SongNote(pitch, t, duration));
                t += duration;
            }

            return new SongData("Twinkle, Twinkle, Little Star", bpm, notes);
        }
    }
}
