using System.Collections.Generic;

namespace PianoGlass.Scoring
{
    /// <summary>One hand's share of a both-hands attempt (#6).</summary>
    public sealed class HandResult
    {
        /// <summary>"left" or "right".</summary>
        public string Hand;
        public int Notes;
        public int Missed;
        /// <summary>Wrong keys pressed when one of this hand's notes was due.</summary>
        public int Wrong;
        /// <summary>Percent, 0-100, worked out the same way as the attempt's.</summary>
        public double Accuracy;
        /// <summary>Missed plus wrong: what "this hand made the mistakes" counts.</summary>
        public int Mistakes => Missed + Wrong;
    }

    /// <summary>
    /// Which hand made the mistakes in a both-hands attempt (#6: "so I can
    /// tell if my left or right hand is the problem"). From the attempt file
    /// alone, so it is pure and tested. Notes both hands share, and extra
    /// presses with nothing due, belong to neither hand. Null for a one-hand
    /// attempt, a song without hands, or an attempt where a hand had no notes.
    /// </summary>
    public static class HandSplit
    {
        /// <summary>A hand this far below the other, and under
        /// <see cref="SuggestionPicker.WeakBarBelow"/>, gets practised alone.</summary>
        public const double WeakHandGap = 15.0;
        /// <summary>Fewer mistakes than this in the weaker hand says nothing.</summary>
        public const int WeakHandMinMistakes = 3;

        /// <returns>Left, then right; or null.</returns>
        public static HandResult[] From(AttemptRecord rec)
        {
            if (rec == null || rec.notes == null || (rec.hand ?? AttemptRecord.BothHands) != AttemptRecord.BothHands)
                return null;
            var left = new HandResult { Hand = AttemptHands.Left };
            var right = new HandResult { Hand = AttemptHands.Right };
            var counts = new Dictionary<HandResult, int[]>
            {
                // perfect, great, good, miss, correct
                [left] = new int[5], [right] = new int[5],
            };

            foreach (JudgementResult n in rec.notes)
            {
                HandResult h = n.hand == AttemptHands.Left ? left : n.hand == AttemptHands.Right ? right : null;
                if (h == null) continue;
                h.Notes++;
                int[] c = counts[h];
                switch (n.judgement)
                {
                    case Judgement.Perfect: c[0]++; break;
                    case Judgement.Great:   c[1]++; break;
                    case Judgement.Good:    c[2]++; break;
                    case Judgement.Correct: c[4]++; break;
                    default:                c[3]++; h.Missed++; break;
                }
            }
            if (rec.wrongNotes != null)
                foreach (WrongNote w in rec.wrongNotes)
                {
                    if (w.hand == AttemptHands.Left) left.Wrong++;
                    else if (w.hand == AttemptHands.Right) right.Wrong++;
                }
            if (left.Notes == 0 || right.Notes == 0) return null;

            bool wait = rec.mode == AttemptRecord.Wait;
            foreach (HandResult h in new[] { left, right })
            {
                int[] c = counts[h];
                h.Accuracy = wait
                    ? AccuracyCalculator.PitchAccuracy(c[4], h.Wrong)
                    : AccuracyCalculator.Accuracy(c[0], c[1], c[2], c[3]);
            }
            return new[] { left, right };
        }

        /// <summary>The hand to practise alone, or null when both did about as
        /// well: under the weak-bar line, <see cref="WeakHandGap"/> below the
        /// other hand, and with at least <see cref="WeakHandMinMistakes"/> mistakes.</summary>
        public static HandResult Weaker(HandResult[] split)
        {
            if (split == null || split.Length != 2) return null;
            HandResult weak = split[0].Accuracy <= split[1].Accuracy ? split[0] : split[1];
            HandResult strong = weak == split[0] ? split[1] : split[0];
            if (weak.Accuracy >= SuggestionPicker.WeakBarBelow) return null;
            if (strong.Accuracy - weak.Accuracy < WeakHandGap) return null;
            return weak.Mistakes >= WeakHandMinMistakes ? weak : null;
        }
    }
}
