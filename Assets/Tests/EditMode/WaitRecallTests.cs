using NUnit.Framework;
using PianoGlass.Song;

namespace PianoGlass.Tests.EditMode
{
    /// <summary>Memory fade in wait mode: what counts as remembered, and the
    /// flash that shows a note at a glimpse.</summary>
    public class WaitRecallTests
    {
        private static WaitRecall Played(params bool[] neededHelp)
        {
            var r = new WaitRecall();
            for (int i = 0; i < neededHelp.Length; i++)
            {
                r.AtStop(i);
                if (neededHelp[i]) r.NeededHelp();
            }
            r.Finish();
            return r;
        }

        [Test]
        public void EveryStopRightFirstTimeIsAllFromMemory()
        {
            WaitRecall r = Played(false, false, false, false);
            Assert.AreEqual(4, r.Total);
            Assert.AreEqual(4, r.Clean);
            Assert.AreEqual(100.0, r.Percent, 1e-9);
        }

        [Test]
        public void BruteForcingOrWaitingForEveryNoteIsNoneFromMemory()
        {
            Assert.AreEqual(0.0, Played(true, true, true, true).Percent, 1e-9);
        }

        [Test]
        public void TheLadderReadsItLikeAnyOtherPass()
        {
            var fade = new MemoryFade(MemoryFade.Glimpse);
            Assert.AreEqual(MemoryFade.Glimpse, fade.StepAfter(Played(false, false, false, true).Percent, true), "3 of 4 stays");
            Assert.AreEqual(3, fade.StepAfter(Played(false, true, false, true).Percent, true), "2 of 4 comes back one");
            Assert.AreEqual(2, fade.StepAfter(Played(true, true, true, true).Percent, true), "0 of 4 comes back two");
        }

        [Test]
        public void HelpCountsOnlyForItsOwnStop()
        {
            var r = new WaitRecall();
            r.AtStop(1.0);
            r.NeededHelp();
            r.NeededHelp();
            r.AtStop(2.0);
            r.Finish();
            Assert.AreEqual(2, r.Total);
            Assert.AreEqual(1, r.Clean);
        }

        [Test]
        public void TheSameStopIsOneStopHoweverOftenItIsReported()
        {
            var r = new WaitRecall();
            Assert.IsTrue(r.AtStop(1.0));
            Assert.IsFalse(r.AtStop(1.0));
            r.Finish();
            r.Finish();
            Assert.AreEqual(1, r.Total);
        }

        [Test]
        public void ResetForgetsThePass()
        {
            WaitRecall r = Played(false, true);
            r.Reset();
            Assert.AreEqual(0, r.Total);
            Assert.AreEqual(0.0, r.Percent, 1e-9);
        }

        [Test]
        public void AFlashShowsOnlyThatNoteLikeAGlimpse()
        {
            var fade = new MemoryFade(MemoryFade.Glimpse);
            Assert.AreEqual(0f, fade.FlashOpacity(5.0, 100.0), 1e-6f, "nothing flashed yet");

            fade.Flash(5.0, 100.0);
            Assert.AreEqual(0.15f, fade.FlashOpacity(5.0, 100.0 + MemoryFade.GlimpseHoldSeconds), 1e-6f);
            Assert.AreEqual(0f, fade.FlashOpacity(5.0, 100.0 + MemoryFade.GlimpseHoldSeconds + MemoryFade.GlimpseFadeSeconds), 1e-6f);
            Assert.AreEqual(0f, fade.FlashOpacity(6.0, 100.0 + MemoryFade.GlimpseHoldSeconds), 1e-6f, "another note stays hidden");
        }

        [Test]
        public void AnotherFlashStartsItAgainAndAPassEndClearsIt()
        {
            var fade = new MemoryFade(MemoryFade.Glimpse);
            fade.Flash(5.0, 100.0);
            fade.Flash(5.0, 110.0);
            Assert.AreEqual(0.15f, fade.FlashOpacity(5.0, 110.5), 1e-6f);

            fade.EndPass(100.0, true);
            Assert.AreEqual(0f, fade.FlashOpacity(5.0, 110.5), 1e-6f);
        }

        [Test]
        public void ARevealFadesInAndStaysUntilThePassEnds()
        {
            var fade = new MemoryFade(MemoryFade.Glimpse);
            fade.Reveal(5.0, 100.0);

            Assert.AreEqual(0f, fade.FlashOpacity(5.0, 100.0), 1e-6f, "not all at once");
            Assert.AreEqual(0.075f, fade.FlashOpacity(5.0, 100.0 + MemoryFade.RevealFadeInSeconds / 2), 1e-5f);
            Assert.AreEqual(0.15f, fade.FlashOpacity(5.0, 100.0 + MemoryFade.RevealFadeInSeconds), 1e-6f);
            Assert.AreEqual(0.15f, fade.FlashOpacity(5.0, 500.0), 1e-6f, "it does not go away");
            Assert.AreEqual(0f, fade.FlashOpacity(6.0, 500.0), 1e-6f, "only that note");

            fade.EndPass(100.0, true);
            Assert.AreEqual(0f, fade.FlashOpacity(5.0, 500.0), 1e-6f);
        }

        [Test]
        public void AskingForTheSameRevealAgainDoesNotStartItOver()
        {
            var fade = new MemoryFade(MemoryFade.Glimpse);
            fade.Reveal(5.0, 100.0);
            fade.Reveal(5.0, 103.0);
            Assert.AreEqual(0.15f, fade.FlashOpacity(5.0, 103.0), 1e-6f);
        }

        [Test]
        public void ARevealDuringAFlashDoesNotDip()
        {
            var fade = new MemoryFade(MemoryFade.Glimpse);
            fade.Flash(5.0, 100.0);
            fade.Reveal(5.0, 100.4);
            Assert.AreEqual(0.15f, fade.FlashOpacity(5.0, 100.4), 1e-6f);
            Assert.AreEqual(0.15f, fade.FlashOpacity(5.0, 100.8), 1e-6f);
        }

        [Test]
        public void ABriefFlashFadesInToo()
        {
            var fade = new MemoryFade(MemoryFade.Glimpse);
            fade.Flash(5.0, 100.0);
            Assert.AreEqual(0f, fade.FlashOpacity(5.0, 100.0), 1e-6f);
            Assert.AreEqual(0.075f, fade.FlashOpacity(5.0, 100.0 + MemoryFade.RevealFadeInSeconds / 2), 1e-5f);
        }
    }
}
