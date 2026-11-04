using System;
using System.Collections.Generic;
using PianoGlass.Song;
using UnityEngine;

namespace PianoGlass.Scoring
{
    /// <summary>One cell of the bar heat strip (F32).</summary>
    public readonly struct BarCell
    {
        public readonly int Bar;
        /// <summary>0-100; meaningless when <see cref="HasNotes"/> is false.</summary>
        public readonly double Accuracy;
        public readonly bool HasNotes;
        public readonly bool IsWorst;
        public readonly Color Colour;

        public BarCell(int bar, double accuracy, bool hasNotes, bool isWorst, Color colour)
        {
            Bar = bar; Accuracy = accuracy; HasNotes = hasNotes; IsWorst = isWorst; Colour = colour;
        }
    }

    /// <summary>
    /// Everything the results card (F30) and its bar heat strip (F32) show,
    /// worked out from attempt records alone -- the same JSON the headset
    /// keeps -- so it is pure and tested, and the card is only layout.
    ///
    /// What it says, and why:
    ///   * Headline accuracy and a one-word verdict.
    ///   * Change since the last attempt at the same song, tempo and mode
    ///     (and loop range): comparing a 70% run with a 100% one says
    ///     nothing. Completed attempts only -- an abandoned one is a restart,
    ///     not a result.
    ///   * Timing as an arrow plus milliseconds ("▼ 37 ms late"), never as a
    ///     colour alone: ▼ late (dragging behind), ▲ early (rushing ahead).
    ///   * Wrong notes, the feedback a video cannot give.
    ///   * One cell per bar on a coral-amber-mint ramp, worst bar outlined
    ///     and also named in words.
    /// </summary>
    public sealed class ResultsSummary
    {
        /// <summary>Mean timing within this of zero reads as "on time".</summary>
        public const double OnTimeMs = 8.0;

        public static readonly Color RampLow = OutcomeTint.Wrong;                       // coral, 0%
        public static readonly Color RampMid = new Color(0xF2 / 255f, 0xC1 / 255f, 0x4E / 255f, 1f); // amber, 70%
        public static readonly Color RampHigh = OutcomeTint.Hit;                        // mint, 100%
        public const double RampMidAccuracy = 70.0;

        public double Accuracy { get; private set; }
        public string AccuracyText { get; private set; }
        public string Verdict { get; private set; }
        /// <summary>Accuracy minus the previous comparable attempt's; null if none.</summary>
        public double? Change { get; private set; }
        public string ChangeText { get; private set; }
        public double MeanErrorMs { get; private set; }
        public string TimingText { get; private set; }
        public int WrongNotes { get; private set; }
        public string WrongNotesText { get; private set; }
        public string ContextText { get; private set; }
        public IReadOnlyList<BarCell> Bars { get; private set; }
        /// <summary>-1 when no bar has a note.</summary>
        public int WorstBar { get; private set; } = -1;
        public string WorstBarText { get; private set; }

        /// <param name="previous">Earlier attempts, in any order and of any
        /// song, tempo or outcome; the comparable latest one is picked here.
        /// <paramref name="current"/> itself may be among them.</param>
        public static ResultsSummary From(AttemptRecord current, IEnumerable<AttemptRecord> previous = null)
        {
            if (current == null) throw new ArgumentNullException(nameof(current));
            var s = new ResultsSummary();

            s.Accuracy = current.accuracy;
            s.AccuracyText = $"{Math.Round(current.accuracy):0}%";
            s.Verdict = VerdictFor(current.accuracy);

            AttemptRecord last = LastComparable(current, previous);
            if (last != null)
            {
                s.Change = current.accuracy - last.accuracy;
                long d = (long)Math.Round(s.Change.Value);
                s.ChangeText = d > 0 ? $"▲ {d}% vs last" : d < 0 ? $"▼ {-d}% vs last" : "same as last";
            }
            else
            {
                s.ChangeText = "first at this tempo";
            }

            s.MeanErrorMs = current.meanErrorMs;
            s.TimingText = TimingFor(current.meanErrorMs, current.notesHit);

            s.WrongNotes = current.wrongPitch + current.extra;
            s.WrongNotesText = s.WrongNotes == 0 ? "no wrong notes"
                             : s.WrongNotes == 1 ? "1 wrong note" : $"{s.WrongNotes} wrong notes";

            s.ContextText = $"{current.song} · {TempoStep.Label((float)current.tempo)}";

            s.WorstBar = WorstOf(current.bars);
            var cells = new List<BarCell>();
            if (current.bars != null)
            {
                var sorted = new List<BarStats>(current.bars);
                sorted.Sort((a, b) => a.bar.CompareTo(b.bar));
                foreach (BarStats b in sorted)
                {
                    bool has = b.notes > 0;
                    cells.Add(new BarCell(b.bar, b.accuracy, has, has && b.bar == s.WorstBar,
                                          has ? Ramp(b.accuracy) : OutcomeTint.Missed));
                }
            }
            s.Bars = cells;

            if (s.WorstBar >= 0)
            {
                BarStats w = current.bars.Find(b => b.bar == s.WorstBar);
                s.WorstBarText = $"weakest: bar {w.bar} ({Math.Round(w.accuracy):0}%)";
            }
            return s;
        }

        /// <summary>Provisional, like the colours: to tune once more people
        /// have played.</summary>
        public static string VerdictFor(double accuracy) =>
            accuracy >= 95 ? "Excellent" :
            accuracy >= 85 ? "Great" :
            accuracy >= 70 ? "Good" :
            accuracy >= 50 ? "Getting there" : "Keep practising";

        /// <summary>"▼ 37 ms late", "▲ 12 ms early", "on time (±3 ms)".
        /// Empty when nothing was pressed.</summary>
        public static string TimingFor(double meanErrorMs, int notesPressed)
        {
            if (notesPressed <= 0) return "";
            long ms = (long)Math.Round(Math.Abs(meanErrorMs));
            if (Math.Abs(meanErrorMs) < OnTimeMs) return $"on time (±{ms} ms)";
            return meanErrorMs > 0 ? $"▼ {ms} ms late" : $"▲ {ms} ms early";
        }

        /// <summary>The score ramp: coral at 0%, amber at 70%, mint at 100%.</summary>
        public static Color Ramp(double accuracy)
        {
            double a = Math.Max(0.0, Math.Min(100.0, accuracy));
            if (a <= RampMidAccuracy) return Color.Lerp(RampLow, RampMid, (float)(a / RampMidAccuracy));
            return Color.Lerp(RampMid, RampHigh, (float)((a - RampMidAccuracy) / (100.0 - RampMidAccuracy)));
        }

        /// <summary>Lowest-accuracy bar that has notes; the earliest on a tie. -1 if none.</summary>
        public static int WorstOf(IEnumerable<BarStats> bars)
        {
            if (bars == null) return -1;
            BarStats worst = null;
            foreach (BarStats b in bars)
            {
                if (b == null || b.notes <= 0) continue;
                if (worst == null || b.accuracy < worst.accuracy ||
                    (b.accuracy == worst.accuracy && b.bar < worst.bar))
                    worst = b;
            }
            return worst != null ? worst.bar : -1;
        }

        /// <summary>Same song, tempo (to the 5% step), mode and loop range;
        /// completed; started before <paramref name="current"/>. The latest
        /// such, by startedUtc (ISO 8601, so it sorts as text).</summary>
        public static AttemptRecord LastComparable(AttemptRecord current, IEnumerable<AttemptRecord> previous)
        {
            if (previous == null) return null;
            AttemptRecord best = null;
            foreach (AttemptRecord p in previous)
            {
                if (p == null || ReferenceEquals(p, current)) continue;
                if (p.outcome != AttemptRecord.Completed) continue;
                if (p.song != current.song) continue;
                if (TempoStep.ToPercent((float)p.tempo) != TempoStep.ToPercent((float)current.tempo)) continue;
                if ((p.mode ?? AttemptRecord.Continuous) != (current.mode ?? AttemptRecord.Continuous)) continue;
                if (p.loopFromBar != current.loopFromBar || p.loopToBar != current.loopToBar) continue;
                if (string.CompareOrdinal(p.startedUtc, current.startedUtc) >= 0) continue;
                if (best == null || string.CompareOrdinal(p.startedUtc, best.startedUtc) > 0) best = p;
            }
            return best;
        }
    }
}
