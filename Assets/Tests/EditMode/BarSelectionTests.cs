using System.Collections.Generic;
using NUnit.Framework;
using PianoGlass.Scoring;

namespace PianoGlass.Tests.EditMode
{
    /// <summary>Tapping the results card's bar pills to choose what to loop.</summary>
    public class BarSelectionTests
    {
        private static List<BarGroup> Bars(int count, int size = 1)
        {
            var groups = new List<BarGroup>();
            for (int i = 0; i < count; i++)
                groups.Add(new BarGroup { FirstBar = i * size + 1, LastBar = (i + 1) * size });
            return groups;
        }

        [Test]
        public void StartsEmptyWithNothingToShow()
        {
            var s = new BarSelection();
            Assert.IsTrue(s.IsEmpty);
            Assert.IsFalse(s.Contains(0));
            Assert.IsNull(s.ToSuggestion(Bars(8), SuggestionRule.WeakBars));
        }

        [Test]
        public void OneTapChoosesOneBarAndSaysARangeIsPossible()
        {
            var s = new BarSelection();
            s.Tap(2);
            Suggestion g = s.ToSuggestion(Bars(8), SuggestionRule.WeakBars);
            Assert.AreEqual("Loop bar 3", g.ButtonText);
            Assert.AreEqual("Tap another bar for a range", g.Message);
            Assert.AreEqual(SuggestionAction.LoopBars, g.Action);
            Assert.AreEqual(3, g.FromBar);
            Assert.AreEqual(3, g.ToBar);
        }

        [TestCase(2, 4)]
        [TestCase(4, 2)]
        public void ASecondTapMakesARangeInEitherOrder(int first, int second)
        {
            var s = new BarSelection();
            s.Tap(first);
            s.Tap(second);
            Assert.IsTrue(s.Contains(3), "the bar between them");
            Assert.IsFalse(s.Contains(5));
            Suggestion g = s.ToSuggestion(Bars(8), SuggestionRule.WeakBars);
            Assert.AreEqual("Loop bars 3–5", g.ButtonText);
            Assert.AreEqual(3, g.FromBar);
            Assert.AreEqual(5, g.ToBar);
        }

        [Test]
        public void TappingTheOneBarAgainClearsIt()
        {
            var s = new BarSelection();
            s.Tap(2);
            s.Tap(2);
            Assert.IsTrue(s.IsEmpty);
        }

        [Test]
        public void ATapAfterARangeStartsAgain()
        {
            var s = new BarSelection();
            s.Tap(1);
            s.Tap(3);
            s.Tap(6);
            Assert.AreEqual(6, s.First);
            Assert.AreEqual(6, s.Last);
        }

        [Test]
        public void GroupedPillsLoopEverythingInThem()
        {
            var s = new BarSelection();
            s.Tap(1);
            Suggestion one = s.ToSuggestion(Bars(8, 4), SuggestionRule.WeakBars);
            Assert.AreEqual("Loop bars 5–8", one.ButtonText, "one pill, several bars");
            s.Tap(2);
            Assert.AreEqual("Loop bars 5–12", s.ToSuggestion(Bars(8, 4), SuggestionRule.WeakBars).ButtonText);
        }
    }
}
