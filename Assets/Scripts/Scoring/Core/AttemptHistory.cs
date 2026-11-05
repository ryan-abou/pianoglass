using System;
using System.Collections.Generic;
using PianoGlass.Song;

namespace PianoGlass.Scoring
{
    /// <summary>One bar of the "recent attempts" chart.</summary>
    public sealed class RecentAttempt
    {
        /// <summary>1-based among comparable attempts.</summary>
        public int Number;
        public double Accuracy;
        public bool IsCurrent;
        /// <summary>"#N", or "now" for this attempt.</summary>
        public string Label;
        public string AccuracyText;
        /// <summary>0-1 of the chart's height.</summary>
        public double Height;
    }

    /// <summary>
    /// Everything that compares this attempt with earlier ones, read from
    /// the saved attempt files: "Attempt N", "vs last", "New best" and the
    /// recent attempts chart. Comparable means the same song, hand, tempo
    /// (to the 5% step), mode and loop range, completed, and started earlier.
    /// An abandoned attempt is a restart, not a result.
    /// </summary>
    public static class AttemptHistory
    {
        public const int ChartAttempts = 5;
        /// <summary>The chart's scale runs from here to 100%, so small
        /// improvements show; anything lower sits at the minimum height.</summary>
        public const double ChartFloorAccuracy = 60.0;
        public const double ChartMinHeight = 0.12;

        /// <summary>Earlier comparable attempts, oldest first.</summary>
        public static List<AttemptRecord> Earlier(AttemptRecord current, IEnumerable<AttemptRecord> all)
        {
            var list = new List<AttemptRecord>();
            if (current == null || all == null) return list;
            foreach (AttemptRecord p in all)
            {
                if (p == null || ReferenceEquals(p, current)) continue;
                if (p.outcome != AttemptRecord.Completed) continue;
                if (p.song != current.song) continue;
                if ((p.hand ?? AttemptRecord.BothHands) != (current.hand ?? AttemptRecord.BothHands)) continue;
                if (TempoStep.ToPercent((float)p.tempo) != TempoStep.ToPercent((float)current.tempo)) continue;
                if ((p.mode ?? AttemptRecord.Continuous) != (current.mode ?? AttemptRecord.Continuous)) continue;
                if (p.loopFromBar != current.loopFromBar || p.loopToBar != current.loopToBar) continue;
                if (string.CompareOrdinal(p.startedUtc, current.startedUtc) >= 0) continue;
                list.Add(p);
            }
            list.Sort((a, b) => string.CompareOrdinal(a.startedUtc, b.startedUtc));
            return list;
        }

        /// <summary>Beats the best saved for this song, hand and tempo. A
        /// first attempt has no best to beat.</summary>
        public static bool IsNewBest(double accuracy, IReadOnlyList<AttemptRecord> earlier)
        {
            if (earlier == null || earlier.Count == 0) return false;
            foreach (AttemptRecord p in earlier)
                if (p.accuracy >= accuracy) return false;
            return true;
        }

        /// <summary>The last few attempts, oldest first, ending with this one.</summary>
        public static List<RecentAttempt> Recent(AttemptRecord current, IReadOnlyList<AttemptRecord> earlier)
        {
            var list = new List<RecentAttempt>();
            int count = earlier != null ? earlier.Count : 0;
            for (int i = Math.Max(0, count - (ChartAttempts - 1)); i < count; i++)
                list.Add(Item(i + 1, earlier[i].accuracy, false));
            list.Add(Item(count + 1, current.accuracy, true));
            return list;
        }

        private static RecentAttempt Item(int number, double accuracy, bool isCurrent) => new RecentAttempt
        {
            Number = number,
            Accuracy = accuracy,
            IsCurrent = isCurrent,
            Label = isCurrent ? "now" : $"#{number}",
            AccuracyText = $"{ResultRank.DisplayPercent(accuracy)}%",
            Height = ChartHeight(accuracy),
        };

        public static double ChartHeight(double accuracy)
        {
            double f = (accuracy - ChartFloorAccuracy) / (100.0 - ChartFloorAccuracy);
            return Math.Max(ChartMinHeight, Math.Min(1.0, f));
        }

        /// <summary>"▲ 6%", "▼ 3%", or null when it rounds to nothing. From
        /// exact values.</summary>
        public static string ArrowPercent(double change)
        {
            long d = (long)Math.Round(change, MidpointRounding.AwayFromZero);
            return d > 0 ? $"▲ {d}%" : d < 0 ? $"▼ {-d}%" : null;
        }

        /// <summary>Whether <paramref name="hand"/> has a perfect run at full
        /// speed or faster on this song.</summary>
        public static bool HasPerfectRun(IEnumerable<AttemptRecord> all, string song, string hand)
        {
            if (all == null || hand == null) return false;
            foreach (AttemptRecord p in all)
            {
                if (p == null || p.outcome != AttemptRecord.Completed || p.song != song) continue;
                if ((p.hand ?? AttemptRecord.BothHands) != hand || p.IsLoop) continue;
                if ((p.mode ?? AttemptRecord.Continuous) != AttemptRecord.Continuous) continue;
                if (p.accuracy >= 100.0 && TempoStep.ToPercent((float)p.tempo) >= SuggestionPicker.FullTempoPercent)
                    return true;
            }
            return false;
        }
    }

    /// <summary>Timing wording and the timing-spread histogram.</summary>
    public static class TimingSpread
    {
        public const int Bins = 11;
        public const double BinMs = 10.0;
        public const int CentreBin = Bins / 2;
        /// <summary>Within this of zero, inclusive, reads as "on beat".</summary>
        public const double OnBeatMs = 10.0;

        /// <summary>11 bins of 10 ms centred on -50, -40 ... +50; the outer
        /// ones catch anything beyond. Pressed notes only: a miss has no offset.</summary>
        public static int[] Histogram(IEnumerable<JudgementResult> notes)
        {
            var bins = new int[Bins];
            if (notes == null) return bins;
            foreach (JudgementResult n in notes)
            {
                if (n == null || !n.pressed) continue;
                int i = (int)Math.Round(n.errorMs / BinMs, MidpointRounding.AwayFromZero) + CentreBin;
                bins[Math.Max(0, Math.Min(Bins - 1, i))]++;
            }
            return bins;
        }

        /// <summary>"37 ms late", "12 ms early", "on beat"; a dash when
        /// nothing was pressed.</summary>
        public static string Text(double meanErrorMs, int notesPressed)
        {
            if (notesPressed <= 0) return "–";
            if (Math.Abs(meanErrorMs) <= OnBeatMs) return "on beat";
            long ms = (long)Math.Round(Math.Abs(meanErrorMs), MidpointRounding.AwayFromZero);
            return meanErrorMs > 0 ? $"{ms} ms late" : $"{ms} ms early";
        }
    }
}
