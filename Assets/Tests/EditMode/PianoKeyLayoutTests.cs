using NUnit.Framework;
using PianoGlass.Piano;
using UnityEngine;

namespace PianoGlass.Tests.EditMode
{
    /// <summary>
    /// PianoKeyLayout turns two measured points into every key's position --
    /// exactly the kind of arithmetic that is easy to get subtly wrong (an
    /// off-by-half-a-key on black keys, a sign flip on Forward) and expensive
    /// to notice by eye on a real keyboard. These tests fix a known, exact
    /// two-point calibration and check the derived positions against maths
    /// worked out by hand, not against the implementation itself.
    /// </summary>
    public class PianoKeyLayoutTests
    {
        // C4 to C5: eight white keys (C D E F G A B C), so seven white-key
        // spans. Sizing the span to exactly 7 standard white keys means the
        // derived spacing should come back as exactly the standard constant,
        // with no calibration warning.
        private const int LowestNote = 60;  // C4
        private const int HighestNote = 72; // C5
        private const float Depth = 0.145f;

        private static PianoKeyLayout BuildLayout(float spacing = PianoKeyLayout.StandardWhiteKeySpacing)
        {
            Vector3 low = Vector3.zero;
            Vector3 high = new Vector3(spacing * 7f, 0f, 0f); // Right = +X
            Vector3 player = new Vector3(0f, 0f, 1f);          // in front, along +Z
            return new PianoKeyLayout(low, high, LowestNote, HighestNote, player, Depth);
        }

        [Test]
        public void DerivesStandardSpacingFromASevenKeySpan()
        {
            PianoKeyLayout layout = BuildLayout();
            Assert.AreEqual(PianoKeyLayout.StandardWhiteKeySpacing, layout.WhiteKeySpacing, 1e-6f);
            Assert.IsNull(layout.Warning);
        }

        [Test]
        public void WarnsWhenTheDerivedSpacingIsImplausible()
        {
            // A tenth of the real span -- as if the wrong two keys were touched.
            PianoKeyLayout layout = BuildLayout(PianoKeyLayout.StandardWhiteKeySpacing * 0.1f);
            Assert.IsNotNull(layout.Warning);
        }

        [Test]
        public void KeyCountCoversTheWholeInclusiveRange()
        {
            PianoKeyLayout layout = BuildLayout();
            Assert.AreEqual(HighestNote - LowestNote + 1, layout.KeyCount); // 13 keys, C4..C5
        }

        [Test]
        public void LowestWhiteKeySitsAtTheCapturedFrontEdgeSteppedBackHalfADepth()
        {
            PianoKeyLayout layout = BuildLayout();
            Assert.IsTrue(layout.TryGetKey(LowestNote, out PianoKey key));

            Vector3 expected = new Vector3(0f, 0f, -Depth * 0.5f);
            AssertApprox(expected, key.Center);
            Assert.IsFalse(key.IsBlack);
        }

        [Test]
        public void HighestWhiteKeySitsAtTheFarEndOfTheMeasuredSpan()
        {
            PianoKeyLayout layout = BuildLayout();
            Assert.IsTrue(layout.TryGetKey(HighestNote, out PianoKey key));

            float span = PianoKeyLayout.StandardWhiteKeySpacing * 7f;
            Vector3 expected = new Vector3(span, 0f, -Depth * 0.5f);
            AssertApprox(expected, key.Center);
        }

        [Test]
        public void BlackKeySitsBetweenItsNeighboursAndStandsProud()
        {
            PianoKeyLayout layout = BuildLayout();
            // C#4 (61): one white key (C4) below it.
            Assert.IsTrue(layout.TryGetKey(61, out PianoKey key));
            Assert.IsTrue(key.IsBlack);

            float spacing = PianoKeyLayout.StandardWhiteKeySpacing;
            float blackDepth = Depth * 0.62f;
            float along = 0.5f * spacing;                       // half a white key in
            float backOffset = Depth - blackDepth * 0.5f;        // aligned to the back
            const float blackRise = 0.010f;

            Vector3 expected = new Vector3(along, blackRise, -backOffset);
            AssertApprox(expected, key.Center);
        }

        [Test]
        public void MissingNoteOutsideTheRangeIsNotFound()
        {
            PianoKeyLayout layout = BuildLayout();
            Assert.IsFalse(layout.Contains(LowestNote - 1));
            Assert.IsFalse(layout.TryGetKey(HighestNote + 1, out _));
        }

        [TestCase(60, false)] // C
        [TestCase(61, true)]  // C#
        [TestCase(62, false)] // D
        [TestCase(63, true)]  // D#
        [TestCase(64, false)] // E
        [TestCase(65, false)] // F
        [TestCase(66, true)]  // F#
        public void IsBlackKeyMatchesPianoArithmetic(int note, bool expectedBlack)
        {
            Assert.AreEqual(expectedBlack, PianoKeyLayout.IsBlackKey(note));
        }

        [Test]
        public void SnapToWhiteMovesOffABlackKeyInTheRequestedDirection()
        {
            Assert.AreEqual(62, PianoKeyLayout.SnapToWhite(61, up: true));  // C#4 -> D4
            Assert.AreEqual(60, PianoKeyLayout.SnapToWhite(61, up: false)); // C#4 -> C4
        }

        [Test]
        public void RangeForKeyCountMatchesTheStandard88KeySpan()
        {
            var (low, high) = PianoKeyLayout.RangeForKeyCount(88);
            Assert.AreEqual(21, low);  // A0
            Assert.AreEqual(108, high); // C8
        }

        private static void AssertApprox(Vector3 expected, Vector3 actual, float tolerance = 1e-4f)
        {
            Assert.AreEqual(expected.x, actual.x, tolerance, "x");
            Assert.AreEqual(expected.y, actual.y, tolerance, "y");
            Assert.AreEqual(expected.z, actual.z, tolerance, "z");
        }
    }
}
