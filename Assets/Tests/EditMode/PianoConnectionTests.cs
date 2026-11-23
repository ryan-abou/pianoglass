using NUnit.Framework;
using PianoGlass.Song;

namespace PianoGlass.Tests.EditMode
{
    public class PianoConnectionTests
    {
        // ---- The popup ------------------------------------------------------

        [Test]
        public void AKeyboardThatOpensAtLaunchNeverShowsThePopup()
        {
            var f = new PianoConnectionFlow();
            f.Tick(0.0, false, false);
            f.Tick(1.0, true, false);
            f.Tick(10.0, true, false);
            Assert.AreEqual(PianoPopup.None, f.Popup);
            Assert.IsFalse(f.NoPiano);
        }

        [Test]
        public void NoKeyboardAsksAfterAFewSecondsAndContinueMeansNoPiano()
        {
            var f = new PianoConnectionFlow();
            f.Tick(0.0, false, false);
            f.Tick(2.0, false, false);
            Assert.AreEqual(PianoPopup.None, f.Popup, "it may still be opening");
            f.Tick(PianoConnectionFlow.StartupGraceSeconds, false, false);
            Assert.AreEqual(PianoPopup.Connect, f.Popup);
            Assert.AreEqual(PianoConnectionFlow.ContinueLabel, f.ButtonLabel);

            Assert.AreEqual(PianoFlowAction.None, f.Press());
            Assert.IsTrue(f.NoPiano);
            f.Tick(20.0, false, false);
            Assert.AreEqual(PianoPopup.None, f.Popup, "it doesn't keep asking");
        }

        [Test]
        public void PluggingInClosesTheConnectPopup()
        {
            var f = new PianoConnectionFlow();
            f.Tick(0.0, false, false);
            f.Tick(5.0, false, false);
            Assert.AreEqual(PianoPopup.Connect, f.Popup);
            f.Tick(6.0, true, false);
            Assert.AreEqual(PianoPopup.None, f.Popup);
        }

        [Test]
        public void UnpluggingMidSongPausesAndAsks()
        {
            var f = new PianoConnectionFlow();
            f.Tick(0.0, true, true);
            Assert.AreEqual(PianoFlowAction.PauseSong, f.Tick(30.0, false, true));
            Assert.AreEqual(PianoPopup.Unplugged, f.Popup);
            Assert.AreEqual(PianoConnectionFlow.ContinueShortLabel, f.ButtonLabel);
        }

        [Test]
        public void UnpluggingWithNoSongJustAsks()
        {
            var f = new PianoConnectionFlow();
            f.Tick(0.0, true, false);
            Assert.AreEqual(PianoFlowAction.None, f.Tick(30.0, false, false));
            Assert.AreEqual(PianoPopup.Connect, f.Popup);
        }

        [Test]
        public void PluggedBackInWaitsForResume()
        {
            var f = new PianoConnectionFlow();
            f.Tick(0.0, true, true);
            f.Tick(30.0, false, true);
            f.Tick(45.0, true, true);
            Assert.AreEqual(PianoPopup.Reconnected, f.Popup, "the song does not go on by itself");
            Assert.AreEqual(PianoConnectionFlow.ResumeLabel, f.ButtonLabel);
            Assert.AreEqual(PianoFlowAction.ResumeSong, f.Press());
            Assert.AreEqual(PianoPopup.None, f.Popup);
            Assert.IsFalse(f.NoPiano);
        }

        [Test]
        public void ContinuingAfterAnUnplugResumesWithoutPiano()
        {
            var f = new PianoConnectionFlow();
            f.Tick(0.0, true, true);
            f.Tick(30.0, false, true);
            Assert.AreEqual(PianoFlowAction.ResumeSong, f.Press());
            Assert.IsTrue(f.NoPiano);
        }

        [Test]
        public void AKeyboardEndsNoPianoModeAndUnpluggingItAsksAgain()
        {
            var f = new PianoConnectionFlow();
            f.Tick(0.0, false, false);
            f.Tick(5.0, false, false);
            f.Press();
            Assert.IsTrue(f.NoPiano);
            f.Tick(60.0, true, false);
            Assert.IsFalse(f.NoPiano);
            f.Tick(90.0, false, false);
            Assert.AreEqual(PianoPopup.Connect, f.Popup);
        }

        [Test]
        public void PressingWithNoPopupDoesNothing()
        {
            var f = new PianoConnectionFlow();
            f.Tick(0.0, true, false);
            Assert.AreEqual(PianoFlowAction.None, f.Press());
            Assert.IsFalse(f.NoPiano);
            Assert.IsNull(f.ButtonLabel);
        }

        // ---- The countdown ----------------------------------------------------

        [Test]
        public void CountsThreeTwoOneThenPlays()
        {
            var c = new ResumeCountdown();
            c.Start(10.0);
            Assert.AreEqual(3, c.Number(10.0));
            Assert.AreEqual(2, c.Number(11.2));
            Assert.AreEqual(1, c.Number(12.9));
            Assert.IsFalse(c.Finished(12.9));
            Assert.IsTrue(c.Finished(13.0));
            Assert.IsFalse(c.Running);
            Assert.IsFalse(c.Finished(13.1), "only once");
            Assert.AreEqual(0, c.Number(13.1));
        }

        // The metronome count-in has its own tests: CountInTests.

        [Test]
        public void CancellingStopsIt()
        {
            var c = new ResumeCountdown();
            c.Start(0.0);
            c.Cancel();
            Assert.IsFalse(c.Finished(5.0));
            Assert.AreEqual(0, c.Number(1.0));
        }
    }
}
