using System;
using System.Collections.Generic;

namespace PianoGlass.Scoring
{
    /// <summary>
    /// Choosing what to loop by tapping the results card's bar pills. The first
    /// tap picks one pill, a second one makes a range, and a tap after that
    /// starts again. Tapping a lone pill again clears the choice, which puts
    /// the card's own suggestion back.
    /// </summary>
    public sealed class BarSelection
    {
        /// <summary>Pill indexes, in order. -1 with nothing chosen.</summary>
        public int First { get; private set; } = -1;
        public int Last { get; private set; } = -1;

        public bool IsEmpty => First < 0;

        public bool Contains(int index) => !IsEmpty && index >= First && index <= Last;

        public void Clear() => First = Last = -1;

        public void Tap(int index)
        {
            if (IsEmpty || First != Last)
            {
                First = Last = index;
                return;
            }
            if (index == First)
            {
                Clear();
                return;
            }
            Last = Math.Max(First, index);
            First = Math.Min(First, index);
        }

        /// <summary>What the suggestion row shows for this choice. Null with
        /// nothing chosen.</summary>
        public Suggestion ToSuggestion(IReadOnlyList<BarGroup> groups, SuggestionRule rule)
        {
            if (IsEmpty) return null;
            int from = groups[First].FirstBar, to = groups[Last].LastBar;
            bool single = from == to;
            return new Suggestion
            {
                Rule = rule,
                Message = First == Last ? "Tap another bar for a range" : $"Bars {from}–{to} chosen",
                Action = SuggestionAction.LoopBars,
                ButtonText = single ? $"Loop bar {from}" : $"Loop bars {from}–{to}",
                FromBar = from,
                ToBar = to,
            };
        }
    }
}
