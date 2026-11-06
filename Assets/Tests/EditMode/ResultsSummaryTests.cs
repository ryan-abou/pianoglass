using System.Collections.Generic;
using NUnit.Framework;
using PianoGlass.Scoring;

namespace PianoGlass.Tests.EditMode
{
    /// <summary>The results card's content: ranks, bar pills, the suggestion
    /// row, history and timing, from attempt records alone.</summary>
    public class ResultsSummaryTests
    {
        private const string Twinkle = "Twinkle, Twinkle, Little Star";

        private static AttemptRecord Rec(double accuracy, string started, double tempo = 1.0,
                                         string outcome = AttemptRecord.Completed, string hand = AttemptRecord.BothHands,
                                         string song = Twinkle, string mode = AttemptRecord.Continuous)
        {
            return new AttemptRecord
            {
                song = song, accuracy = accuracy, startedUtc = started, tempo = tempo, hand = hand,
                outcome = outcome, mode = mode, notesHit = 10,
            };
        }

        private static BarStats Bar(int bar, double accuracy, int notes = 4) =>
            new BarStats { bar = bar, accuracy = accuracy, notes = notes };

        private static List<BarStats> Bars(int count, double accuracy = 98.0)
        {
            var list = new List<BarStats>();
            for (int i = 1; i <= count; i++) list.Add(Bar(i, accuracy));
            return list;
        }

        // ------------------------------------------------------------------
        // Rank
        // ------------------------------------------------------------------

        [TestCase(100.0, "SS")]
        [TestCase(95.0, "SS")]
        [TestCase(94.9, "S")]
        [TestCase(90.0, "S")]
        [TestCase(89.9, "A")]
        [TestCase(80.0, "A")]
        [TestCase(79.9, "B")]
        [TestCase(70.0, "B")]
        [TestCase(69.9, "C")]
        [TestCase(60.0, "C")]
        [TestCase(59.9, "D")]
        [TestCase(50.0, "D")]
        [TestCase(49.9, "E")]
        [TestCase(0.0, "E")]
        public void RankThresholds(double accuracy, string rank)
        {
            Assert.AreEqual(rank, ResultRank.For(accuracy));
        }

        [TestCase("E", "D", 50.0)]
        [TestCase("D", "C", 60.0)]
        [TestCase("A", "S", 90.0)]
        [TestCase("S", "SS", 95.0)]
        public void NextRank(string rank, string next, double threshold)
        {
            Assert.IsTrue(ResultRank.TryNext(rank, out string n, out double t));
            Assert.AreEqual(next, n);
            Assert.AreEqual(threshold, t);
        }

        [Test]
        public void TopRankHasNoNext()
        {
            Assert.IsFalse(ResultRank.TryNext("SS", out _, out _));
        }

        [TestCase(94.9, 94)]
        [TestCase(99.6, 99)]
        [TestCase(100.0, 100)]
        [TestCase(91.9999999999, 92)]
        public void DisplayPercentRoundsDown(double accuracy, int shown)
        {
            Assert.AreEqual(shown, ResultRank.DisplayPercent(accuracy));
        }

        [Test]
        public void HeadlineNeverContradictsTheRank()
        {
            ResultsSummary s = ResultsSummary.From(Rec(94.9, "2026-11-04T10:00:00Z"));
            Assert.AreEqual("94%", s.AccuracyText);
            Assert.AreEqual("S", s.Rank);
            Assert.IsFalse(s.IsTopRank);
            Assert.IsTrue(ResultsSummary.From(Rec(95, "2026-11-04T10:00:00Z")).IsTopRank);
        }

        // ------------------------------------------------------------------
        // Bar grouping
        // ------------------------------------------------------------------

        [TestCase(8, 1, 8)]
        [TestCase(10, 1, 10)]
        [TestCase(16, 1, 16)]
        [TestCase(17, 2, 9)]
        [TestCase(32, 2, 16)]
        [TestCase(33, 4, 9)]
        [TestCase(64, 4, 16)]
        [TestCase(65, 8, 9)]
        [TestCase(97, 8, 13)]
        [TestCase(128, 8, 16)]
        public void GroupingRule(int bars, int size, int pills)
        {
            Assert.AreEqual(size, BarGrouping.GroupSize(bars));
            List<BarGroup> groups = BarGrouping.Build(Bars(bars));
            Assert.AreEqual(pills, groups.Count);
            Assert.LessOrEqual(groups.Count, BarGrouping.MaxPills);
        }

        [Test]
        public void VeryLongSongsStillFit()
        {
            Assert.LessOrEqual(BarGrouping.Build(Bars(500)).Count, BarGrouping.MaxPills);
        }

        [Test]
        public void LastPillHoldsTheRemainder()
        {
            List<BarGroup> groups = BarGrouping.Build(Bars(97));
            BarGroup last = groups[groups.Count - 1];
            Assert.AreEqual(97, last.FirstBar);
            Assert.AreEqual(97, last.LastBar);
            Assert.AreEqual(89, groups[groups.Count - 2].FirstBar);
            Assert.AreEqual(96, groups[groups.Count - 2].LastBar);
        }

        [Test]
        public void GroupUsesItsWorstBarNotTheAverage()
        {
            List<BarStats> bars = Bars(32, 99);
            bars[14].accuracy = 40; // bar 15
            List<BarGroup> groups = BarGrouping.Build(bars);
            BarGroup g = groups[7]; // bars 15-16
            Assert.AreEqual(15, g.FirstBar);
            Assert.AreEqual(16, g.LastBar);
            Assert.AreEqual(15, g.WorstBar);
            Assert.AreEqual(40.0, g.WorstAccuracy);
            Assert.IsTrue(g.IsWeakest);
        }

        [Test]
        public void WeakestIsTheLowestEarliestOnATie_AndOnlyOne()
        {
            var bars = new List<BarStats> { Bar(1, 90), Bar(2, 60), Bar(3, 60), Bar(4, 80) };
            List<BarGroup> groups = BarGrouping.Build(bars);
            Assert.IsTrue(groups[1].IsWeakest);
            Assert.IsFalse(groups[0].IsWeakest);
            Assert.IsFalse(groups[2].IsWeakest);
        }

        [Test]
        public void PerfectRunHasNoWeakestGroup()
        {
            foreach (BarGroup g in BarGrouping.Build(Bars(8, 100))) Assert.IsFalse(g.IsWeakest);
        }

        [Test]
        public void BarsWithoutNotesAreEmptyAndNeverWeakest()
        {
            var bars = new List<BarStats> { Bar(1, 90), Bar(3, 95), new BarStats { bar = 2, notes = 0, accuracy = 0 } };
            List<BarGroup> groups = BarGrouping.Build(bars);
            Assert.AreEqual(3, groups.Count);
            Assert.IsFalse(groups[1].HasNotes);
            Assert.IsFalse(groups[1].IsWeakest);
            Assert.IsTrue(groups[0].IsWeakest);
        }

        [TestCase(100.0, 0x3D)]
        [TestCase(95.0, 0x3D)]
        [TestCase(94.9, 0x4F)]
        [TestCase(90.0, 0x4F)]
        [TestCase(89.9, 0x6B)]
        [TestCase(85.0, 0x6B)]
        [TestCase(84.9, 0x9C)]
        [TestCase(75.0, 0x9C)]
        [TestCase(74.9, 0xB4)]
        [TestCase(0.0, 0xB4)]
        public void PillColours(double accuracy, int red)
        {
            Assert.AreEqual(red / 255f, BarGrouping.ColourFor(accuracy).r, 1e-5f);
        }

        // ------------------------------------------------------------------
        // Loop range
        // ------------------------------------------------------------------

        [Test]
        public void GroupOfUpToFourBarsLoopsWhole()
        {
            var g = new BarGroup { FirstBar = 5, LastBar = 8, WorstBar = 7, WorstAccuracy = 50 };
            Assert.AreEqual((5, 8), BarGrouping.LoopRange(g));
        }

        [TestCase(9, 9, 12)]
        [TestCase(10, 9, 12)]
        [TestCase(12, 9, 12)]
        [TestCase(13, 13, 16)]
        [TestCase(16, 13, 16)]
        public void BiggerGroupLoopsThePhraseHoldingTheWorstBar(int worst, int from, int to)
        {
            var g = new BarGroup { FirstBar = 9, LastBar = 16, WorstBar = worst, WorstAccuracy = 50 };
            Assert.AreEqual((from, to), BarGrouping.LoopRange(g));
        }

        [Test]
        public void PhraseStopsAtTheGroupsEnd()
        {
            var g = new BarGroup { FirstBar = 89, LastBar = 94, WorstBar = 93, WorstAccuracy = 50 };
            Assert.AreEqual((93, 94), BarGrouping.LoopRange(g));
        }

        // ------------------------------------------------------------------
        // Suggestions
        // ------------------------------------------------------------------

        private static Suggestion Pick(double accuracy, int tempo, string hand = AttemptRecord.BothHands,
                                       List<BarStats> bars = null, bool otherPerfect = false, bool nextSong = false)
        {
            return SuggestionPicker.Pick(accuracy, tempo, hand, BarGrouping.Build(bars ?? Bars(8)), otherPerfect, nextSong);
        }

        [Test]
        public void A_OneWeakBar()
        {
            List<BarStats> bars = Bars(8);
            bars[2].accuracy = 62;
            Suggestion s = Pick(92, 85, bars: bars);
            Assert.AreEqual(SuggestionRule.WeakBars, s.Rule);
            Assert.AreEqual("Bar 3 needs work", s.Message);
            Assert.AreEqual("Loop bar 3", s.ButtonText);
            Assert.AreEqual(SuggestionAction.LoopBars, s.Action);
            Assert.AreEqual(3, s.FromBar);
            Assert.AreEqual(3, s.ToBar);
        }

        [Test]
        public void A_GroupedBars()
        {
            List<BarStats> bars = Bars(32);
            bars[14].accuracy = 58;
            Suggestion s = Pick(90, 100, bars: bars);
            Assert.AreEqual("Bars 15–16 need work", s.Message);
            Assert.AreEqual("Loop bars 15–16", s.ButtonText);
        }

        [Test]
        public void A_BigGroupCoversTheFourBarPhrase_ButTheOutlineCoversTheGroup()
        {
            List<BarStats> bars = Bars(97);
            bars[41].accuracy = 49; // bar 42, in the pill for 41-48
            Suggestion s = Pick(90, 100, bars: bars);
            Assert.AreEqual("Bars 41–44 need work", s.Message);
            Assert.AreEqual("Loop bars 41–44", s.ButtonText);
            Assert.AreEqual(41, s.FromBar);
            Assert.AreEqual(44, s.ToBar);

            BarGroup weakest = BarGrouping.Build(bars).Find(g => g.IsWeakest);
            Assert.AreEqual(41, weakest.FirstBar);
            Assert.AreEqual(48, weakest.LastBar);
        }

        [Test]
        public void A_NamesTheWeakestWhenSeveralAreBelow()
        {
            List<BarStats> bars = Bars(8);
            bars[1].accuracy = 70;
            bars[5].accuracy = 40;
            Assert.AreEqual("Bar 6 needs work", Pick(80, 100, bars: bars).Message);
        }

        [Test]
        public void A_SeventyFiveIsNotWeak()
        {
            List<BarStats> bars = Bars(8);
            bars[2].accuracy = 75;
            Assert.AreNotEqual(SuggestionRule.WeakBars, Pick(80, 100, bars: bars).Rule);
            bars[2].accuracy = 74.9;
            Assert.AreEqual(SuggestionRule.WeakBars, Pick(80, 100, bars: bars).Rule);
        }

        [Test]
        public void A_ComesBeforeEverything()
        {
            List<BarStats> bars = Bars(8);
            bars[0].accuracy = 10;
            Assert.AreEqual(SuggestionRule.WeakBars, Pick(95, 85, bars: bars).Rule);
        }

        [Test]
        public void B_FasterTempo()
        {
            Suggestion s = Pick(90, 85);
            Assert.AreEqual(SuggestionRule.FasterTempo, s.Rule);
            Assert.AreEqual("Ready for a faster tempo?", s.Message);
            Assert.AreEqual("Try 90% tempo", s.ButtonText);
            Assert.AreEqual(90, s.TargetTempoPercent);
            Assert.AreEqual(SuggestionAction.TryTempo, s.Action);
        }

        [Test]
        public void B_CapsAtFullTempo()
        {
            Assert.AreEqual(100, Pick(95, 95).TargetTempoPercent);
            Assert.AreEqual(100, Pick(95, 98).TargetTempoPercent);
        }

        [Test]
        public void B_NeedsNinetyPercent()
        {
            Assert.AreEqual(SuggestionRule.NextRank, Pick(89.9, 85).Rule);
        }

        [Test]
        public void C_NextRank_NoButton()
        {
            Suggestion s = Pick(82, 100);
            Assert.AreEqual(SuggestionRule.NextRank, s.Rule);
            Assert.AreEqual("Next rank: S at 90%", s.Message);
            Assert.AreEqual(SuggestionAction.None, s.Action);
            Assert.IsNull(s.ButtonText);
            Assert.AreEqual("Next rank: SS at 95%", Pick(94.9, 100).Message);
            Assert.AreEqual("Next rank: D at 50%", Pick(20, 100).Message);
        }

        [Test]
        public void C_AwayFromPerfect_UsesTheDisplayedValue()
        {
            Suggestion s = Pick(96.5, 100);
            Assert.AreEqual(SuggestionRule.AwayFromPerfect, s.Rule);
            Assert.AreEqual("4% away from perfect", s.Message);
            Assert.AreEqual(SuggestionAction.None, s.Action);
            Assert.AreEqual("1% away from perfect", Pick(99.6, 100).Message);
        }

        [Test]
        public void D_OtherHand()
        {
            Suggestion s = Pick(100, 100, AttemptHands.Right);
            Assert.AreEqual(SuggestionRule.OtherHand, s.Rule);
            Assert.AreEqual("Perfect! Now try your left hand", s.Message);
            Assert.AreEqual("Switch to left hand", s.ButtonText);
            Assert.AreEqual("left", s.TargetHand);
            Assert.AreEqual("right", Pick(100, 100, AttemptHands.Left).TargetHand);
        }

        [Test]
        public void D_CountsFasterThanFullTempo()
        {
            Assert.AreEqual(SuggestionRule.OtherHand, Pick(100, 120, AttemptHands.Right).Rule);
        }

        [Test]
        public void E_MasteredHasNoButtonWithoutASongList()
        {
            Suggestion s = Pick(100, 100, AttemptHands.Right, otherPerfect: true);
            Assert.AreEqual(SuggestionRule.Mastered, s.Rule);
            Assert.AreEqual("You've mastered this song ★", s.Message);
            Assert.AreEqual(SuggestionAction.None, s.Action);
            Assert.IsNull(s.ButtonText);
        }

        [Test]
        public void E_OffersNextSongOnceThereIsAList()
        {
            Suggestion s = Pick(100, 100, AttemptHands.Right, otherPerfect: true, nextSong: true);
            Assert.AreEqual(SuggestionAction.NextSong, s.Action);
            Assert.AreEqual("Next song", s.ButtonText);
        }

        [Test]
        public void E_BothHandsPerfectIsMastered()
        {
            Assert.AreEqual(SuggestionRule.Mastered, Pick(100, 100, AttemptRecord.BothHands).Rule);
        }

        [Test]
        public void E_PerfectAboveFullTempoStillSaysSomething()
        {
            Assert.AreEqual(SuggestionRule.Mastered, Pick(100, 200, AttemptRecord.BothHands).Rule);
        }

        [Test]
        public void RowIsNeverEmpty()
        {
            string[] hands = { AttemptRecord.BothHands, AttemptHands.Left, AttemptHands.Right };
            double[] worst = { 10, 74.9, 75, 100 };
            foreach (string hand in hands)
            foreach (double w in worst)
            foreach (bool other in new[] { false, true })
            foreach (bool next in new[] { false, true })
            for (int tempo = 25; tempo <= 200; tempo += 5)
            for (double acc = 0; acc <= 100.0; acc += 0.5)
            {
                List<BarStats> bars = Bars(4, 100);
                bars[1].accuracy = w;
                Suggestion s = Pick(acc, tempo, hand, bars, other, next);
                Assert.IsFalse(string.IsNullOrEmpty(s.Message), $"{hand} {w} {other} {next} {tempo} {acc}");
                Assert.AreEqual(s.Action != SuggestionAction.None, s.ButtonText != null);
            }
        }

        // ------------------------------------------------------------------
        // History
        // ------------------------------------------------------------------

        [Test]
        public void VsLastIsFromExactValues()
        {
            AttemptRecord now = Rec(92.9, "2026-11-04T12:00:00Z");
            var earlier = new[] { Rec(92.1, "2026-11-04T11:00:00Z") };
            ResultsSummary s = ResultsSummary.From(now, earlier);
            Assert.AreEqual(0.8, s.Change.Value, 1e-9);
            Assert.AreEqual("▲ 1% vs last", s.ChangeText);
        }

        [Test]
        public void VsLastSameWhenItRoundsToZero()
        {
            ResultsSummary s = ResultsSummary.From(Rec(92.3, "2026-11-04T12:00:00Z"),
                                                   new[] { Rec(92.1, "2026-11-04T11:00:00Z") });
            Assert.AreEqual("same as last", s.ChangeText);
        }

        [Test]
        public void VsLastDown()
        {
            ResultsSummary s = ResultsSummary.From(Rec(80, "2026-11-04T12:00:00Z"),
                                                   new[] { Rec(86, "2026-11-04T11:00:00Z") });
            Assert.AreEqual("▼ 6% vs last", s.ChangeText);
        }

        [Test]
        public void FirstAttemptHasNoVsLastAndNoNewBest()
        {
            ResultsSummary s = ResultsSummary.From(Rec(99, "2026-11-04T12:00:00Z"));
            Assert.IsNull(s.ChangeText);
            Assert.IsFalse(s.IsNewBest);
            Assert.AreEqual(1, s.AttemptNumber);
        }

        [Test]
        public void OnlyComparableAttemptsCount()
        {
            AttemptRecord now = Rec(90, "2026-11-04T12:00:00Z", tempo: 0.85, hand: AttemptHands.Right);
            var all = new[]
            {
                Rec(70, "2026-11-04T09:00:00Z", 0.85, hand: AttemptHands.Right),                       // counts
                Rec(95, "2026-11-04T10:00:00Z", 0.85, hand: AttemptHands.Left),                        // other hand
                Rec(95, "2026-11-04T10:01:00Z", 1.0, hand: AttemptHands.Right),                        // other tempo
                Rec(95, "2026-11-04T10:02:00Z", 0.85, AttemptRecord.Abandoned, AttemptHands.Right),    // restarted
                Rec(95, "2026-11-04T10:03:00Z", 0.85, hand: AttemptHands.Right, song: "Other"),        // other song
                Rec(95, "2026-11-04T10:04:00Z", 0.85, hand: AttemptHands.Right, mode: AttemptRecord.Wait), // other mode
                Rec(95, "2026-11-04T13:00:00Z", 0.85, hand: AttemptHands.Right),                       // later
                now,
            };

            ResultsSummary s = ResultsSummary.From(now, all);
            Assert.AreEqual(2, s.AttemptNumber);
            Assert.AreEqual(20.0, s.Change.Value, 1e-9);
            Assert.IsTrue(s.IsNewBest);
        }

        [Test]
        public void TyingTheBestIsNotANewBest()
        {
            var earlier = new[] { Rec(90, "2026-11-04T11:00:00Z") };
            Assert.IsFalse(ResultsSummary.From(Rec(90, "2026-11-04T12:00:00Z"), earlier).IsNewBest);
            Assert.IsTrue(ResultsSummary.From(Rec(90.1, "2026-11-04T12:00:00Z"), earlier).IsNewBest);
        }

        [Test]
        public void ChartShowsTheLastFiveOldestFirst()
        {
            var earlier = new List<AttemptRecord>();
            double[] acc = { 60, 66, 71, 78, 80, 86 };
            for (int i = 0; i < acc.Length; i++) earlier.Add(Rec(acc[i], $"2026-11-04T10:{i:00}:00Z"));

            ResultsSummary s = ResultsSummary.From(Rec(92, "2026-11-04T12:00:00Z"), earlier);
            Assert.AreEqual(5, s.Recent.Count);
            Assert.AreEqual("#3", s.Recent[0].Label);
            Assert.AreEqual(71.0, s.Recent[0].Accuracy);
            Assert.AreEqual("#6", s.Recent[3].Label);
            Assert.AreEqual("now", s.Recent[4].Label);
            Assert.IsTrue(s.Recent[4].IsCurrent);
            Assert.IsFalse(s.Recent[3].IsCurrent);
            Assert.AreEqual("▲ 21% over 5 tries", s.TrendText);
            Assert.AreEqual(7, s.AttemptNumber);
        }

        [Test]
        public void OneAttemptHasNoChart()
        {
            ResultsSummary s = ResultsSummary.From(Rec(92, "2026-11-04T12:00:00Z"));
            Assert.AreEqual(1, s.Recent.Count);
            Assert.IsFalse(s.HasChart);
            Assert.IsNull(s.TrendText);
        }

        [Test]
        public void TwoAttemptsShowTwoBars_TrendDown()
        {
            ResultsSummary s = ResultsSummary.From(Rec(80, "2026-11-04T12:00:00Z"),
                                                   new[] { Rec(85, "2026-11-04T11:00:00Z") });
            Assert.AreEqual(2, s.Recent.Count);
            Assert.IsTrue(s.HasChart);
            Assert.AreEqual("▼ 5% over 2 tries", s.TrendText);
        }

        [TestCase(100.0, 1.0)]
        [TestCase(80.0, 0.5)]
        [TestCase(60.0, AttemptHistory.ChartMinHeight)]
        [TestCase(10.0, AttemptHistory.ChartMinHeight)]
        public void ChartHeightScalesFromSixtyToHundred(double accuracy, double height)
        {
            Assert.AreEqual(height, AttemptHistory.ChartHeight(accuracy), 1e-9);
        }

        [Test]
        public void PerfectRunNeedsCompletedFullSpeedSameHand()
        {
            var all = new[]
            {
                Rec(100, "a", 0.95, hand: AttemptHands.Left),
                Rec(99, "b", 1.0, hand: AttemptHands.Left),
                Rec(100, "c", 1.0, AttemptRecord.Abandoned, AttemptHands.Left),
                Rec(100, "d", 1.0, hand: AttemptHands.Right),
            };
            Assert.IsFalse(AttemptHistory.HasPerfectRun(all, Twinkle, AttemptHands.Left));
            Assert.IsTrue(AttemptHistory.HasPerfectRun(all, Twinkle, AttemptHands.Right));
            Assert.IsTrue(AttemptHistory.HasPerfectRun(new[] { Rec(100, "e", 1.2, hand: AttemptHands.Left) }, Twinkle, AttemptHands.Left));
        }

        // ------------------------------------------------------------------
        // Timing
        // ------------------------------------------------------------------

        [TestCase(37.2, 10, "37 ms late")]
        [TestCase(-18.0, 10, "18 ms early")]
        [TestCase(10.0, 10, "on beat")]
        [TestCase(-10.0, 10, "on beat")]
        [TestCase(10.1, 10, "10 ms late")]
        [TestCase(-10.6, 10, "11 ms early")]
        [TestCase(0.0, 0, "–")]
        public void TimingText(double mean, int pressed, string text)
        {
            Assert.AreEqual(text, TimingSpread.Text(mean, pressed));
        }

        private static JudgementResult Hit(double errorMs, double time = 0) =>
            JudgementResult.Hit(0, 60, 1, time, time + errorMs / 1000.0, errorMs, Judgement.Good);

        [Test]
        public void HistogramBins()
        {
            var notes = new List<JudgementResult>
            {
                Hit(-400), Hit(-50), Hit(-46), Hit(-44), Hit(-4), Hit(0), Hit(4.9), Hit(6), Hit(44), Hit(50), Hit(120),
                JudgementResult.Missed(1, 60, 1, 5.0),
            };
            int[] bins = TimingSpread.Histogram(notes);
            Assert.AreEqual(11, bins.Length);
            Assert.AreEqual(3, bins[0], "-400, -50, -46");
            Assert.AreEqual(1, bins[1], "-44");
            Assert.AreEqual(3, bins[5], "-4, 0, 4.9");
            Assert.AreEqual(1, bins[6], "6");
            Assert.AreEqual(1, bins[9], "44");
            Assert.AreEqual(2, bins[10], "50, 120");
        }

        [Test]
        public void HistogramIgnoresMisses()
        {
            int[] bins = TimingSpread.Histogram(new[] { JudgementResult.Missed(1, 60, 1, 5.0) });
            foreach (int b in bins) Assert.AreEqual(0, b);
        }

        // ------------------------------------------------------------------
        // Streak
        // ------------------------------------------------------------------

        [Test]
        public void StreakBreaksOnAMissAndOnAWrongKey()
        {
            var notes = new List<JudgementResult>
            {
                Hit(0, 1), Hit(0, 2),
                JudgementResult.Missed(2, 60, 1, 3),
                Hit(0, 4), Hit(0, 5), Hit(0, 6), Hit(0, 7),
                Hit(0, 9),
            };
            var wrong = new List<WrongNote> { new WrongNote { kind = WrongNote.Extra, pressTime = 7.5 } };
            Assert.AreEqual(4, StreakCalculator.Best(notes, wrong));
            Assert.AreEqual(5, StreakCalculator.Best(notes, null));
            Assert.AreEqual(0, StreakCalculator.Best(null, null));
        }

        [Test]
        public void SessionRecordsRankAndStreak()
        {
            var song = PianoGlass.Song.SongData.TwinkleTwinkleLittleStar();
            var session = new ScoringSession(song, TimingWindows.Default);
            for (int i = 0; i < 3; i++) session.OnNoteOn(song.Notes[i].Pitch, song.Notes[i].StartTime, 1.0);
            AttemptRecord rec = session.ToRecord(AttemptRecord.Abandoned, "s", "e", 1.0, 0);
            Assert.AreEqual(3, rec.bestStreak);
            Assert.AreEqual("SS", rec.rank);
            Assert.AreEqual(AttemptRecord.CurrentSchema, rec.schema);
        }

        [Test]
        public void OlderFilesGetRankAndStreakWorkedOut()
        {
            var old = new AttemptRecord { schema = 2, accuracy = 82, rank = null, bestStreak = 0 };
            old.notes.Add(Hit(0, 1));
            old.notes.Add(Hit(0, 2));
            AttemptRecord back = AttemptRecord.Parse(UnityEngine.JsonUtility.ToJson(old));
            Assert.AreEqual("A", back.rank);
            Assert.AreEqual(2, back.bestStreak);
        }

        // ------------------------------------------------------------------
        // Samples
        // ------------------------------------------------------------------

        [Test]
        public void EightBarSampleIsAsSpecified()
        {
            ResultsSummary s = ResultsSamples.Summary(ResultsSamples.EightBars);
            Assert.AreEqual("92%", s.AccuracyText);
            Assert.AreEqual("S", s.Rank);
            Assert.AreEqual("Right hand · Tempo 85% · Attempt 7", s.SubLine);
            Assert.AreEqual("37 ms late", s.TimingText);
            Assert.AreEqual(2, s.WrongNotes);
            Assert.AreEqual("21 notes", s.StreakText);
            Assert.AreEqual(48, s.TotalNotes);
            Assert.AreEqual(8, s.Groups.Count);
            Assert.IsTrue(s.IsNewBest);
            Assert.AreEqual("▲ 6% vs last", s.ChangeText);
            Assert.AreEqual("Bar 3 needs work", s.Suggestion.Message);
            double[] recent = { 71, 78, 80, 86, 92 };
            for (int i = 0; i < 5; i++) Assert.AreEqual(recent[i], s.Recent[i].Accuracy);
        }

        [Test]
        public void GroupingSamples()
        {
            Assert.AreEqual(16, ResultsSamples.Summary(ResultsSamples.ThirtyTwoBars).Groups.Count);
            ResultsSummary big = ResultsSamples.Summary(ResultsSamples.NinetySevenBars);
            Assert.AreEqual(13, big.Groups.Count);
            Assert.AreEqual("Bars 41–44 need work", big.Suggestion.Message);
            Assert.AreEqual("Both hands · Tempo 100% · Attempt 1", big.SubLine);
        }

        [Test]
        public void PerfectSamplesHitRulesDAndE()
        {
            Assert.AreEqual(SuggestionRule.OtherHand, ResultsSamples.Summary(ResultsSamples.PerfectOtherHand).Suggestion.Rule);
            Assert.AreEqual(SuggestionRule.Mastered, ResultsSamples.Summary(ResultsSamples.PerfectMastered).Suggestion.Rule);
        }
    }
}
