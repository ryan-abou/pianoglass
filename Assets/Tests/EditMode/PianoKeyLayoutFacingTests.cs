using NUnit.Framework;
using PianoGlass.Piano;
using UnityEngine;

namespace PianoGlass.Tests.EditMode
{
    /// <summary>
    /// PianoKeyLayout.WithFacing and FrontEdgeCenter. Kept in their own
    /// file so PianoKeyLayoutTests stays exactly as it was.
    /// </summary>
    public class PianoKeyLayoutFacingTests
    {
        private const int Lowest = 60, Highest = 72;
        private const float Depth = 0.145f;
        private static readonly Vector3 Low = Vector3.zero;
        private static readonly Vector3 High = new Vector3(PianoKeyLayout.StandardWhiteKeySpacing * 7f, 0f, 0f);

        [Test]
        public void ExplicitFacingMatchesTheHeadConstructorWhenTheHeadIsOnThePlayerSide()
        {
            Vector3 head = new Vector3(0.1f, 0.5f, 0.6f);
            var byHead = new PianoKeyLayout(Low, High, Lowest, Highest, head, Depth);
            var byFacing = PianoKeyLayout.WithFacing(Low, High, Lowest, Highest, Vector3.forward, Depth);

            for (int note = Lowest; note <= Highest; note++)
            {
                Assert.IsTrue(byHead.TryGetKey(note, out PianoKey a));
                Assert.IsTrue(byFacing.TryGetKey(note, out PianoKey b));
                AnchoredKeyboardTests.AssertApprox(a.Center, b.Center, 1e-6f);
            }
            AnchoredKeyboardTests.AssertApprox(byHead.Forward, byFacing.Forward, 1e-6f);
        }

        /// <summary>The head on the far side of the key line -- a stale origin,
        /// or an untracked first frame -- mirrors the head-based layout front
        /// to back. Explicit facing cannot be fooled that way.</summary>
        [Test]
        public void ExplicitFacingDoesNotMirrorWhenTheHeadIsOnTheFarSide()
        {
            Vector3 headBehind = new Vector3(0.1f, 0.5f, -0.6f);
            var byHead = new PianoKeyLayout(Low, High, Lowest, Highest, headBehind, Depth);
            var byFacing = PianoKeyLayout.WithFacing(Low, High, Lowest, Highest, Vector3.forward, Depth);

            Assert.Less(byHead.Forward.z, 0f, "head-based layout follows the head and flips");
            Assert.Greater(byFacing.Forward.z, 0f, "explicit facing stays toward the player");
        }

        [Test]
        public void FrontEdgeOfTheEndKeysIsTheCapturedPoint()
        {
            var layout = PianoKeyLayout.WithFacing(Low, High, Lowest, Highest, Vector3.forward, Depth);
            AnchoredKeyboardTests.AssertApprox(Low, layout.FrontEdgeCenter(Lowest), 1e-6f);
            AnchoredKeyboardTests.AssertApprox(High, layout.FrontEdgeCenter(Highest), 1e-6f);
            AnchoredKeyboardTests.AssertApprox(Low, layout.LowFrontEdge, 1e-6f);
            AnchoredKeyboardTests.AssertApprox(High, layout.HighFrontEdge, 1e-6f);
        }

        [Test]
        public void FrontEdgeIsHalfAKeyInFrontOfTheCentre()
        {
            var layout = PianoKeyLayout.WithFacing(Low, High, Lowest, Highest, Vector3.forward, Depth);

            Assert.IsTrue(layout.TryGetKey(64, out PianoKey e4)); // white
            AnchoredKeyboardTests.AssertApprox(e4.Center + layout.Forward * (e4.Depth * 0.5f), layout.FrontEdgeCenter(64), 1e-6f);

            Assert.IsTrue(layout.TryGetKey(61, out PianoKey cs4)); // black
            AnchoredKeyboardTests.AssertApprox(cs4.Center + layout.Forward * (cs4.Depth * 0.5f), layout.FrontEdgeCenter(61), 1e-6f);
        }
    }
}
