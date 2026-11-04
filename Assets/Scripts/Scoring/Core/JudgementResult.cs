using System;
using System.Collections.Generic;

namespace PianoGlass.Scoring
{
    /// <summary>
    /// What happened to one expected note. Plain serializable data: this is
    /// what the attempt file stores, and what anything later -- per-bar
    /// practice, fading hints, personal bests, XP -- reads. Nothing here
    /// knows about rendering.
    /// </summary>
    [Serializable]
    public sealed class JudgementResult
    {
        /// <summary>Index into SongData.Notes.</summary>
        public int noteIndex;
        public int pitch;
        public int bar;
        /// <summary>When the note was due, song seconds.</summary>
        public double expectedTime;
        /// <summary>False for a MISS that nobody pressed for.</summary>
        public bool pressed;
        /// <summary>When it was pressed, song seconds; 0 when not pressed.</summary>
        public double pressTime;
        /// <summary>Real milliseconds, positive = late; 0 when not pressed.</summary>
        public double errorMs;
        public Judgement judgement;
        /// <summary>The judgement's name, so the file reads without a lookup table.</summary>
        public string rating;

        public bool IsHit => judgement != Judgement.Miss;

        public static JudgementResult Hit(int noteIndex, int pitch, int bar, double expectedTime,
                                          double pressTime, double errorMs, Judgement j) =>
            new JudgementResult
            {
                noteIndex = noteIndex, pitch = pitch, bar = bar, expectedTime = expectedTime,
                pressed = true, pressTime = pressTime, errorMs = errorMs, judgement = j, rating = j.ToString(),
            };

        public static JudgementResult Missed(int noteIndex, int pitch, int bar, double expectedTime) =>
            new JudgementResult
            {
                noteIndex = noteIndex, pitch = pitch, bar = bar, expectedTime = expectedTime,
                judgement = Judgement.Miss, rating = nameof(Judgement.Miss),
            };
    }

    /// <summary>
    /// A press that matched no expected note of its pitch. "wrong-pitch" when
    /// another note was due at that moment (D instead of D#: the feedback the
    /// app exists to give), "extra" when nothing was due at all.
    /// </summary>
    [Serializable]
    public sealed class WrongNote
    {
        public const string WrongPitch = "wrong-pitch";
        public const string Extra = "extra";

        public string kind;
        public int pressedPitch;
        public double pressTime;
        public int bar;
        /// <summary>The note that was due instead; -1 for an extra press.</summary>
        public int expectedPitch = -1;
        public int expectedNoteIndex = -1;
        /// <summary>Real ms from the due note, positive = late; 0 for an extra press.</summary>
        public double errorMs;
    }

    [Serializable]
    public sealed class BarStats
    {
        public int bar;
        public int notes;
        public int perfect, great, good, miss;
        public int wrongNotes;
        /// <summary>Percent, 0-100, over the notes judged in this bar.</summary>
        public double accuracy;
    }

    /// <summary>
    /// One play-through, as written to the headset. schema bumps on any
    /// change a reader would trip over.
    ///
    /// v2 (2026-11-03) adds how the attempt was practised -- mode, loop
    /// range, hand, metronome -- all at once, ahead of the features that set
    /// them, so the results card can compare like with like. Load files with
    /// <see cref="Parse"/>: a v1 file has none of these fields, and every
    /// initializer below is what a v1 attempt actually was (continuous, no
    /// loop, both hands, no metronome). The version stays in the existing
    /// "schema" field: v1 files store it explicitly, so it tells them apart,
    /// where a new field would take its v2 initializer when missing.
    /// </summary>
    [Serializable]
    public sealed class AttemptRecord
    {
        public const string Completed = "completed";
        public const string Abandoned = "abandoned";

        public const int CurrentSchema = 2;
        public const string Continuous = "continuous";
        public const string Wait = "wait";
        public const string BothHands = "both";
        public const int NoLoop = -1;

        public int schema = CurrentSchema;
        public string song;
        public float bpm;
        public string startedUtc;
        public string endedUtc;
        /// <summary>Completed, or Abandoned (restarted part-way).</summary>
        public string outcome;
        /// <summary>The slowest tempo the attempt was played at (tempo can
        /// change while paused part-way).</summary>
        public double tempo;
        public double inputOffsetMs;
        /// <summary>How far ahead the highway was drawn when the attempt was
        /// saved (render-to-display latency, from the runtime); 0 in the editor.</summary>
        public double displayLatencyMs;
        public TimingWindows windows;

        /// <summary>"continuous" or "wait" (F22). Never compared across modes.</summary>
        public string mode = Continuous;
        /// <summary>First and last bar of a loop (F33), inclusive; -1 = no loop.</summary>
        public int loopFromBar = NoLoop;
        public int loopToBar = NoLoop;
        /// <summary>"both" until one-hand practice exists.</summary>
        public string hand = BothHands;
        /// <summary>Metronome (F27) clicking during the attempt.</summary>
        public bool metronome;

        public int score;
        public double accuracy;
        public int perfect, great, good, miss;
        public int notesHit, notesMissed;
        public int wrongPitch, extra;
        /// <summary>Over pressed notes: a consistent non-zero mean is input
        /// latency to subtract, not the player.</summary>
        public double meanErrorMs;
        public double errorSdMs;

        public List<JudgementResult> notes = new List<JudgementResult>();
        public List<WrongNote> wrongNotes = new List<WrongNote>();
        public List<BarStats> bars = new List<BarStats>();

        public bool IsLoop => loopFromBar != NoLoop && loopToBar != NoLoop;

        /// <summary>Reads an attempt file of any schema so far. Fields a file
        /// lacks keep their v1-meaning defaults; empty strings (a hand-edited
        /// or truncated file) get the same treatment. Null for unreadable JSON.</summary>
        public static AttemptRecord Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            AttemptRecord rec;
            try { rec = UnityEngine.JsonUtility.FromJson<AttemptRecord>(json); }
            catch (ArgumentException) { return null; }
            if (rec == null) return null;

            if (string.IsNullOrEmpty(rec.mode)) rec.mode = Continuous;
            if (string.IsNullOrEmpty(rec.hand)) rec.hand = BothHands;
            if (rec.loopFromBar < 0 || rec.loopToBar < 0) rec.loopFromBar = rec.loopToBar = NoLoop;
            if (rec.notes == null) rec.notes = new List<JudgementResult>();
            if (rec.wrongNotes == null) rec.wrongNotes = new List<WrongNote>();
            if (rec.bars == null) rec.bars = new List<BarStats>();
            return rec;
        }
    }
}
