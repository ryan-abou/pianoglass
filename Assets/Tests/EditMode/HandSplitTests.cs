using System.Collections.Generic;
using NUnit.Framework;
using PianoGlass.Scoring;

namespace PianoGlass.Tests.EditMode
{
    public class HandSplitTests
    {
        private static JudgementResult Note(string hand, Judgement j) => new JudgementResult
        {
            hand = hand, judgement = j, rating = j.ToString(), pressed = j != Judgement.Miss,
        };

        /// <summary>A both-hands attempt: the right hand all perfect, the left
        /// hand with <paramref name="leftMissed"/> of its 10 notes missed.</summary>
        private static AttemptRecord Attempt(int leftMissed, int leftWrong = 0, string hand = AttemptRecord.BothHands)
        {
            var rec = new AttemptRecord { hand = hand, wrongNotes = new List<WrongNote>() };
            for (int i = 0; i < 10; i++) rec.notes.Add(Note("right", Judgement.Perfect));
            for (int i = 0; i < 10; i++) rec.notes.Add(Note("left", i < leftMissed ? Judgement.Miss : Judgement.Perfect));
            for (int i = 0; i < leftWrong; i++) rec.wrongNotes.Add(new WrongNote { kind = WrongNote.WrongPitch, hand = "left" });
            rec.wrongNotes.Add(new WrongNote { kind = WrongNote.Extra, hand = "" });
            return rec;
        }

        [Test]
        public void EachHandGetsItsOwnNotesMissesAndWrongKeys()
        {
            HandResult[] split = HandSplit.From(Attempt(leftMissed: 4, leftWrong: 2));
            Assert.IsNotNull(split);
            Assert.AreEqual("left", split[0].Hand);
            Assert.AreEqual(10, split[0].Notes);
            Assert.AreEqual(4, split[0].Missed);
            Assert.AreEqual(2, split[0].Wrong, "the extra press belongs to neither hand");
            Assert.AreEqual(6, split[0].Mistakes);
            Assert.AreEqual(60.0, split[0].Accuracy, 1e-9);
            Assert.AreEqual("right", split[1].Hand);
            Assert.AreEqual(0, split[1].Mistakes);
            Assert.AreEqual(100.0, split[1].Accuracy, 1e-9);
        }

        [Test]
        public void SharedNotesBelongToNeitherHand()
        {
            AttemptRecord rec = Attempt(leftMissed: 0);
            rec.notes.Add(Note("", Judgement.Miss));
            HandResult[] split = HandSplit.From(rec);
            Assert.AreEqual(10, split[0].Notes);
            Assert.AreEqual(10, split[1].Notes);
            Assert.AreEqual(0, split[0].Missed + split[1].Missed);
        }

        [Test]
        public void OneHandAttemptsAndSongsWithoutHandsHaveNoSplit()
        {
            Assert.IsNull(HandSplit.From(Attempt(4, hand: "left")));
            var noHands = new AttemptRecord { hand = AttemptRecord.BothHands };
            noHands.notes.Add(Note("", Judgement.Perfect));
            Assert.IsNull(HandSplit.From(noHands));
            Assert.IsNull(HandSplit.From(null));
        }

        [Test]
        public void WaitModeJudgesEachHandOnPitch()
        {
            var rec = new AttemptRecord { hand = AttemptRecord.BothHands, mode = AttemptRecord.Wait, wrongNotes = new List<WrongNote>() };
            for (int i = 0; i < 4; i++) rec.notes.Add(Note("left", Judgement.Correct));
            for (int i = 0; i < 4; i++) rec.notes.Add(Note("right", Judgement.Correct));
            for (int i = 0; i < 4; i++) rec.wrongNotes.Add(new WrongNote { kind = WrongNote.WrongPitch, hand = "right" });
            HandResult[] split = HandSplit.From(rec);
            Assert.AreEqual(100.0, split[0].Accuracy, 1e-9);
            Assert.AreEqual(AccuracyCalculator.PitchAccuracy(4, 4), split[1].Accuracy, 1e-9);
        }

        [Test]
        public void AClearlyWeakerHandIsPickedToPractiseAlone()
        {
            HandResult weak = HandSplit.Weaker(HandSplit.From(Attempt(leftMissed: 4)));
            Assert.IsNotNull(weak);
            Assert.AreEqual("left", weak.Hand);
        }

        [Test]
        public void TheWeakerHandComesBeforeAWeakBar()
        {
            HandResult[] split = HandSplit.From(Attempt(leftMissed: 4, leftWrong: 1));
            var bars = new List<BarStats> { new BarStats { bar = 3, notes = 4, perfect = 1, miss = 3, accuracy = 25 } };
            Suggestion s = SuggestionPicker.Pick(80, 100, AttemptRecord.BothHands, BarGrouping.Build(bars),
                                                 false, false, null, split);
            Assert.AreEqual(SuggestionRule.WeakHand, s.Rule);
            Assert.AreEqual("Left hand: all 5 mistakes", s.Message);
            Assert.AreEqual(SuggestionAction.SwitchHand, s.Action);
            Assert.AreEqual("left", s.TargetHand);
            Assert.AreEqual("Left hand alone", s.ButtonText);
        }

        [Test]
        public void TheMessageCountsTheOtherHandsMistakesToo()
        {
            AttemptRecord rec = Attempt(leftMissed: 5);
            rec.wrongNotes.Add(new WrongNote { kind = WrongNote.WrongPitch, hand = "right" });
            Suggestion s = SuggestionPicker.Pick(70, 100, AttemptRecord.BothHands, new List<BarGroup>(),
                                                 false, false, null, HandSplit.From(rec));
            Assert.AreEqual("Left hand: 5 of 6 mistakes", s.Message);
        }

        [Test]
        public void TheSummaryShowsEachHandsAccuracy()
        {
            AttemptRecord rec = Attempt(leftMissed: 4);
            rec.song = "Minuet";
            rec.accuracy = 80;
            ResultsSummary s = ResultsSummary.From(rec);
            Assert.AreEqual("60%", s.LeftHandText);
            Assert.AreEqual("100%", s.RightHandText);
            Assert.AreEqual(SuggestionRule.WeakHand, s.Suggestion.Rule);
            Assert.IsNull(ResultsSummary.From(Attempt(4, hand: "left")).Hands);
        }

        [Test]
        public void ThePreviewSampleShowsBothHands()
        {
            ResultsSummary s = ResultsSamples.Summary(ResultsSamples.BothHandsLeftWeak);
            Assert.IsNotNull(s.Hands);
            Assert.AreEqual(SuggestionRule.WeakHand, s.Suggestion.Rule);
            Assert.AreEqual("left", s.Suggestion.TargetHand);
        }

        [Test]
        public void HandsThatDidAboutAsWellPickNeither()
        {
            Assert.IsNull(HandSplit.Weaker(HandSplit.From(Attempt(leftMissed: 2))), "80%: above the weak line");
            Assert.IsNull(HandSplit.Weaker(null));
        }
    }
}
