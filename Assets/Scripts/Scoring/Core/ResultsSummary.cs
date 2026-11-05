using System;
using System.Collections.Generic;
using PianoGlass.Song;

namespace PianoGlass.Scoring
{
    /// <summary>
    /// Everything the results card and its details panel show, worked out
    /// from attempt records alone -- the same JSON the headset keeps -- so
    /// it is pure and tested, and the card is only layout.
    /// </summary>
    public sealed class ResultsSummary
    {
        public string Title { get; private set; }
        /// <summary>"Right hand · Tempo 85% · Attempt 7".</summary>
        public string SubLine { get; private set; }
        public int AttemptNumber { get; private set; }
        public bool IsNewBest { get; private set; }

        public double Accuracy { get; private set; }
        public string AccuracyText { get; private set; }
        public string Rank { get; private set; }
        public bool IsTopRank => Rank == ResultRank.Top;

        /// <summary>Exact accuracy minus the last comparable attempt's; null
        /// on a first attempt.</summary>
        public double? Change { get; private set; }
        /// <summary>"▲ 6% vs last", "▼ 3% vs last", "same as last"; null on a first attempt.</summary>
        public string ChangeText { get; private set; }

        public string TimingText { get; private set; }
        public int WrongNotes { get; private set; }
        public int BestStreak { get; private set; }
        public string StreakText { get; private set; }

        public IReadOnlyList<BarGroup> Groups { get; private set; }
        public Suggestion Suggestion { get; private set; }

        // Details panel
        public int TotalNotes { get; private set; }
        public int Perfect { get; private set; }
        public int Great { get; private set; }
        public int Good { get; private set; }
        public int Miss { get; private set; }
        /// <summary>Wait mode's hits; the four above stay 0 there.</summary>
        public int Correct { get; private set; }
        public bool IsWaitMode { get; private set; }
        public int[] Histogram { get; private set; }
        /// <summary>"avg 37 ms late", "avg on beat".</summary>
        public string AverageTimingText { get; private set; }
        /// <summary>+1 late, -1 early, 0 on beat.</summary>
        public int TimingDirection { get; private set; }
        public IReadOnlyList<RecentAttempt> Recent { get; private set; }
        /// <summary>"▲ 21% over 5 tries"; null with only one attempt.</summary>
        public string TrendText { get; private set; }
        public double TrendChange { get; private set; }
        public bool HasChart => Recent != null && Recent.Count >= 2;

        /// <param name="previous">Earlier attempts, in any order and of any
        /// song, tempo or outcome; the comparable ones are picked here.
        /// <paramref name="current"/> itself may be among them.</param>
        /// <param name="hasNextSong">A song list exists to move on to.</param>
        public static ResultsSummary From(AttemptRecord current, IEnumerable<AttemptRecord> previous = null,
                                          bool hasNextSong = false)
        {
            if (current == null) throw new ArgumentNullException(nameof(current));
            var s = new ResultsSummary();
            List<AttemptRecord> earlier = AttemptHistory.Earlier(current, previous);
            int tempoPercent = TempoStep.ToPercent((float)current.tempo);

            s.Title = current.song;
            s.AttemptNumber = earlier.Count + 1;
            s.IsWaitMode = current.mode == AttemptRecord.Wait;
            s.SubLine = (s.IsWaitMode ? "Wait mode · " : "") +
                        $"{AttemptHands.Label(current.hand)} · {TempoStep.Label((float)current.tempo)} · Attempt {s.AttemptNumber}";
            s.IsNewBest = AttemptHistory.IsNewBest(current.accuracy, earlier);

            s.Accuracy = current.accuracy;
            s.AccuracyText = $"{ResultRank.DisplayPercent(current.accuracy)}%";
            s.Rank = ResultRank.For(current.accuracy);

            if (earlier.Count > 0)
            {
                s.Change = current.accuracy - earlier[earlier.Count - 1].accuracy;
                string arrow = AttemptHistory.ArrowPercent(s.Change.Value);
                s.ChangeText = arrow != null ? $"{arrow} vs last" : "same as last";
            }

            // Wait mode has no timing: a dash, like an attempt with no presses.
            s.TimingText = TimingSpread.Text(current.meanErrorMs, s.IsWaitMode ? 0 : current.notesHit);
            s.WrongNotes = current.wrongPitch + current.extra;
            s.BestStreak = current.bestStreak;
            s.StreakText = current.bestStreak == 1 ? "1 note" : $"{current.bestStreak} notes";

            s.Groups = BarGrouping.Build(current.bars);
            string other = AttemptHands.Other(current.hand);
            s.Suggestion = SuggestionPicker.Pick(current.accuracy, tempoPercent, current.hand, s.Groups,
                                                 other != null && AttemptHistory.HasPerfectRun(previous, current.song, other),
                                                 hasNextSong, current.mode);

            s.Perfect = current.perfect;
            s.Great = current.great;
            s.Good = current.good;
            s.Miss = current.miss;
            s.Correct = current.correct;
            s.TotalNotes = current.perfect + current.great + current.good + current.miss + current.correct;

            s.Histogram = TimingSpread.Histogram(s.IsWaitMode ? null : current.notes);
            s.AverageTimingText = s.IsWaitMode ? "pitch only" : "avg " + s.TimingText;
            s.TimingDirection = s.IsWaitMode || current.notesHit <= 0 || Math.Abs(current.meanErrorMs) <= TimingSpread.OnBeatMs
                ? 0 : current.meanErrorMs > 0 ? 1 : -1;

            s.Recent = AttemptHistory.Recent(current, earlier);
            if (s.Recent.Count >= 2)
            {
                s.TrendChange = current.accuracy - s.Recent[0].Accuracy;
                string arrow = AttemptHistory.ArrowPercent(s.TrendChange);
                s.TrendText = $"{arrow ?? "same"} over {s.Recent.Count} tries";
            }
            return s;
        }
    }
}
