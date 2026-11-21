using NUnit.Framework;
using PianoGlass.UI;

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
            for (int i = 1; i <= 12; i++) text += $"- item {i}\n";
            HeadsetChecklist c = HeadsetChecklist.Parse(text);
            Assert.AreEqual(HeadsetChecklist.MaxItems, c.Items.Count);
            Assert.AreEqual(2, c.Overflow);
        }

        [Test]
        public void TicksSurviveTheSameListPushedAgain()
        {
            HeadsetChecklist first = HeadsetChecklist.Parse(Batch);
            Assert.IsTrue(first.Toggle(0));
            first.Toggle(2);
            Assert.AreEqual(2, first.TickedCount);

            HeadsetChecklist again = HeadsetChecklist.Parse(Batch);
            again.ApplyTicks(first.TickedKeys());

            Assert.IsTrue(again.Items[0].Ticked);
            Assert.IsFalse(again.Items[1].Ticked);
            Assert.IsTrue(again.Items[2].Ticked);
        }

        [Test]
        public void AnEditedItemOrANewListStartsUnticked()
        {
            HeadsetChecklist first = HeadsetChecklist.Parse(Batch);
            first.Toggle(0);
            first.Toggle(1);

            HeadsetChecklist edited = HeadsetChecklist.Parse(Batch.Replace("* Recenter", "* Recenter twice"));
            edited.ApplyTicks(first.TickedKeys());
            Assert.IsTrue(edited.Items[0].Ticked, "unchanged item keeps its tick");
            Assert.IsFalse(edited.Items[1].Ticked, "edited item starts over");

            HeadsetChecklist other = HeadsetChecklist.Parse(Batch.Replace("# Batch G round", "# Batch H round"));
            other.ApplyTicks(first.TickedKeys());
            Assert.AreEqual(0, other.TickedCount, "a new list starts over");
        }

        [Test]
        public void ToggleTwiceUnticks()
        {
            HeadsetChecklist c = HeadsetChecklist.Parse(Batch);
            c.Toggle(3);
            Assert.IsFalse(c.Toggle(3));
            Assert.AreEqual(0, c.TickedCount);
        }
    }
}
