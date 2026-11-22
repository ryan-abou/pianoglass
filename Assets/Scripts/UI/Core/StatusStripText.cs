using System.Collections.Generic;
using System.Globalization;

namespace PianoGlass.UI
{
    /// <summary>The keys as calibrated, in the numbers the strip shows.</summary>
    public struct StripKeys
    {
        public int KeyCount;
        public string Lowest, Highest;
        public float KeyWidthMm;
        public float SpanCm, ExpectedSpanCm;
        /// <summary>The key width is near a real piano's.</summary>
        public bool Plausible;
        /// <summary>Along the keys (toward higher notes), up, toward the player.</summary>
        public float NudgeRightMm, NudgeUpMm, NudgeNearerMm;
        /// <summary>Negative is turned left, as Settings' "Turn left" does.</summary>
        public float YawDegrees;
    }

    /// <summary>The running score, as the strip shows it during a song.</summary>
    public struct StripScore
    {
        /// <summary>Wait mode: only right or wrong keys, no timing.</summary>
        public bool PitchOnly;
        public int Score;
        public double Accuracy;
        public int Perfect, Great, Good, Miss, Wrong, Correct;
    }

    /// <summary>
    /// The words on the status strip (#37), which replaces the old status
    /// panel's test readout for players: setup steps and problems, the keys'
    /// numbers while setting up, and a game-style score while playing. Rich
    /// text for TextMeshPro. Pure and tested; StatusStrip decides when each
    /// part shows.
    /// </summary>
    public static class StatusStripText
    {
        public const float RealKeyWidthMm = 23.5f;

        public const string Green = "#A9C67C", Blue = "#8FC1E8", Yellow = "#E8D48A", Red = "#E5948A";

        private static readonly CultureInfo En = CultureInfo.InvariantCulture;

        public static string Colour(string text, string colour) => $"<color={colour}>{text}</color>";

        /// <summary>The keys' numbers in plain words, one line each.</summary>
        public static List<string> Keys(StripKeys k)
        {
            var lines = new List<string>
            {
                $"{k.KeyCount} keys, {k.Lowest} to {k.Highest}",
                Colour(string.Format(En, "Key width {0:0.0} mm", k.KeyWidthMm), k.Plausible ? Green : Red) +
                string.Format(En, "   (a real one is {0:0.0})", RealKeyWidthMm),
                string.Format(En, "Keyboard {0:0.0} cm wide   (a real one is {1:0.0})", k.SpanCm, k.ExpectedSpanCm),
            };
            string moved = Moved(k.NudgeRightMm, k.NudgeUpMm, k.NudgeNearerMm);
            if (moved != null) lines.Add(moved);
            string turned = Turned(k.YawDegrees);
            if (turned != null) lines.Add(turned);
            if (!k.Plausible)
                lines.Add(Colour("Those were probably not the lowest and highest keys. Press B to redo.", Red));
            return lines;
        }

        /// <summary>"Moved 2.0 mm right, 1.0 mm up", or null if not moved.</summary>
        public static string Moved(float rightMm, float upMm, float nearerMm)
        {
            var parts = new List<string>();
            Part(parts, rightMm, "right", "left");
            Part(parts, upMm, "up", "down");
            Part(parts, nearerMm, "nearer", "further");
            return parts.Count == 0 ? null : "Moved " + string.Join(", ", parts);
        }

        private static void Part(List<string> parts, float mm, string plus, string minus)
        {
            if (System.Math.Abs(mm) < 0.05f) return;
            parts.Add(string.Format(En, "{0:0.0} mm {1}", System.Math.Abs(mm), mm > 0f ? plus : minus));
        }

        /// <summary>"Turned 0.20° left", or null if not turned.</summary>
        public static string Turned(float yawDegrees)
        {
            if (System.Math.Abs(yawDegrees) < 0.005f) return null;
            return string.Format(En, "Turned {0:0.00}° {1}", System.Math.Abs(yawDegrees), yawDegrees < 0f ? "left" : "right");
        }

        /// <summary>The big line and the counts under it.</summary>
        public static List<string> Score(StripScore s)
        {
            if (s.PitchOnly)
                return new List<string>
                {
                    string.Format(En, "<size=150%><b>{0:0}%</b></size>", s.Accuracy),
                    $"{Colour($"Correct {s.Correct}", Green)}   {Colour($"Wrong {s.Wrong}", Red)}",
                };
            return new List<string>
            {
                string.Format(En, "<size=150%><b>{0:N0}</b></size>   {1:0}%", s.Score, s.Accuracy),
                $"{Colour($"Perfect {s.Perfect}", Green)}   {Colour($"Great {s.Great}", Blue)}   " +
                $"{Colour($"Good {s.Good}", Yellow)}   {Colour($"Miss {s.Miss}", Red)}   {Colour($"Wrong {s.Wrong}", Red)}",
            };
        }

        /// <summary>"PERFECT   C4   12 ms late". <paramref name="errorMs"/>
        /// is positive when late, as the scoring files store it.</summary>
        public static string LastHit(string rating, string note, bool pressed, double errorMs)
        {
            string colour = rating switch
            {
                "perfect" => Green,
                "great" => Blue,
                "good" => Yellow,
                _ => Red,
            };
            string line = $"<b>{Colour(rating.ToUpperInvariant(), colour)}</b>   {note}";
            if (!pressed) return line;
            double ms = System.Math.Round(errorMs);
            if (ms == 0) return line + "   right on time";
            return line + string.Format(En, "   {0:0} ms {1}", System.Math.Abs(ms), ms > 0 ? "late" : "early");
        }

        /// <param name="expected">The note that was due; null if none was.</param>
        /// <param name="notesHidden">Memory fade has hidden the notes: naming
        /// the right one would give it away.</param>
        public static string Wrong(string pressed, string expected, int bar, bool notesHidden)
        {
            string what = notesHidden ? $"Wrong note {pressed}"
                        : expected == null ? $"{pressed}, nothing to play"
                        : $"{pressed} instead of {expected}";
            return Colour($"{what}, bar {bar}", Red);
        }
    }
}
