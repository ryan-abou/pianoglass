namespace PianoGlass.Scoring
{
    /// <summary>
    /// Points and accuracy from judgement counts. One place, so the panel, the
    /// attempt file and per-bar stats can never disagree on what 87% means.
    /// </summary>
    public static class AccuracyCalculator
    {
        public const int PerfectPoints = 100, GreatPoints = 70, GoodPoints = 40;

        public static int Points(Judgement j) => j switch
        {
            Judgement.Perfect => PerfectPoints,
            Judgement.Great   => GreatPoints,
            Judgement.Good    => GoodPoints,
            Judgement.Correct => PerfectPoints,
            _                 => 0,
        };

        public static int Score(int perfect, int great, int good) =>
            perfect * PerfectPoints + great * GreatPoints + good * GoodPoints;

        /// <summary>Percent, 0-100: points earned over points possible for the
        /// notes judged so far. 0 when nothing has been judged.</summary>
        public static double Accuracy(int perfect, int great, int good, int miss)
        {
            int judged = perfect + great + good + miss;
            if (judged == 0) return 0.0;
            return 100.0 * Score(perfect, great, good) / (judged * (double)PerfectPoints);
        }

        /// <summary>Wait mode (F22): nothing can be late or missed there, so
        /// accuracy is the right keys out of every key pressed.</summary>
        public static double PitchAccuracy(int correct, int wrong)
        {
            int presses = correct + wrong;
            return presses == 0 ? 0.0 : 100.0 * correct / presses;
        }
    }
}
