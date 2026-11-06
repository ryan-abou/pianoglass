using PianoGlass.Song;

namespace PianoGlass.Scoring
{
    /// <summary>
    /// What a practice loop's tempo does after a pass (F35), shown on the panel
    /// so it is never a mystery:
    ///
    ///   90% or better    5% faster, up to 100%: the goal is full speed, so it
    ///                    never goes past it, even though a manual tempo can
    ///   under 70%        5% slower, down to 25%
    ///   anything else    stays
    ///
    /// Reads exact accuracy, like every other rule on the results.
    /// </summary>
    public static class AutoTempo
    {
        public const double FasterFrom = 90.0;
        public const double SlowerBelow = 70.0;
        public const float GoalTempo = 1f;

        /// <summary>The tempo for the next pass.</summary>
        public static float Next(float tempo, double accuracy)
        {
            if (accuracy >= FasterFrom) return tempo >= GoalTempo ? tempo : System.Math.Min(TempoStep.Step(tempo, +1), GoalTempo);
            if (accuracy < SlowerBelow) return TempoStep.Step(tempo, -1);
            return tempo;
        }

        /// <summary>
        /// The tempo with memory fade on: one thing gets harder or easier at a
        /// time, so a bad pass says what caused it. A good pass fades the notes
        /// first, and only speeds up once they are at their faintest, which
        /// means learning it slowly and then bringing it up to speed. A bad pass
        /// brings the notes back first, and only slows down once they are full.
        /// </summary>
        public static float NextWithFade(float tempo, double accuracy, MemoryFade fade)
        {
            float next = Next(tempo, accuracy);
            if (next > tempo && !fade.AtFaintest) return tempo;
            if (next < tempo && !fade.AtFullest) return tempo;
            return next;
        }

        /// <summary>One line for the panel: how the pass went, what it did to
        /// the tempo and what it would take to change that.</summary>
        public static string Describe(float before, float after, double accuracy)
        {
            string pass = $"{accuracy:0}%";
            int was = TempoStep.ToPercent(before), now = TempoStep.ToPercent(after);
            if (now > was) return $"{pass}: tempo up to {now}%";
            if (now < was) return $"{pass}: tempo down to {now}%";
            if (accuracy >= FasterFrom) return $"{pass}: at full speed";
            if (accuracy < SlowerBelow) return $"{pass}: already at the slowest, {was}%";
            return $"{pass}: tempo stays {was}%, {FasterFrom:0}% speeds up";
        }
    }
}
