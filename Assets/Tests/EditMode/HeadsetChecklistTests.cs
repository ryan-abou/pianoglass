using NUnit.Framework;
using PianoGlass.UI;
using Mark = PianoGlass.UI.HeadsetChecklist.Mark;

namespace PianoGlass.Tests.EditMode
{
    public class HeadsetChecklistTests
    {
        private const string Batch =
            "# Batch G round\n" +
            "\n" +
            "Do these in order.\n" +
            "- Launch, don't recenter for 15 s | keys on the piano?\n" +
            "* Recenter\n" +
            "1. Middle C fix with the **bare** tip | keys stay put\n" +
            "2) Metronome video\n" +
            "- [ ] Check `[LaunchPose]` lines\n" +
            "## A heading is not an item\n";

        [Test]
        public void ReadsTitleItemsAndDetails()
        {
            HeadsetChecklist c = HeadsetChecklist.Parse(Batch);

            Assert.AreEqual("Batch G round", c.Title);
            Assert.AreEqual(5, c.Items.Count);
            Assert.AreEqual("Launch, don't recenter for 15 s", c.Items[0].Text);
            Assert.AreEqual("keys on the piano?", c.Items[0].Detail);
            Assert.AreEqual("Recenter", c.Items[1].Text);
            Assert.AreEqual("", c.Items[1].Detail);
            Assert.AreEqual("Middle C fix with the bare tip", c.Items[2].Text, "Markdown bold dropped");
            Assert.AreEqual("Metronome video", c.Items[3].Text);
            Assert.AreEqual("Check [LaunchPose] lines", c.Items[4].Text, "checkbox and backticks dropped");
            Assert.AreEqual(0, c.Overflow);
        }

        [Test]
        public void WindowsLineEndingsAndNoTitleStillWork()
        {
            HeadsetChecklist c = HeadsetChecklist.Parse("- one\r\n- two\r\n");
            Assert.AreEqual("Checklist", c.Title);
            Assert.AreEqual(2, c.Items.Count);
            Assert.AreEqual("two", c.Items[1].Text);
        }

        [Test]
        public void AMissingFileIsAnEmptyList()
        {
            Assert.AreEqual(0, HeadsetChecklist.Parse(null).Items.Count);
            Assert.AreEqual(0, HeadsetChecklist.Parse("").Items.Count);
        }

        [Test]
        public void ItemsPastTheLimitAreCountedNotShown()
        {
            var text = "# Long\n";
            for (int i = 1; i <= HeadsetChecklist.MaxItems + 2; i++) text += $"- item {i}\n";
            HeadsetChecklist c = HeadsetChecklist.Parse(text);
            Assert.AreEqual(HeadsetChecklist.MaxItems, c.Items.Count);
            Assert.AreEqual(2, c.Overflow);
        }

        [Test]
        public void MarksSurviveTheSameListPushedAgain()
        {
            HeadsetChecklist first = HeadsetChecklist.Parse(Batch);
            Assert.AreEqual(Mark.Pass, first.Press(0, Mark.Pass));
            Assert.AreEqual(Mark.Fail, first.Press(2, Mark.Fail));
            Assert.AreEqual(1, first.PassedCount);
            Assert.AreEqual(1, first.FailedCount);
            Assert.AreEqual(2, first.MarkedCount);

            HeadsetChecklist again = HeadsetChecklist.Parse(Batch);
            again.ApplyMarks(first.Keys(Mark.Pass), first.Keys(Mark.Fail));

            Assert.AreEqual(Mark.Pass, again.Items[0].Mark);
            Assert.AreEqual(Mark.None, again.Items[1].Mark);
            Assert.AreEqual(Mark.Fail, again.Items[2].Mark);
        }

        [Test]
        public void AnEditedItemOrANewListStartsOver()
        {
            HeadsetChecklist first = HeadsetChecklist.Parse(Batch);
            first.Press(0, Mark.Pass);
            first.Press(1, Mark.Fail);

            HeadsetChecklist edited = HeadsetChecklist.Parse(Batch.Replace("* Recenter", "* Recenter twice"));
            edited.ApplyMarks(first.Keys(Mark.Pass), first.Keys(Mark.Fail));
            Assert.AreEqual(Mark.Pass, edited.Items[0].Mark, "unchanged item keeps its mark");
            Assert.AreEqual(Mark.None, edited.Items[1].Mark, "edited item starts over");

            HeadsetChecklist other = HeadsetChecklist.Parse(Batch.Replace("# Batch G round", "# Batch H round"));
            other.ApplyMarks(first.Keys(Mark.Pass), first.Keys(Mark.Fail));
            Assert.AreEqual(0, other.MarkedCount, "a new list starts over");
        }

        [Test]
        public void PressingTheSameButtonAgainClearsIt()
        {
            HeadsetChecklist c = HeadsetChecklist.Parse(Batch);
            c.Press(3, Mark.Pass);
            Assert.AreEqual(Mark.None, c.Press(3, Mark.Pass));
            Assert.AreEqual(0, c.MarkedCount);
        }

        [Test]
        public void TheOtherButtonSwitchesTheMark()
        {
            HeadsetChecklist c = HeadsetChecklist.Parse(Batch);
            c.Press(3, Mark.Pass);
            Assert.AreEqual(Mark.Fail, c.Press(3, Mark.Fail));
            Assert.AreEqual(0, c.PassedCount);
            Assert.AreEqual(1, c.FailedCount);
        }

        [Test]
        public void OldTicksComeInAsPasses()
        {
            HeadsetChecklist c = HeadsetChecklist.Parse(Batch);
            c.ApplyMarks(new[] { c.Items[1].Key }, null);
            Assert.AreEqual(Mark.Pass, c.Items[1].Mark);
            Assert.AreEqual(1, c.MarkedCount);
        }
    }
}
