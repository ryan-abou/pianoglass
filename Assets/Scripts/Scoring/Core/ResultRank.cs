using System;
using System.Collections.Generic;

namespace PianoGlass.Scoring
{
    /// <summary>
    /// The rank letter on the results card, from exact accuracy. Display
    /// only: the research data is the accuracy itself. The percentage is
    /// rounded DOWN for display, so "95%" can never sit beside rank S and
    /// "100%" means exactly that.
    /// </summary>
    public static class ResultRank
    {
        public const string Top = "SS";

        private static readonly (string rank, double from)[] Ladder =
        {
            ("SS", 95.0), ("S", 90.0), ("A", 80.0), ("B", 70.0), ("C", 60.0), ("D", 50.0),
        };

        public static string For(double accuracy)
        {
            foreach ((string rank, double from) in Ladder)
                if (accuracy >= from) return rank;
            return "E";
        }

        /// <summary>The rank above <paramref name="rank"/> and the accuracy
        /// that earns it; false at the top.</summary>
        public static bool TryNext(string rank, out string next, out double threshold)
        {
            next = null;
            threshold = 0.0;
            if (rank == Top) return false;
            for (int i = Ladder.Length - 1; i >= 0; i--)
            {
                if (Ladder[i].rank == rank)
                {
                    next = Ladder[i - 1].rank;
                    threshold = Ladder[i - 1].from;
                    return true;
                }
            }
            // E, or anything unrecognised: the lowest rung is next.
            next = Ladder[Ladder.Length - 1].rank;
            threshold = Ladder[Ladder.Length - 1].from;
            return true;
        }

        /// <summary>Whole percent for display, rounded down. The tiny nudge
        /// keeps a value that is really 92 but stored as 91.99999999 at 92.</summary>
        public static int DisplayPercent(double accuracy) =>
            (int)Math.Floor(Math.Max(0.0, Math.Min(100.0, accuracy)) + 1e-9);
    }

    /// <summary>The longest run of hit notes with no miss and no wrong or
    /// extra key in between, in the order they happened.</summary>
    public static class StreakCalculator
    {
        public static int Best(IEnumerable<JudgementResult> notes, IEnumerable<WrongNote> wrongNotes)
        {
            var events = new List<(double time, bool breaks)>();
            if (notes != null)
                foreach (JudgementResult n in notes)
                    if (n != null) events.Add((n.pressed ? n.pressTime : n.expectedTime, !n.IsHit));
            if (wrongNotes != null)
                foreach (WrongNote w in wrongNotes)
                    if (w != null) events.Add((w.pressTime, true));

            events.Sort((a, b) => a.time.CompareTo(b.time));

            int best = 0, run = 0;
            foreach ((_, bool breaks) in events)
            {
                run = breaks ? 0 : run + 1;
                if (run > best) best = run;
            }
            return best;
        }
    }
}
