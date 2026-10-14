using NUnit.Framework;
using PianoGlass.Piano;
using UnityEngine;

namespace PianoGlass.Tests.EditMode
{
    /// <summary>
    /// The anchor frame and the anchor-local round trip -- and, most
    /// importantly, the recenter-invariance case that is the actual bug:
    /// geometry stored relative to an anchor follows the anchor when tracking
    /// space is re-based, while legacy world points do not.
    ///
    /// Scene convention used throughout: the player stands at -Z looking
    /// toward +Z, so their right (low → high) is +X and toward-player is -Z.
    /// </summary>
    public class AnchoredKeyboardTests
    {
        private const int Lowest = 21, Highest = 108;
        private const float Depth = 0.145f;
        private static readonly float Span88 =
            (PianoKeyLayout.CountWhiteKeys(Lowest, Highest) - 1) * PianoKeyLayout.StandardWhiteKeySpacing;

        private static Vector3 Low => new Vector3(-Span88 / 2f, 0.75f, 0.3f);
        private static Vector3 High => new Vector3(Span88 / 2f, 0.75f, 0.3f);
        private static readonly Vector3 TowardPlayer = Vector3.back;

        // ------------------------------------------------------------------
        // Frame
        // ------------------------------------------------------------------

        [Test]
        public void FrameSitsAtTheMidpointOfTheTwoFrontEdges()
        {
            Pose f = AnchoredKeyboard.Frame(Low, High, TowardPlayer);
            AssertApprox((Low + High) * 0.5f, f.position);
        }

        [Test]
        public void FrameIsYawOnlyEvenWhenTheTwoPointsDifferInHeight()
        {
            Vector3 high = High + new Vector3(0f, 0.02f, 0f);
            Pose f = AnchoredKeyboard.Frame(Low, high, TowardPlayer);
            AssertApprox(Vector3.up, f.rotation * Vector3.up, 1e-5f);
        }

        [Test]
        public void FrameLocalXRunsLowToHighAndLocalMinusZFacesThePlayer()
        {
            Pose f = AnchoredKeyboard.Frame(Low, High, TowardPlayer);
            Vector3 lowLocal = AnchoredKeyboard.ToLocal(f, Low);
            Vector3 highLocal = AnchoredKeyboard.ToLocal(f, High);

            Assert.Less(lowLocal.x, 0f);
            Assert.Greater(highLocal.x, 0f);
            AssertApprox(TowardPlayer, f.rotation * Vector3.back, 1e-5f);
        }

        [Test]
        public void TowardPlayerIsHorizontalAndOnTheHeadSideEvenIfTheHintTilts()
        {
            Vector3 tiltedHint = new Vector3(0.1f, -0.6f, -1f); // head above and in front
            Pose f = AnchoredKeyboard.Frame(Low, High, tiltedHint);
            Vector3 toward = f.rotation * Vector3.back;

            Assert.AreEqual(0f, toward.y, 1e-5f);
            Assert.Greater(Vector3.Dot(toward, tiltedHint), 0f);
            Assert.AreEqual(0f, Vector3.Dot(toward, (High - Low).normalized), 1e-5f);
        }

        // ------------------------------------------------------------------
        // Round trip
        // ------------------------------------------------------------------

        [Test]
        public void WorldToLocalToWorldIsLossless()
        {
            var rng = new System.Random(1234);
            for (int i = 0; i < 200; i++)
            {
                var anchor = new Pose(RandomVector(rng, 3f), Quaternion.Euler(0f, (float)rng.NextDouble() * 360f, 0f));
                Vector3 p = RandomVector(rng, 3f);

                Vector3 back = AnchoredKeyboard.ToWorld(anchor, AnchoredKeyboard.ToLocal(anchor, p));
                AssertApprox(p, back, 1e-5f);
            }
        }

        [Test]
        public void IdentityAnchorIsANoOp()
        {
            Vector3 p = new Vector3(0.3f, 1.1f, -2.4f);
            AssertApprox(p, AnchoredKeyboard.ToLocal(Pose.identity, p), 1e-6f);
            AssertApprox(p, AnchoredKeyboard.ToWorld(Pose.identity, p), 1e-6f);
        }

        // ------------------------------------------------------------------
        // Recenter invariance -- the bug
        // ------------------------------------------------------------------

        /// <summary>
        /// Store the keyboard relative to anchor pose A. A recenter re-bases
        /// tracking space by T (yaw 37°, then a 1.2 m / -0.4 m shift), so the
        /// runtime now reports the same physical anchor at B = T·A. The
        /// rebuilt key must be exactly T applied to where it was.
        /// </summary>
        [Test]
        public void AnchoredLayoutFollowsARecenterExactly()
        {
            Pose a = AnchoredKeyboard.Frame(Low, High, TowardPlayer);
            KeyboardGeometry local = AnchoredKeyboard.ToLocal(a, Low, High, TowardPlayer);

            PianoKeyLayout before = AnchoredKeyboard.BuildLayout(a, local, Lowest, Highest, Depth, Vector3.zero);
            Assert.IsTrue(before.TryGetKey(60, out PianoKey c4Before));

            Quaternion tr = Quaternion.AngleAxis(37f, Vector3.up);
            Vector3 tt = new Vector3(1.2f, 0f, -0.4f);
            var b = new Pose(tr * a.position + tt, tr * a.rotation);

            PianoKeyLayout after = AnchoredKeyboard.BuildLayout(b, local, Lowest, Highest, Depth, Vector3.zero);
            Assert.IsTrue(after.TryGetKey(60, out PianoKey c4After));

            AssertApprox(tr * c4Before.Center + tt, c4After.Center, 1e-4f);
            Assert.AreEqual(Quaternion.Angle(tr * c4Before.Rotation, c4After.Rotation), 0f, 0.01f);
        }

        /// <summary>The inverse: today's world-point file, re-read after the
        /// same re-base, puts Middle C exactly T's displacement away from
        /// where it physically is.</summary>
        [Test]
        public void LegacyWorldPointsAreWrongByExactlyTheRecenter()
        {
            Quaternion tr = Quaternion.AngleAxis(37f, Vector3.up);
            Vector3 tt = new Vector3(1.2f, 0f, -0.4f);
            Vector3 head = (Low + High) * 0.5f + TowardPlayer * 0.5f;

            var legacy = new PianoKeyLayout(Low, High, Lowest, Highest, head, Depth);
            Assert.IsTrue(legacy.TryGetKey(60, out PianoKey c4));

            Vector3 physicallyNow = tr * c4.Center + tt;
            // Same numbers re-read from disk: the key is drawn where it was.
            var reloaded = new PianoKeyLayout(Low, High, Lowest, Highest, tr * head + tt, Depth);
            Assert.IsTrue(reloaded.TryGetKey(60, out PianoKey c4Reloaded));

            float error = Vector3.Distance(c4Reloaded.Center, physicallyNow);
            Assert.AreEqual(Vector3.Distance(c4.Center, physicallyNow), error, 1e-4f);
            Assert.Greater(error, 0.5f);
        }

        // ------------------------------------------------------------------
        // Yaw
        // ------------------------------------------------------------------

        [Test]
        public void YawRotatesAboutTheKeyboardCentreAndKeepsSpanAndHeight()
        {
            var g = new KeyboardGeometry(Low, High, TowardPlayer);
            KeyboardGeometry turned = AnchoredKeyboard.RotateYaw(g, 0.5f);

            AssertApprox((Low + High) * 0.5f, (turned.LowFront + turned.HighFront) * 0.5f, 1e-5f);
            Assert.AreEqual(Vector3.Distance(Low, High), Vector3.Distance(turned.LowFront, turned.HighFront), 1e-5f);
            Assert.AreEqual(Low.y, turned.LowFront.y, 1e-6f);
            Assert.AreEqual(0.5f, AnchoredKeyboard.YawBetween(High - Low, turned.HighFront - turned.LowFront), 1e-3f);
            Assert.AreEqual(0.5f, AnchoredKeyboard.YawBetween(TowardPlayer, turned.TowardPlayer), 1e-3f);
        }

        [Test]
        public void HalfADegreeOfYawMovesEachEndAboutFiveMillimetres()
        {
            var g = new KeyboardGeometry(Low, High, TowardPlayer);
            KeyboardGeometry turned = AnchoredKeyboard.RotateYaw(g, 0.5f);
            float endMove = Vector3.Distance(High, turned.HighFront);
            Assert.AreEqual(Span88 / 2f * Mathf.Deg2Rad * 0.5f, endMove, 1e-4f);
            Assert.That(endMove, Is.InRange(0.004f, 0.006f));
        }

        // ------------------------------------------------------------------
        // Plausibility
        // ------------------------------------------------------------------

        [Test]
        public void ARealKeyboardIsPlausible()
        {
            Pose a = AnchoredKeyboard.Frame(Low, High, TowardPlayer);
            Assert.IsNull(AnchoredKeyboard.CheckPlausible(
                AnchoredKeyboard.ToLocal(a, Low, High, TowardPlayer), Lowest, Highest));
        }

        // ------------------------------------------------------------------

        private static Vector3 RandomVector(System.Random rng, float range) =>
            new Vector3((float)(rng.NextDouble() * 2 - 1) * range,
                        (float)(rng.NextDouble() * 2 - 1) * range,
                        (float)(rng.NextDouble() * 2 - 1) * range);

        internal static void AssertApprox(Vector3 expected, Vector3 actual, float tolerance = 1e-4f)
        {
            Assert.AreEqual(expected.x, actual.x, tolerance, "x");
            Assert.AreEqual(expected.y, actual.y, tolerance, "y");
            Assert.AreEqual(expected.z, actual.z, tolerance, "z");
        }
    }
}
