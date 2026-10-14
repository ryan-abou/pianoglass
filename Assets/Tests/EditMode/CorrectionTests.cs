using NUnit.Framework;
using PianoGlass.Piano;
using UnityEngine;

namespace PianoGlass.Tests.EditMode
{
    /// <summary>One-point (Middle C), two-point, and keyboard-frame nudge.</summary>
    public class CorrectionTests
    {
        private const int Lowest = 21, Highest = 108;
        private const float Depth = 0.145f;
        private const float Spacing = PianoKeyLayout.StandardWhiteKeySpacing;
        private static readonly float Span88 = (PianoKeyLayout.CountWhiteKeys(Lowest, Highest) - 1) * Spacing;

        private static Vector3 Low => new Vector3(-Span88 / 2f, 0.75f, 0.3f);
        private static Vector3 High => new Vector3(Span88 / 2f, 0.75f, 0.3f);
        private static readonly Vector3 Toward = Vector3.back;

        private static PianoKeyLayout TrueLayout() =>
            PianoKeyLayout.WithFacing(Low, High, Lowest, Highest, Toward, Depth);

        // ------------------------------------------------------------------
        // One-point
        // ------------------------------------------------------------------

        [Test]
        public void MiddleCRecoversASevenMillimetreOffset()
        {
            Pose anchor = AnchoredKeyboard.Frame(Low, High, Toward);
            Vector3 offset = new Vector3(0.006f, 0f, -0.0036f); // ~7 mm, horizontal
            KeyboardGeometry wrong = AnchoredKeyboard.ToLocal(anchor, Low - offset, High - offset, Toward);
            PianoKeyLayout wrongLayout = AnchoredKeyboard.BuildLayout(anchor, wrong, Lowest, Highest, Depth, Vector3.zero);

            Vector3 touched = TrueLayout().FrontEdgeCenter(60);
            OnePointCorrection c = AnchoredKeyboard.CorrectOnePoint(wrongLayout.FrontEdgeCenter(60), touched);
            Assert.IsTrue(c.Accepted, c.Reason);

            KeyboardGeometry fixedLocal = AnchoredKeyboard.Translate(wrong, AnchoredKeyboard.DirectionToLocal(anchor, c.Delta));
            PianoKeyLayout fixedLayout = AnchoredKeyboard.BuildLayout(anchor, fixedLocal, Lowest, Highest, Depth, Vector3.zero);

            AnchoredKeyboardTests.AssertApprox(touched, fixedLayout.FrontEdgeCenter(60), 0.0001f);
            Assert.AreEqual(wrongLayout.WhiteKeySpacing, fixedLayout.WhiteKeySpacing, 1e-6f, "span unchanged");
            AnchoredKeyboardTests.AssertApprox(wrongLayout.Right, fixedLayout.Right, 1e-6f);
        }

        [Test]
        public void MiddleCCapsTheVerticalPartAtOneCentimetre()
        {
            Vector3 expected = new Vector3(0f, 0.75f, 0f);
            OnePointCorrection c = AnchoredKeyboard.CorrectOnePoint(expected, expected + new Vector3(0.002f, 0.03f, 0f));
            Assert.IsTrue(c.Accepted);
            Assert.AreEqual(0.01f, c.Delta.y, 1e-6f);
            Assert.AreEqual(0.002f, c.Delta.x, 1e-6f);
        }

        [Test]
        public void MiddleCRejectsOverFiveCentimetresAsMoved()
        {
            Vector3 expected = new Vector3(0f, 0.75f, 0f);
            OnePointCorrection c = AnchoredKeyboard.CorrectOnePoint(expected, expected + new Vector3(0.051f, 0f, 0f));
            Assert.IsFalse(c.Accepted);
            StringAssert.Contains("moved", c.Reason);
        }

        // ------------------------------------------------------------------
        // Two-point
        // ------------------------------------------------------------------

        /// <summary>The current layout is off by 2° of yaw, 1.5% of width and
        /// a 1 cm shift. Touching the real C3 and C6 recovers the truth --
        /// the solve only needs the two touches and their notes.</summary>
        [Test]
        public void TwoPointRecoversYawWidthAndShift()
        {
            var truth = TrueLayout();

            Vector3 centre = (Low + High) * 0.5f;
            Quaternion yaw = Quaternion.AngleAxis(2f, Vector3.up);
            Vector3 shift = new Vector3(0.01f, 0f, 0f);
            Vector3 wrongLow = centre + yaw * ((Low - centre) * 1.015f) + shift;
            Vector3 wrongHigh = centre + yaw * ((High - centre) * 1.015f) + shift;
            var wrong = PianoKeyLayout.WithFacing(wrongLow, wrongHigh, Lowest, Highest, Toward, Depth);
            Assert.Greater(Vector3.Distance(wrong.FrontEdgeCenter(Highest), High), 0.02f, "test setup is actually wrong");

            TwoPointSolution s = AnchoredKeyboard.SolveTwoPoint(
                48, truth.FrontEdgeCenter(48), 84, truth.FrontEdgeCenter(84), Lowest, Highest);

            Assert.IsTrue(s.Accepted, s.Reason);
            AnchoredKeyboardTests.AssertApprox(Low, s.LowFront, 0.0005f);
            AnchoredKeyboardTests.AssertApprox(High, s.HighFront, 0.0005f);
            Assert.AreEqual(Spacing, s.WhiteKeySpacing, 1e-5f);
        }

        [Test]
        public void TwoPointWithTheEndKeysIsFullCalibration()
        {
            Vector3 a0 = new Vector3(-0.59f, 0.76f, 0.31f);
            Vector3 c8 = new Vector3(0.6f, 0.75f, 0.29f);
            TwoPointSolution s = AnchoredKeyboard.SolveTwoPoint(Lowest, a0, Highest, c8, Lowest, Highest);

            Assert.IsTrue(s.Accepted, s.Reason);
            AnchoredKeyboardTests.AssertApprox(a0, s.LowFront, 1e-5f);
            AnchoredKeyboardTests.AssertApprox(c8, s.HighFront, 1e-5f);
        }

        [Test]
        public void TwoPointAcceptsTheKeysInEitherOrder()
        {
            var truth = TrueLayout();
            TwoPointSolution s = AnchoredKeyboard.SolveTwoPoint(
                84, truth.FrontEdgeCenter(84), 48, truth.FrontEdgeCenter(48), Lowest, Highest);
            Assert.IsTrue(s.Accepted, s.Reason);
            AnchoredKeyboardTests.AssertApprox(Low, s.LowFront, 0.0005f);
        }

        [Test]
        public void TwoPointRejectsImplausibleSpacing()
        {
            var truth = TrueLayout();
            // Touched C3 and C6 but claimed C3 and C4: spacing comes out ~3x too wide.
            TwoPointSolution s = AnchoredKeyboard.SolveTwoPoint(
                48, truth.FrontEdgeCenter(48), 72, truth.FrontEdgeCenter(84), Lowest, Highest);
            Assert.IsFalse(s.Accepted);
        }

        [Test]
        public void TwoPointRejectsTheSameKeyTwice()
        {
            var truth = TrueLayout();
            Assert.IsFalse(AnchoredKeyboard.SolveTwoPoint(60, truth.FrontEdgeCenter(60), 60, truth.FrontEdgeCenter(60), Lowest, Highest).Accepted);
        }

        [Test]
        public void TwoPointRejectsBlackKeysAndKeysTooClose()
        {
            var truth = TrueLayout();
            Assert.IsFalse(AnchoredKeyboard.SolveTwoPoint(61, truth.FrontEdgeCenter(61), 84, truth.FrontEdgeCenter(84), Lowest, Highest).Accepted, "black key");
            Assert.IsFalse(AnchoredKeyboard.SolveTwoPoint(60, truth.FrontEdgeCenter(60), 67, truth.FrontEdgeCenter(67), Lowest, Highest).Accepted, "a fifth apart");
        }

        // ------------------------------------------------------------------
        // Nudge
        // ------------------------------------------------------------------

        [Test]
        public void KeyboardFrameNudgeSurvivesANinetyDegreeHeadingChange()
        {
            Vector3 nudgeMm = new Vector3(3f, -1f, 5f);
            Pose a = AnchoredKeyboard.Frame(Low, High, Toward);
            KeyboardGeometry local = AnchoredKeyboard.ToLocal(a, Low, High, Toward);

            Quaternion turn = Quaternion.AngleAxis(90f, Vector3.up);
            var b = new Pose(turn * a.position, turn * a.rotation);

            foreach (Pose anchor in new[] { a, b })
            {
                PianoKeyLayout plain = AnchoredKeyboard.BuildLayout(anchor, local, Lowest, Highest, Depth, Vector3.zero);
                PianoKeyLayout nudged = AnchoredKeyboard.BuildLayout(anchor, local, Lowest, Highest, Depth, nudgeMm);

                Vector3 worldShift = nudged.LowFrontEdge - plain.LowFrontEdge;
                Vector3 measured = AnchoredKeyboard.NudgeToKeyboardMm(worldShift, plain.Right, plain.Up, plain.Forward);
                AnchoredKeyboardTests.AssertApprox(nudgeMm, measured, 1e-3f);
            }
        }

        /// <summary>The keyboard-frame conversion reports exactly what
        /// KeyboardCalibration.NudgeMillimetres shows today for a world nudge.</summary>
        [Test]
        public void KeyboardFrameNudgeMatchesTodaysNudgeMillimetres()
        {
            Vector3 worldNudge = new Vector3(0.004f, 0.001f, -0.0025f);
            Vector3 head = (Low + High) * 0.5f + Toward * 0.5f + Vector3.up * 0.4f;

            // What KeyboardCalibration does: layout from nudged points, then
            // project the nudge on that layout's axes.
            var legacy = new PianoKeyLayout(Low + worldNudge, High + worldNudge, Lowest, Highest, head, Depth);
            Vector3 today = new Vector3(Vector3.Dot(worldNudge, legacy.Right),
                                        Vector3.Dot(worldNudge, legacy.Up),
                                        Vector3.Dot(worldNudge, legacy.Forward)) * 1000f;

            AnchoredKeyboard.Axes(Low, High, head - Low, out Vector3 r, out Vector3 u, out Vector3 f);
            AnchoredKeyboardTests.AssertApprox(today, AnchoredKeyboard.NudgeToKeyboardMm(worldNudge, r, u, f), 1e-3f);
        }
    }
}
