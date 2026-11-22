using NUnit.Framework;
using PianoGlass.UI;

namespace PianoGlass.Tests.EditMode
{
    public class StatusStripTextTests
    {
        private static StripKeys Good() => new StripKeys
        {
            KeyCount = 88, Lowest = "A0", Highest = "C8",
            KeyWidthMm = 23.46f, SpanCm = 119.6f, ExpectedSpanCm = 119.9f, Plausible = true,
        };

        [Test]
        public void KeysReadInPlainWords()
        {
            var lines = StatusStripText.Keys(Good());
            Assert.AreEqual(3, lines.Count, "no nudge or turn lines when there is none");
            Assert.AreEqual("88 keys, A0 to C8", lines[0]);
            StringAssert.Contains("Key width 23.5 mm", lines[1]);
            StringAssert.Contains("a real one is 23.5", lines[1]);
            StringAssert.Contains("Keyboard 119.6 cm wide", lines[2]);
        }

        [Test]
        public void NudgeAndTurnSayWhichWay()
        {
            Assert.AreEqual("Moved 2.0 mm right, 1.5 mm down", StatusStripText.Moved(2f, -1.5f, 0f));
            Assert.AreEqual("Moved 3.0 mm further", StatusStripText.Moved(0f, 0f, -3f));
            Assert.IsNull(StatusStripText.Moved(0.01f, 0f, 0f));
            Assert.AreEqual("Turned 0.20° left", StatusStripText.Turned(-0.2f));
            Assert.AreEqual("Turned 0.50° right", StatusStripText.Turned(0.5f));
            Assert.IsNull(StatusStripText.Turned(0f));

            StripKeys k = Good();
            k.NudgeRightMm = 2f;
            k.YawDegrees = -0.2f;
            Assert.AreEqual(5, StatusStripText.Keys(k).Count);
        }

        [Test]
        public void WrongKeysSayToRedo()
        {
            StripKeys k = Good();
            k.Plausible = false;
            k.KeyWidthMm = 31f;
            var lines = StatusStripText.Keys(k);
            StringAssert.Contains(StatusStripText.Red, lines[1]);
            StringAssert.Contains("Press B to redo", lines[lines.Count - 1]);
        }

        [Test]
        public void ScoreLooksLikeAGame()
        {
            var lines = StatusStripText.Score(new StripScore
            {
                Score = 12340, Accuracy = 96.4, Perfect = 40, Great = 12, Good = 3, Miss = 2, Wrong = 1,
            });
            StringAssert.Contains("12,340", lines[0]);
            StringAssert.Contains("96%", lines[0]);
            StringAssert.Contains("Perfect 40", lines[1]);
            StringAssert.Contains("Wrong 1", lines[1]);
        }

        [Test]
        public void WaitModeShowsCorrectAndWrong()
        {
            var lines = StatusStripText.Score(new StripScore { PitchOnly = true, Accuracy = 90, Correct = 9, Wrong = 1 });
            StringAssert.Contains("90%", lines[0]);
            StringAssert.Contains("Correct 9", lines[1]);
            StringAssert.DoesNotContain("Perfect", lines[1]);
        }

        [Test]
        public void LastHitSaysEarlyOrLate()
        {
            StringAssert.EndsWith("C4   12 ms late", StatusStripText.LastHit("perfect", "C4", true, 12.3));
            StringAssert.EndsWith("C4   30 ms early", StatusStripText.LastHit("good", "C4", true, -30));
            StringAssert.EndsWith("right on time", StatusStripText.LastHit("perfect", "C4", true, 0.2));
            StringAssert.EndsWith("E4", StatusStripText.LastHit("miss", "E4", false, 0));
            StringAssert.Contains("MISS", StatusStripText.LastHit("miss", "E4", false, 0));
        }

        [Test]
        public void AWrongNoteNamesTheRightOneUnlessItIsHidden()
        {
            StringAssert.Contains("D4 instead of D#4, bar 9", StatusStripText.Wrong("D4", "D#4", 9, false));
            StringAssert.Contains("D4, nothing to play, bar 2", StatusStripText.Wrong("D4", null, 2, false));
            string hidden = StatusStripText.Wrong("D4", "D#4", 9, true);
            StringAssert.DoesNotContain("D#4", hidden);
            StringAssert.Contains("Wrong note D4, bar 9", hidden);
        }
    }
}
