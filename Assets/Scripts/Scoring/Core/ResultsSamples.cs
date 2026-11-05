using System;
using System.Collections.Generic;

namespace PianoGlass.Scoring
{
    /// <summary>
    /// Made-up attempts for checking the results card's layout without a
    /// headset. They go through the same ResultsSummary a real attempt does,
    /// but their accuracy is typed in, not calculated: the 8-bar sample's
    /// 36/8/2/2 would be 88% by AccuracyCalculator, and it says 92%.
    /// </summary>
    public static class ResultsSamples
    {
        public const string EightBars = "8 bars", ThirtyTwoBars = "32 bars", NinetySevenBars = "97 bars",
                            PerfectOtherHand = "Perfect, other hand next", PerfectMastered = "Perfect, mastered";

        public static readonly string[] All = { EightBars, ThirtyTwoBars, NinetySevenBars, PerfectOtherHand, PerfectMastered };

        private const string Song = "Twinkle, Twinkle, Little Star";

        /// <summary>The summary a card shows for a named sample.</summary>
        public static ResultsSummary Summary(string name, bool hasNextSong = false)
        {
            (AttemptRecord current, List<AttemptRecord> previous) = Build(name);
            return ResultsSummary.From(current, previous, hasNextSong);
        }

        public static (AttemptRecord current, List<AttemptRecord> previous) Build(string name)
        {
            switch (name)
            {
                case EightBars:
                {
                    AttemptRecord rec = Record(92, "right", 0.85, 7);
                    rec.bars = Bars(98, 95, 62, 90, 96, 93, 80, 98);
                    Counts(rec, 36, 8, 2, 2);
                    rec.meanErrorMs = 37;
                    rec.wrongPitch = 2;
                    rec.bestStreak = 21;
                    rec.notes = Offsets(0, 1, 1, 2, 3, 4, 7, 10, 11, 5, 2);
                    return (rec, History("right", 0.85, 62, 66, 71, 78, 80, 86));
                }
                case ThirtyTwoBars:
                {
                    // One bad bar among good ones: bars 15-16 share a pill.
                    AttemptRecord rec = Record(84, "left", 0.7, 3);
                    rec.bars = Generated(32, weakBar: 15, weakAccuracy: 58);
                    Counts(rec, 90, 24, 8, 6);
                    rec.meanErrorMs = -22;
                    rec.extra = 4;
                    rec.bestStreak = 37;
                    rec.notes = Offsets(4, 8, 14, 22, 26, 20, 16, 6, 3, 2, 1);
                    return (rec, History("left", 0.7, 79, 86));
                }
                case NinetySevenBars:
                {
                    // Groups of 8, the last pill holding bar 97 alone. The
                    // loop covers the 4-bar phrase, not the 8-bar pill.
                    AttemptRecord rec = Record(78, "both", 1.0, 1);
                    rec.bars = Generated(97, weakBar: 42, weakAccuracy: 49);
                    Counts(rec, 210, 70, 30, 30);
                    rec.meanErrorMs = 6;
                    rec.wrongPitch = 11;
                    rec.extra = 3;
                    rec.bestStreak = 44;
                    rec.notes = Offsets(6, 9, 14, 24, 40, 60, 62, 40, 30, 15, 10);
                    return (rec, new List<AttemptRecord>());
                }
                case PerfectOtherHand:
                {
                    AttemptRecord rec = PerfectRun("right");
                    return (rec, History("right", 1.0, 91, 94, 97));
                }
                case PerfectMastered:
                {
                    AttemptRecord rec = PerfectRun("right");
                    List<AttemptRecord> previous = History("right", 1.0, 94, 97);
                    AttemptRecord otherHand = Record(100, "left", 1.0, 1);
                    previous.Add(otherHand);
                    return (rec, previous);
                }
                default:
                    throw new ArgumentException($"No results sample named '{name}'.", nameof(name));
            }
        }

        private static AttemptRecord PerfectRun(string hand)
        {
            AttemptRecord rec = Record(100, hand, 1.0, 10);
            rec.bars = Bars(100, 100, 100, 100, 100, 100, 100, 100);
            Counts(rec, 28, 0, 0, 0);
            rec.meanErrorMs = 4;
            rec.bestStreak = 28;
            rec.notes = Offsets(0, 0, 0, 0, 1, 5, 14, 6, 2, 0, 0);
            return rec;
        }

        private static AttemptRecord Record(double accuracy, string hand, double tempo, int number) => new AttemptRecord
        {
            song = Song, accuracy = accuracy, hand = hand, tempo = tempo,
            outcome = AttemptRecord.Completed, rank = ResultRank.For(accuracy),
            startedUtc = Started(number),
        };

        /// <summary>Earlier attempts, oldest first, numbered from 1.</summary>
        private static List<AttemptRecord> History(string hand, double tempo, params double[] accuracies)
        {
            var list = new List<AttemptRecord>();
            for (int i = 0; i < accuracies.Length; i++)
                list.Add(Record(accuracies[i], hand, tempo, i + 1));
            return list;
        }

        private static string Started(int number) => $"2026-11-04T10:{number:00}:00Z";

        private static void Counts(AttemptRecord rec, int perfect, int great, int good, int miss)
        {
            rec.perfect = perfect;
            rec.great = great;
            rec.good = good;
            rec.miss = miss;
            rec.notesHit = perfect + great + good;
            rec.notesMissed = miss;
        }

        private static List<BarStats> Bars(params double[] accuracies)
        {
            var list = new List<BarStats>();
            for (int i = 0; i < accuracies.Length; i++)
                list.Add(new BarStats { bar = i + 1, notes = 6, accuracy = accuracies[i] });
            return list;
        }

        /// <summary>A repeatable mix of good bars, with one weak one.</summary>
        private static List<BarStats> Generated(int count, int weakBar, double weakAccuracy)
        {
            double[] pattern = { 98, 93, 96, 88, 91, 99, 86, 94, 97 };
            var values = new double[count];
            for (int i = 0; i < count; i++) values[i] = pattern[i % pattern.Length];
            values[weakBar - 1] = weakAccuracy;
            return Bars(values);
        }

        /// <summary>Pressed notes making the given histogram, one count per
        /// bin from -50 ms to +50 ms.</summary>
        private static List<JudgementResult> Offsets(params int[] bins)
        {
            var notes = new List<JudgementResult>();
            for (int i = 0; i < bins.Length; i++)
                for (int n = 0; n < bins[i]; n++)
                    notes.Add(JudgementResult.Hit(notes.Count, 60, 1, notes.Count * 0.5, notes.Count * 0.5,
                                                  (i - TimingSpread.CentreBin) * TimingSpread.BinMs, Judgement.Good));
            return notes;
        }
    }
}
