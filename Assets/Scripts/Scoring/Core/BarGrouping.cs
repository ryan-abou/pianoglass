using System;
using System.Collections.Generic;
using UnityEngine;

namespace PianoGlass.Scoring
{
    /// <summary>One pill of the bar strip: one or more consecutive bars.</summary>
    public sealed class BarGroup
    {
        public int FirstBar, LastBar;
        /// <summary>The lowest-accuracy bar with notes in the group, earliest
        /// on a tie; -1 when no bar in the group has notes.</summary>
        public int WorstBar = -1;
        public double WorstAccuracy;
        public bool HasNotes => WorstBar >= 0;
        public bool IsWeakest;
        public int Size => LastBar - FirstBar + 1;
    }

    /// <summary>
    /// Fits any song's bars into at most <see cref="MaxPills"/> pills. Each
    /// pill is coloured by the WORST bar in it, not the average, so one bad
    /// bar cannot hide among good ones.
    /// </summary>
    public static class BarGrouping
    {
        public const int MaxPills = 16;
        /// <summary>A loop never covers more than one phrase of this many bars.</summary>
        public const int PhraseBars = 4;

        public static readonly Color Colour95 = Hex(0x3D, 0x9A, 0x70);
        public static readonly Color Colour90 = Hex(0x4F, 0xA0, 0x76);
        public static readonly Color Colour85 = Hex(0x6B, 0x9A, 0x5E);
        public static readonly Color Colour75 = Hex(0x9C, 0x9A, 0x55);
        public static readonly Color ColourLow = Hex(0xB4, 0x7A, 0x42);

        /// <summary>The smallest of 1, 2, 4, 8 that fits; songs of more than
        /// 128 bars keep doubling, so the pill limit always holds.</summary>
        public static int GroupSize(int barCount)
        {
            int size = 1;
            while (barCount > size * MaxPills) size *= 2;
            return size;
        }

        /// <summary>Groups covering bar 1 to the highest bar listed; the last
        /// pill holds the remainder. Bars without an entry or without notes
        /// count as empty, and the weakest group is the one holding the
        /// lowest bar, earliest on a tie. A perfect run has no weakest group:
        /// outlining bar 1 would point at nothing.</summary>
        public static List<BarGroup> Build(IEnumerable<BarStats> bars)
        {
            var byBar = new Dictionary<int, BarStats>();
            int total = 0;
            if (bars != null)
            {
                foreach (BarStats b in bars)
                {
                    if (b == null || b.bar < 1) continue;
                    byBar[b.bar] = b;
                    total = Math.Max(total, b.bar);
                }
            }

            var groups = new List<BarGroup>();
            if (total == 0) return groups;

            int size = GroupSize(total);
            BarGroup weakest = null;
            for (int first = 1; first <= total; first += size)
            {
                var g = new BarGroup { FirstBar = first, LastBar = Math.Min(first + size - 1, total) };
                for (int bar = g.FirstBar; bar <= g.LastBar; bar++)
                {
                    if (!byBar.TryGetValue(bar, out BarStats s) || s.notes <= 0) continue;
                    if (!g.HasNotes || s.accuracy < g.WorstAccuracy)
                    {
                        g.WorstBar = bar;
                        g.WorstAccuracy = s.accuracy;
                    }
                }
                groups.Add(g);
                if (g.HasNotes && (weakest == null || g.WorstAccuracy < weakest.WorstAccuracy)) weakest = g;
            }
            if (weakest != null && weakest.WorstAccuracy < 100.0) weakest.IsWeakest = true;
            return groups;
        }

        public static Color ColourFor(double worstAccuracy) =>
            worstAccuracy >= 95.0 ? Colour95 :
            worstAccuracy >= 90.0 ? Colour90 :
            worstAccuracy >= 85.0 ? Colour85 :
            worstAccuracy >= 75.0 ? Colour75 : ColourLow;

        /// <summary>What to loop for a weak group: the group itself, or, when
        /// it is bigger than a phrase, the 4-bar phrase (counted from bar 1)
        /// holding its worst bar, cut off at the group's end.</summary>
        public static (int from, int to) LoopRange(BarGroup group)
        {
            if (group.Size <= PhraseBars || !group.HasNotes) return (group.FirstBar, group.LastBar);
            int from = (group.WorstBar - 1) / PhraseBars * PhraseBars + 1;
            return (from, Math.Min(from + PhraseBars - 1, group.LastBar));
        }

        private static Color Hex(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f, 1f);
    }
}
