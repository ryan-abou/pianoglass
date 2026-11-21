using NUnit.Framework;
using PianoGlass.UI;
using UnityEngine;

namespace PianoGlass.Tests.EditMode
{
    public class PanelPoseTests
    {
        private static void Near(Pose expected, Pose actual)
        {
            Assert.Less(Vector3.Distance(expected.position, actual.position), 1e-4f, "position");
            Assert.Less(Quaternion.Angle(expected.rotation, actual.rotation), 0.01f, "rotation");
        }

        private static readonly Pose Keyboard =
            new Pose(new Vector3(0.4f, 0.75f, 0.6f), Quaternion.Euler(0f, 37f, 0f));

        [Test]
        public void AFramePoseComesBackTheSame()
        {
            var world = new Pose(new Vector3(-0.2f, 1.1f, 0.9f), Quaternion.Euler(-30f, 12f, 4f));
            Near(world, PanelPose.FromFrame(Keyboard, PanelPose.ToFrame(Keyboard, world)));
        }

        [Test]
        public void APanelSavedOnTheKeyboardMovesWithTheKeys()
        {
            var world = new Pose(new Vector3(0.1f, 0.9f, 0.7f), Quaternion.Euler(-30f, 37f, 0f));
            Pose saved = PanelPose.ToFrame(Keyboard, world);

            // The keys are nudged 2 cm along and turned 1 degree.
            var nudged = new Pose(Keyboard.position + new Vector3(0.02f, 0f, 0f), Quaternion.Euler(0f, 38f, 0f));
            Pose moved = PanelPose.FromFrame(nudged, saved);
            Near(saved, PanelPose.ToFrame(nudged, moved));
            Assert.AreEqual(1f, Quaternion.Angle(world.rotation, moved.rotation), 0.01f);
        }

        [Test]
        public void HoldingAPanelKeepsItWhereItWasInTheHand()
        {
            var hand = new Pose(new Vector3(0f, 1f, 0.3f), Quaternion.Euler(10f, 20f, 30f));
            var panel = new Pose(new Vector3(0.05f, 1.1f, 0.35f), Quaternion.Euler(-30f, 0f, 0f));
            Pose inHand = PanelPose.ToFrame(hand, panel);

            // The hand moves up 10 cm and tips back 20 degrees.
            var later = new Pose(hand.position + Vector3.up * 0.1f, Quaternion.Euler(-20f, 0f, 0f) * hand.rotation);
            Pose now = PanelPose.FromFrame(later, inHand);
            Assert.AreEqual(Vector3.Distance(hand.position, panel.position),
                            Vector3.Distance(later.position, now.position), 1e-4f);
            Assert.AreEqual(20f, Quaternion.Angle(panel.rotation, now.rotation), 0.05f);
        }

        [Test]
        public void AHeldPanelNeverLeansSideways()
        {
            Quaternion leaning = Quaternion.Euler(-25f, 30f, 20f);
            Quaternion r = PanelPose.Upright(leaning, Vector3.up, null);
            Assert.AreEqual(0f, Vector3.Dot(r * Vector3.right, Vector3.up), 1e-4f, "no roll");
            Assert.Less(Vector3.Angle(leaning * Vector3.forward, r * Vector3.forward), 0.01f, "still faces the same way");
        }

        [Test]
        public void PitchSnapsUprightNearZero()
        {
            Quaternion r = PanelPose.Upright(Quaternion.Euler(2f, 40f, 0f), Vector3.up, null);
            Assert.AreEqual(0f, Vector3.Dot(r * Vector3.forward, Vector3.up), 1e-4f, "straight up");
            Quaternion tipped = PanelPose.Upright(Quaternion.Euler(-30f, 40f, 0f), Vector3.up, null);
            Assert.AreEqual(30f, Vector3.Angle(Vector3.ProjectOnPlane(tipped * Vector3.forward, Vector3.up),
                                               tipped * Vector3.forward), 0.05f, "a real tilt is kept");
        }

        [Test]
        public void YawSnapsSquareToTheKeysNearZero()
        {
            var straight = new Vector3(0.6f, 0f, 0.8f);
            Quaternion near = Quaternion.LookRotation(Quaternion.AngleAxis(2f, Vector3.up) * straight, Vector3.up);
            Quaternion r = PanelPose.Upright(near, Vector3.up, straight);
            Assert.Less(Vector3.Angle(straight, r * Vector3.forward), 0.01f);

            Quaternion turned = Quaternion.LookRotation(Quaternion.AngleAxis(20f, Vector3.up) * straight, Vector3.up);
            Assert.AreEqual(20f, Vector3.Angle(straight, PanelPose.Upright(turned, Vector3.up, straight) * Vector3.forward), 0.05f);
        }

        [Test]
        public void YawSnapKeepsATilt()
        {
            Quaternion q = Quaternion.Euler(-30f, 2f, 0f);
            Quaternion r = PanelPose.Upright(q, Vector3.up, Vector3.forward);
            Vector3 f = r * Vector3.forward;
            Assert.AreEqual(0f, f.x, 1e-4f, "square to the keys");
            Assert.AreEqual(30f, Vector3.Angle(Vector3.ProjectOnPlane(f, Vector3.up), f), 0.05f, "tilt kept");
        }

        [Test]
        public void OnceSnappedItHoldsUntilWellPastTheEdge()
        {
            var snap = new PanelPose.Snap();
            PanelPose.Upright(Quaternion.Euler(2f, 0f, 0f), Vector3.up, null, ref snap);
            Assert.IsTrue(snap.Upright, "snaps in within 2.5 degrees");

            Quaternion r = PanelPose.Upright(Quaternion.Euler(5f, 0f, 0f), Vector3.up, null, ref snap);
            Assert.IsTrue(snap.Upright, "5 degrees: still snapped");
            Assert.AreEqual(0f, Vector3.Dot(r * Vector3.forward, Vector3.up), 1e-4f);

            PanelPose.Upright(Quaternion.Euler(7f, 0f, 0f), Vector3.up, null, ref snap);
            Assert.IsFalse(snap.Upright, "7 degrees: let go");

            PanelPose.Upright(Quaternion.Euler(4f, 0f, 0f), Vector3.up, null, ref snap);
            Assert.IsFalse(snap.Upright, "4 degrees coming back: not yet");
        }

        [Test]
        public void SquareHoldsTheSameWay()
        {
            var snap = new PanelPose.Snap();
            Quaternion Turned(float deg) => Quaternion.LookRotation(Quaternion.AngleAxis(deg, Vector3.up) * Vector3.forward);
            PanelPose.Upright(Turned(2f), Vector3.up, Vector3.forward, ref snap);
            Assert.IsTrue(snap.Square);
            PanelPose.Upright(Turned(5f), Vector3.up, Vector3.forward, ref snap);
            Assert.IsTrue(snap.Square, "5 degrees: still square");
            PanelPose.Upright(Turned(8f), Vector3.up, Vector3.forward, ref snap);
            Assert.IsFalse(snap.Square);
        }

        [Test]
        public void ARayPoseHasNoRoll()
        {
            Pose p = PanelPose.RayPose(Vector3.zero, new Vector3(0.3f, -0.4f, 1f));
            Assert.AreEqual(0f, Vector3.Dot(p.rotation * Vector3.right, Vector3.up), 1e-4f);
            Pose straightDown = PanelPose.RayPose(Vector3.zero, Vector3.down);
            Assert.AreEqual(0f, Vector3.Angle(Vector3.down, straightDown.rotation * Vector3.forward), 0.01f);
        }

        [Test]
        public void SmoothingGetsThereAtAnyFrameRate()
        {
            var a = new Pose(Vector3.zero, Quaternion.identity);
            var b = new Pose(Vector3.one, Quaternion.Euler(0f, 90f, 0f));
            Pose at72 = a, at36 = a;
            for (int i = 0; i < 72; i++) at72 = PanelPose.Smooth(at72, b, 1f / 72f, 18f);
            for (int i = 0; i < 36; i++) at36 = PanelPose.Smooth(at36, b, 1f / 36f, 18f);
            Near(at72, at36);
            Near(b, at72);
            Near(a, PanelPose.Smooth(a, b, 0f, 18f));
        }

        [Test]
        public void ASavedPoseReadsBack()
        {
            var p = new Pose(new Vector3(-0.123456f, 0.5f, 1e-5f), Quaternion.Euler(-30f, 15f, 2f));
            Assert.IsTrue(PanelPose.TryParse(PanelPose.Format(p), out Pose back));
            Near(p, back);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("1,2,3")]
        [TestCase("1,2,3,0,0,0,x")]
        [TestCase("1,2,NaN,0,0,0,1")]
        [TestCase("1,2,3,0,0,0,0")]
        [TestCase("1,2,3,0,0,0,1,4")]
        public void ABadSaveIsIgnored(string s)
        {
            Assert.IsFalse(PanelPose.TryParse(s, out Pose p));
            Near(Pose.identity, p);
        }

        [Test]
        public void ASaveReadsTheSameWithACommaDecimalLanguage()
        {
            var old = System.Threading.Thread.CurrentThread.CurrentCulture;
            try
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = new System.Globalization.CultureInfo("fr-FR");
                var p = new Pose(new Vector3(0.25f, 1.5f, -0.75f), Quaternion.identity);
                Assert.IsTrue(PanelPose.TryParse(PanelPose.Format(p), out Pose back));
                Near(p, back);
            }
            finally
            {
                System.Threading.Thread.CurrentThread.CurrentCulture = old;
            }
        }
    }
}
