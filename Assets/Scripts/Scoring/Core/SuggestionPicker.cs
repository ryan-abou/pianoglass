using System.Collections.Generic;

namespace PianoGlass.Scoring
{
    public enum SuggestionRule { WeakBars, FasterTempo, NextRank, AwayFromPerfect, OtherHand, Mastered }
    public enum SuggestionAction { None, LoopBars, TryTempo, SwitchHand, NextSong }

    /// <summary>The one message of the suggestion row and its optional button.</summary>
    public sealed class Suggestion
    {
        public SuggestionRule Rule;
        public string Message;
        public SuggestionAction Action = SuggestionAction.None;
        /// <summary>Null when there is no button.</summary>
        public string ButtonText;
        public int FromBar = -1, ToBar = -1;
        public int TargetTempoPercent = -1;
        public string TargetHand;
    }

    /// <summary>
    /// Always exactly one suggestion: the first rule that applies. Rules read
    /// exact accuracy; only the wording uses the rounded-down display value.
    /// "100% tempo" means 100 or faster, since tempo goes up to 200%: a
    /// perfect run at 120% has nothing faster left to suggest.
    /// </summary>
    public static class SuggestionPicker
    {
        public const int FullTempoPercent = 100;
        public const double WeakBarBelow = 75.0;
        public const double FasterTempoFrom = 90.0;
        public const int TempoStepPercent = 5;

        /// <param name="otherHandPerfect">The other hand has 100% at 100%
        /// tempo on this song. Ignored for a both-hands attempt, which has no
        /// other hand: its perfect run goes straight to "mastered".</param>
        /// <param name="hasNextSong">There is a next song to offer. False
        /// until a song list exists: "mastered" then has no button.</param>
        public static Suggestion Pick(double accuracy, int tempoPercent, string hand,
                                      IReadOnlyList<BarGroup> groups, bool otherHandPerfect, bool hasNextSong)
        {
            // a. A weak bar. The weakest group, the one the strip outlines.
            BarGroup weakest = null;
            if (groups != null)
                foreach (BarGroup g in groups)
                    if (g.IsWeakest) weakest = g;
            if (weakest != null && weakest.WorstAccuracy < WeakBarBelow)
            {
                // One bar in the pill: that bar and one either side (F33), so
                // it is practised going in and coming out. The message still
                // names the bar that needs the work.
                (int from, int to) = weakest.Size == 1
                    ? PianoGlass.Song.LoopRange.AroundBar(weakest.FirstBar, groups[groups.Count - 1].LastBar)
                    : BarGrouping.LoopRange(weakest);
                bool single = from == to;
                return new Suggestion
                {
                    Rule = SuggestionRule.WeakBars,
                    Message = weakest.Size == 1
                        ? $"Bar {weakest.FirstBar} needs work"
                        : $"Bars {from}–{to} need work",
                    Action = SuggestionAction.LoopBars,
                    ButtonText = single ? $"Loop bar {from}" : $"Loop bars {from}–{to}",
                    FromBar = from,
                    ToBar = to,
                };
            }

            // b. Ready to go faster.
            if (accuracy >= FasterTempoFrom && tempoPercent < FullTempoPercent)
            {
                int target = System.Math.Min(tempoPercent + TempoStepPercent, FullTempoPercent);
                return new Suggestion
                {
                    Rule = SuggestionRule.FasterTempo,
                    Message = "Ready for a faster tempo?",
                    Action = SuggestionAction.TryTempo,
                    ButtonText = $"Try {target}% tempo",
                    TargetTempoPercent = target,
                };
            }

            // c. The next rank, or how close to perfect.
            string rank = ResultRank.For(accuracy);
            if (ResultRank.TryNext(rank, out string next, out double threshold))
                return new Suggestion
                {
                    Rule = SuggestionRule.NextRank,
                    Message = $"Next rank: {next} at {threshold:0}%",
                };
            if (accuracy < 100.0)
                return new Suggestion
                {
                    Rule = SuggestionRule.AwayFromPerfect,
                    Message = $"{100 - ResultRank.DisplayPercent(accuracy)}% away from perfect",
                };

            // d. Perfect at full speed, with another hand still to do.
            string other = AttemptHands.Other(hand);
            if (other != null && !otherHandPerfect)
                return new Suggestion
                {
                    Rule = SuggestionRule.OtherHand,
                    Message = $"Perfect! Now try your {other} hand",
                    Action = SuggestionAction.SwitchHand,
                    ButtonText = $"Switch to {other} hand",
                    TargetHand = other,
                };

            // e. Nothing left on this song.
            return new Suggestion
            {
                Rule = SuggestionRule.Mastered,
                Message = "You've mastered this song ★",
                Action = hasNextSong ? SuggestionAction.NextSong : SuggestionAction.None,
                ButtonText = hasNextSong ? "Next song" : null,
            };
        }
    }

    /// <summary>The attempt file's hand values.</summary>
    public static class AttemptHands
    {
        public const string Left = "left", Right = "right";

        /// <summary>"left" and "right" swap; null for "both" or anything else.</summary>
        public static string Other(string hand) => hand == Left ? Right : hand == Right ? Left : null;

        public static string Label(string hand) =>
            hand == Left ? "Left hand" : hand == Right ? "Right hand" : "Both hands";
    }
}
