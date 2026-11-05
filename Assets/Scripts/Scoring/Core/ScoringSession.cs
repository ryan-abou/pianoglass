using System;
using System.Collections.Generic;
using PianoGlass.Song;

namespace PianoGlass.Scoring
{
    /// <summary>
    /// One attempt at one song: matches key presses to expected notes and
    /// keeps the running tally. Pure -- times in, results out -- so every rule
    /// below is tested headlessly. ScoreKeeper is the Unity shell that feeds
    /// it MIDI presses (already converted to song time) and the song clock.
    ///
    /// The rules:
    ///   * A press matches the EARLIEST unjudged note of the same pitch whose
    ///     time is within the GOOD window. One press judges one note, so a
    ///     key held through two repeated notes earns the second a MISS.
    ///   * A press that matches nothing is a wrong note: "wrong-pitch" if
    ///     another note was due within the window (the nearest one is named),
    ///     "extra" otherwise. A wrong note never judges the due note -- that
    ///     note can still be hit with the right key, or misses on its own.
    ///   * A note is a MISS once the song is past its GOOD window unpressed.
    ///   * Chords need nothing special: each note is matched on its own.
    ///   * Windows are real milliseconds; the tempo converts them to song
    ///     time, so half-speed practice is judged on the player's real timing.
    /// </summary>
    public sealed class ScoringSession
    {
        public SongData Song { get; }
        public TimingWindows Windows { get; }

        public int Perfect { get; private set; }
        public int Great { get; private set; }
        public int Good { get; private set; }
        public int Miss { get; private set; }
        public int WrongPitchCount { get; private set; }
        public int ExtraCount { get; private set; }

        public int NotesHit => Perfect + Great + Good;
        public int NotesMissed => Miss;
        public int Score => AccuracyCalculator.Score(Perfect, Great, Good);
        public double Accuracy => AccuracyCalculator.Accuracy(Perfect, Great, Good, Miss);

        /// <summary>Notes this keyboard can play; the ones it cannot are not scored.</summary>
        public int ScorableCount => _scorable.Count;
        public int JudgedCount => _results.Count;
        public bool IsComplete => _results.Count == _scorable.Count;

        public IReadOnlyList<JudgementResult> Results => _results;
        public IReadOnlyList<WrongNote> WrongNotes => _wrong;
        public JudgementResult LastResult { get; private set; }
        public WrongNote LastWrongNote { get; private set; }

        /// <summary>The slowest tempo this attempt was played at, from every
        /// OnNoteOn and Advance; +inf before either. Tempo can change while
        /// paused part-way (F26), and the attempt then records this one: it
        /// was not done at full speed until it was done at full speed
        /// throughout.</summary>
        public double LowestTempo { get; private set; } = double.PositiveInfinity;

        /// <summary>Song time of the first scorable note, or +inf if none.</summary>
        public double FirstNoteTime { get; }

        public event Action<JudgementResult> Judged;
        public event Action<WrongNote> WrongNotePlayed;

        /// <summary>Indices into Song.Notes, sorted by start time.</summary>
        private readonly List<int> _scorable = new List<int>();
        private readonly bool[] _judged;
        private readonly List<JudgementResult> _results = new List<JudgementResult>();
        private readonly List<WrongNote> _wrong = new List<WrongNote>();

        /// <param name="isPlayable">Whether a pitch is on the player's
        /// keyboard; null means every pitch is.</param>
        /// <param name="scoreNotesFrom">Song time before which notes are not
        /// part of this attempt at all -- neither hit nor missed. For an
        /// attempt that begins part-way through the song (the keys were
        /// confirmed while it was already playing): ScoreKeeper passes "now
        /// minus the GOOD window", so every note still hittable counts and
        /// none that went by unplayable is charged as a MISS.</param>
        /// <param name="scoreNotesUntil">Song time at and after which notes
        /// are not part of this attempt: the end of a practice loop.</param>
        public ScoringSession(SongData song, TimingWindows windows, Func<int, bool> isPlayable = null,
                              double scoreNotesFrom = double.NegativeInfinity,
                              double scoreNotesUntil = double.PositiveInfinity)
        {
            Song = song ?? throw new ArgumentNullException(nameof(song));
            Windows = windows;
            _judged = new bool[song.Notes.Count];

            for (int i = 0; i < song.Notes.Count; i++)
                if ((isPlayable == null || isPlayable(song.Notes[i].Pitch)) &&
                    song.Notes[i].StartTime >= scoreNotesFrom - 1e-9 &&
                    song.Notes[i].StartTime < scoreNotesUntil - 1e-9)
                    _scorable.Add(i);
            _scorable.Sort((a, b) => song.Notes[a].StartTime.CompareTo(song.Notes[b].StartTime));

            FirstNoteTime = _scorable.Count > 0 ? song.Notes[_scorable[0]].StartTime : double.PositiveInfinity;
        }

        /// <summary>A key went down. <paramref name="pressSongTime"/> is when,
        /// in song seconds -- from the press's own timestamp, not the frame
        /// it was noticed in.</summary>
        public void OnNoteOn(int pitch, double pressSongTime, double tempo)
        {
            NoteTempo(tempo);
            double window = NoteJudge.WindowSongSeconds(Windows.goodMs, tempo);

            int match = -1;
            int nearestOther = -1;
            double nearestOtherGap = double.PositiveInfinity;

            foreach (int i in _scorable)
            {
                if (_judged[i]) continue;
                SongNote n = Song.Notes[i];
                double gap = Math.Abs(pressSongTime - n.StartTime);
                if (gap > window) continue;

                if (n.Pitch == pitch)
                {
                    // _scorable is in start order: the first in-window one is the earliest.
                    match = i;
                    break;
                }
                if (gap < nearestOtherGap)
                {
                    nearestOtherGap = gap;
                    nearestOther = i;
                }
            }

            if (match >= 0)
            {
                SongNote n = Song.Notes[match];
                double error = NoteJudge.ErrorMs(pressSongTime, n.StartTime, tempo);
                // Within the GOOD window by construction, so never a Miss here.
                Judgement j = NoteJudge.Judge(error, Windows);
                Record(JudgementResult.Hit(match, n.Pitch, Song.BarOf(n.StartTime), n.StartTime,
                                           pressSongTime, error, j));
                return;
            }

            var wrong = new WrongNote { pressedPitch = pitch, pressTime = pressSongTime };
            if (nearestOther >= 0)
            {
                SongNote due = Song.Notes[nearestOther];
                wrong.kind = WrongNote.WrongPitch;
                wrong.expectedPitch = due.Pitch;
                wrong.expectedNoteIndex = nearestOther;
                wrong.errorMs = NoteJudge.ErrorMs(pressSongTime, due.StartTime, tempo);
                wrong.bar = Song.BarOf(due.StartTime);
                WrongPitchCount++;
            }
            else
            {
                wrong.kind = WrongNote.Extra;
                wrong.bar = Song.BarOf(pressSongTime);
                ExtraCount++;
            }
            _wrong.Add(wrong);
            LastWrongNote = wrong;
            WrongNotePlayed?.Invoke(wrong);
        }

        /// <summary>Every frame while playing: any note now past its GOOD
        /// window without a press is a MISS.</summary>
        public void Advance(double songTime, double tempo)
        {
            NoteTempo(tempo);
            double window = NoteJudge.WindowSongSeconds(Windows.goodMs, tempo);
            foreach (int i in _scorable)
            {
                SongNote n = Song.Notes[i];
                if (n.StartTime + window >= songTime) break; // sorted: none later can be due
                if (_judged[i]) continue;
                Record(JudgementResult.Missed(i, n.Pitch, Song.BarOf(n.StartTime), n.StartTime));
            }
        }

        private void NoteTempo(double tempo)
        {
            if (tempo > 0 && tempo < LowestTempo) LowestTempo = tempo;
        }

        private void Record(JudgementResult r)
        {
            _judged[r.noteIndex] = true;
            _results.Add(r);
            switch (r.judgement)
            {
                case Judgement.Perfect: Perfect++; break;
                case Judgement.Great:   Great++;   break;
                case Judgement.Good:    Good++;    break;
                default:                Miss++;    break;
            }
            LastResult = r;
            Judged?.Invoke(r);
        }

        // ------------------------------------------------------------------
        // Summaries
        // ------------------------------------------------------------------

        /// <summary>Mean and standard deviation of the signed error over
        /// pressed notes, real ms. A steady non-zero mean is latency.</summary>
        public (double mean, double sd, int count) ErrorStats()
        {
            double sum = 0, sumSq = 0;
            int n = 0;
            foreach (JudgementResult r in _results)
            {
                if (!r.pressed) continue;
                sum += r.errorMs;
                sumSq += r.errorMs * r.errorMs;
                n++;
            }
            if (n == 0) return (0, 0, 0);
            double mean = sum / n;
            double variance = Math.Max(0, sumSq / n - mean * mean);
            return (mean, Math.Sqrt(variance), n);
        }

        /// <summary>One entry per bar that has a scorable note, in order.</summary>
        public List<BarStats> Bars()
        {
            var byBar = new SortedDictionary<int, BarStats>();
            BarStats Get(int bar)
            {
                if (!byBar.TryGetValue(bar, out BarStats s)) byBar[bar] = s = new BarStats { bar = bar };
                return s;
            }

            foreach (int i in _scorable) Get(Song.BarOf(Song.Notes[i].StartTime)).notes++;
            foreach (JudgementResult r in _results)
            {
                BarStats s = Get(r.bar);
                switch (r.judgement)
                {
                    case Judgement.Perfect: s.perfect++; break;
                    case Judgement.Great:   s.great++;   break;
                    case Judgement.Good:    s.good++;    break;
                    default:                s.miss++;    break;
                }
            }
            foreach (WrongNote w in _wrong) Get(w.bar).wrongNotes++;
            foreach (BarStats s in byBar.Values) s.accuracy = AccuracyCalculator.Accuracy(s.perfect, s.great, s.good, s.miss);

            return new List<BarStats>(byBar.Values);
        }

        /// <summary>The bar with the lowest accuracy among those fully or
        /// partly judged, ties to the one with more wrong notes; null if none.</summary>
        public BarStats WorstBar()
        {
            BarStats worst = null;
            foreach (BarStats s in Bars())
            {
                if (s.perfect + s.great + s.good + s.miss == 0) continue;
                if (worst == null || s.accuracy < worst.accuracy ||
                    (s.accuracy == worst.accuracy && s.wrongNotes > worst.wrongNotes))
                    worst = s;
            }
            return worst;
        }

        public AttemptRecord ToRecord(string outcome, string startedUtc, string endedUtc,
                                      double tempo, double inputOffsetMs)
        {
            (double mean, double sd, _) = ErrorStats();
            var rec = new AttemptRecord
            {
                song = Song.Title, bpm = Song.Bpm,
                startedUtc = startedUtc, endedUtc = endedUtc, outcome = outcome,
                tempo = tempo, inputOffsetMs = inputOffsetMs, windows = Windows,
                score = Score, accuracy = Accuracy, rank = ResultRank.For(Accuracy),
                bestStreak = StreakCalculator.Best(_results, _wrong),
                perfect = Perfect, great = Great, good = Good, miss = Miss,
                notesHit = NotesHit, notesMissed = NotesMissed,
                wrongPitch = WrongPitchCount, extra = ExtraCount,
                meanErrorMs = mean, errorSdMs = sd,
                wrongNotes = new List<WrongNote>(_wrong),
                bars = Bars(),
            };
            rec.notes.AddRange(_results);
            rec.notes.Sort((a, b) => a.expectedTime != b.expectedTime
                ? a.expectedTime.CompareTo(b.expectedTime)
                : a.pitch.CompareTo(b.pitch));
            return rec;
        }
    }
}
