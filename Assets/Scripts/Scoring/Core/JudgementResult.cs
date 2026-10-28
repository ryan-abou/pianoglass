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

    /// <summary>One play-through, as written to the headset. schema bumps on
    /// any change a reader would trip over.</summary>
    [Serializable]
    public sealed class AttemptRecord
    {
        public const string Completed = "completed";
        public const string Abandoned = "abandoned";

        public int schema = 1;
        public string song;
        public float bpm;
        public string startedUtc;
        public string endedUtc;
        /// <summary>Completed, or Abandoned (restarted part-way).</summary>
        public string outcome;
        public double tempo;
        public double inputOffsetMs;
        public TimingWindows windows;

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
    }
}
