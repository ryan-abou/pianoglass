using NUnit.Framework;
using UnityEngine;
using PianoGlass.UI;
using Step = PianoGlass.UI.PinchHold.Step;

namespace PianoGlass.Tests.EditMode
{
    public class PinchHoldTests
    {
        private const float Dt = 1f / 72f;

        /// <summary>Feeds frames with the gap from <paramref name="gap"/> (seconds
        /// since the start); the first step other than Hold, or Hold.</summary>
        private static Step Feed(PinchHold h, ref double now, float seconds, System.Func<double, float> gap,
                                 bool tracked = true)
        {
            double start = now;
            for (float t = 0f; t < seconds - 1e-4f; t += Dt)
            {
                now += Dt;
                Step s = h.Update(tracked, gap(now - start), now, Dt);
                if (s == Step.End) return s;
            }
            return Step.Hold;
        }

        [Test]
        public void FingersTogetherKeepHolding()
        {
            var h = new PinchHold();
            double now = 0;
            h.Begin(0.015f, now);
            // Tracking wobbles a few mm, as it does on the Quest.
            Assert.AreEqual(Step.Hold, Feed(h, ref now, 3f, t => 0.015f + 0.006f * Mathf.Sin((float)t * 40f)));
        }

        [Test]
        public void OpeningTheFingersLetsGoQuickly()
        {
            var h = new PinchHold();
            double now = 0;
            h.Begin(0.015f, now);
            Feed(h, ref now, 0.5f, _ => 0.015f);
            // Opening at 25 cm/s: clearly apart (35 mm) after 0.08 s, then 0.05 s more.
            double opened = now;
            Assert.AreEqual(Step.End, Feed(h, ref now, 0.3f, t => 0.015f + 0.25f * (float)t));
            Assert.Less(now - opened, 0.16, "let go within about a sixth of a second");
        }

        [Test]
        public void ItIsMeasuredFromTheTightestPinch()
        {
            // A hand whose pinch reads 28 mm apart (big fingers, or tracking)
            // still holds at 28 and lets go at 50.
            var h = new PinchHold();
            double now = 0;
            h.Begin(0.028f, now);
            Assert.AreEqual(Step.Hold, Feed(h, ref now, 1f, _ => 0.028f));
            Assert.AreEqual(Step.End, Feed(h, ref now, 0.2f, _ => 0.05f));
        }

        [Test]
        public void AOneFrameJumpInTheGapDoesNotLetGo()
        {
            var h = new PinchHold();
            double now = 0;
            h.Begin(0.015f, now);
            Assert.AreEqual(Step.Hold, Feed(h, ref now, 1f, t => (int)(t / Dt) % 20 == 10 ? 0.08f : 0.015f));
        }

        [Test]
        public void LosingTheHandHoldsStillThenLetsGo()
        {
            var h = new PinchHold();
            double now = 0;
            h.Begin(0.015f, now);
            Assert.AreEqual(Step.Lost, h.Update(false, 0f, now += Dt, Dt));
            Assert.AreEqual(Step.Hold, Feed(h, ref now, PinchHold.LostSeconds * 0.8f, _ => 0f, tracked: false));
            Assert.AreEqual(Step.Hold, h.Update(true, 0.015f, now += Dt, Dt), "back in time: still held");
            Assert.AreEqual(Step.End, Feed(h, ref now, PinchHold.LostSeconds + 0.1f, _ => 0f, tracked: false));
            Assert.IsTrue(h.EndedLost);
        }

        [Test]
        public void ClosedAtIsWhenTheFingersStartedToOpen()
        {
            var h = new PinchHold();
            double now = 0;
            h.Begin(0.015f, now);
            Feed(h, ref now, 0.5f, _ => 0.015f);
            double together = now;
            Feed(h, ref now, 0.3f, t => 0.015f + 0.25f * (float)t);
            Assert.AreEqual(together, h.ClosedAt, 0.04, "the last frame the tips were still together");
        }

        [Test]
        public void TheTrailGivesWhereThePanelWasThen()
        {
            var trail = new PoseTrail();
            for (int i = 0; i <= 60; i++) trail.Add(i / 60.0, new Pose(new Vector3(i, 0f, 0f), Quaternion.identity));
            Assert.IsTrue(trail.TryAt(0.9, 1.0, out Pose p));
            Assert.AreEqual(54f, p.position.x, 1e-4f);
            Assert.IsTrue(trail.TryAt(0.1, 1.0, out p), "asked for too far back");
            Assert.AreEqual(42f, p.position.x, 1.01f, "never more than 0.3 s back");
            Assert.LessOrEqual(trail.Count, 32, "keeps only half a second");
        }
    }
}
