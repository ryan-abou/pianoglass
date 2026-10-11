using NUnit.Framework;
using PianoGlass.Piano;

namespace PianoGlass.Tests.EditMode
{
    /// <summary>
    /// EdgeMarkerGeometry.FarEdgeBar is the only place EdgeMarkers mode's bar
    /// placement comes from. The property that actually matters for the
    /// feature -- "the bar never reaches into the front half of the key,
    /// where a finger rests" -- is awkward to eyeball in the headset through
    /// a low-resolution passthrough feed, so it's checked here instead.
    /// </summary>
    public class EdgeMarkerGeometryTests
    {
        private const float KeyDepth = 0.145f; // a real white key, per CLAUDE.md

        [Test]
        public void BarDepthIsKeyDepthTimesFraction()
        {
            var (barDepth, _) = EdgeMarkerGeometry.FarEdgeBar(KeyDepth, 0.2f);
            Assert.AreEqual(KeyDepth * 0.2f, barDepth, 1e-6f);
        }

        [Test]
        public void BarsFarEdgeCoincidesWithTheKeysFarEdge()
        {
            // Center is mid-depth, so the key's own far edge (away from the
            // player) is at -KeyDepth/2 along Forward -- the bar's far edge
            // should land exactly there, whatever the fraction.
            var (barDepth, centerOffset) = EdgeMarkerGeometry.FarEdgeBar(KeyDepth, 0.2f);
            float barFarEdge = centerOffset - barDepth * 0.5f;
            Assert.AreEqual(-KeyDepth * 0.5f, barFarEdge, 1e-6f);
        }

        [Test]
        public void BarsNearEdgeNeverCrossesTheKeysOwnMidpointWithinTheSupportedFractionRange()
        {
            // The near edge (toward the player) is where a finger actually
            // rests. Within PianoKeyHighlighter's serialized [Range(0.05,
            // 0.5)] for edgeMarkerDepthFraction, the bar must stay on the
            // far side of the key's own centre (offset 0 along Forward from
            // Center). At fraction 1 the bar legitimately IS the whole key,
            // so that case is intentionally excluded here, not a bug.
            foreach (float fraction in new[] { 0.05f, 0.2f, 0.5f })
            {
                var (barDepth, centerOffset) = EdgeMarkerGeometry.FarEdgeBar(KeyDepth, fraction);
                float barNearEdge = centerOffset + barDepth * 0.5f;
                Assert.LessOrEqual(barNearEdge, 1e-6f, $"fraction={fraction}");
            }
        }

        [Test]
        public void FullFractionMakesTheBarSpanTheEntireKey()
        {
            // The flip side of the test above: fraction 1 is outside the
            // field's supported range, and here it correctly covers the key
            // edge to edge rather than silently clamping to something smaller.
            var (barDepth, centerOffset) = EdgeMarkerGeometry.FarEdgeBar(KeyDepth, 1f);
            Assert.AreEqual(KeyDepth, barDepth, 1e-6f);
            Assert.AreEqual(0f, centerOffset, 1e-6f);
        }

        [Test]
        public void FractionAboveOneClampsToTheFullKey()
        {
            var (barDepthOver, _) = EdgeMarkerGeometry.FarEdgeBar(KeyDepth, 1.5f);
            var (barDepthFull, _) = EdgeMarkerGeometry.FarEdgeBar(KeyDepth, 1f);
            Assert.AreEqual(barDepthFull, barDepthOver, 1e-6f);
        }

        [Test]
        public void FractionBelowZeroClampsToZeroDepth()
        {
            var (barDepth, _) = EdgeMarkerGeometry.FarEdgeBar(KeyDepth, -0.5f);
            Assert.AreEqual(0f, barDepth, 1e-6f);
        }

        [Test]
        public void SmallerFractionMakesTheBarThinnerWithoutMovingItsFarEdge()
        {
            var (narrowDepth, narrowOffset) = EdgeMarkerGeometry.FarEdgeBar(KeyDepth, 0.1f);
            var (wideDepth, wideOffset) = EdgeMarkerGeometry.FarEdgeBar(KeyDepth, 0.3f);

            Assert.Less(narrowDepth, wideDepth);

            float narrowFarEdge = narrowOffset - narrowDepth * 0.5f;
            float wideFarEdge = wideOffset - wideDepth * 0.5f;
            Assert.AreEqual(wideFarEdge, narrowFarEdge, 1e-6f);
        }
    }
}
