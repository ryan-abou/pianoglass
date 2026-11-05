using NUnit.Framework;
using PianoGlass.Song;
using PianoGlass.UI;
using UnityEngine;

namespace PianoGlass.Tests.EditMode
{
    public class GlassTests
    {
        // ------------------------------------------------------------------
        // GlassShape
        // ------------------------------------------------------------------

        [Test]
        public void HalfSizeIsRectTimesScalePlusPadding()
        {
            // The status canvas: 600 x 600 units at 0.001 m per unit.
            Vector2 half = GlassShape.HalfSizeMetres(new Vector2(600, 400), 0.001f, new Vector2(0.01f, 0.02f));
            Assert.AreEqual(0.31f, half.x, 1e-6f);
            Assert.AreEqual(0.22f, half.y, 1e-6f);
        }

        [Test]
        public void HalfSizeNeverNegative()
        {
            Vector2 half = GlassShape.HalfSizeMetres(new Vector2(10, 10), 0.001f, new Vector2(-1f, -1f));
            Assert.AreEqual(Vector2.zero, half);
        }

        [Test]
        public void QuadLeavesRoomForTheEdge()
        {
            Vector2 quad = GlassShape.QuadHalfMetres(new Vector2(0.3f, 0.2f));
            Assert.AreEqual(0.3f + GlassShape.EdgeMargin, quad.x, 1e-6f);
            Assert.AreEqual(0.2f + GlassShape.EdgeMargin, quad.y, 1e-6f);
        }

        [Test]
        public void RadiusClampedToTheShorterHalf()
        {
            Assert.AreEqual(0.05f, GlassShape.ClampRadius(0.2f, new Vector2(0.3f, 0.05f)), 1e-6f);
            Assert.AreEqual(0.02f, GlassShape.ClampRadius(0.02f, new Vector2(0.3f, 0.05f)), 1e-6f);
            Assert.AreEqual(0f, GlassShape.ClampRadius(-1f, new Vector2(0.3f, 0.05f)), 1e-6f);
        }

        // ------------------------------------------------------------------
        // GlassTokens
        // ------------------------------------------------------------------

        private static GlassTokens NewTokens() => ScriptableObject.CreateInstance<GlassTokens>();

        [Test]
        public void TextColoursAreTheDesignHexes()
        {
            GlassTokens t = NewTokens();
            Assert.AreEqual("E9EEF2", ColorUtility.ToHtmlStringRGB(t.textPrimary));
            Assert.AreEqual("A3AEBA", ColorUtility.ToHtmlStringRGB(t.textSecondary));
            Object.DestroyImmediate(t);
        }

        [Test]
        public void OutcomeColoursAreOutcomeTintsOwn()
        {
            GlassTokens t = NewTokens();
            Assert.AreEqual(OutcomeTint.Hit, t.Hit);
            Assert.AreEqual(OutcomeTint.Missed, t.Missed);
            Assert.AreEqual(OutcomeTint.Wrong, t.Wrong);
            Object.DestroyImmediate(t);
        }

        [Test]
        public void PanelIsTranslucentAndDarkEnoughForLightText()
        {
            GlassTokens t = NewTokens();
            Assert.Greater(t.panel.tint.a, 0.3f, "light text must stay readable over a bright window");
            Assert.Less(t.panel.tint.a, 0.9f, "it is glass: the room shows through");
            Assert.Less(t.panel.tint.grayscale, 0.3f);
            Object.DestroyImmediate(t);
        }

        [Test]
        public void NestedLayersAreLighterAndSmaller()
        {
            GlassTokens t = NewTokens();
            Assert.Less(t.row.cornerRadius, t.panel.cornerRadius, "concentric curves");
            Assert.Less(t.row.bevelWidth, t.panel.bevelWidth);
            Assert.Less(t.row.tint.a, t.panel.tint.a, "a row is a lighter frost on the panel");
            Assert.AreEqual(t.row.cornerRadius, t.Layer(GlassStyle.Row).cornerRadius);
            Assert.AreEqual(t.pill.tint, t.Layer(GlassStyle.Pill).tint);
            Assert.AreEqual(t.panel.tint, t.Layer(GlassStyle.Panel).tint);
            Object.DestroyImmediate(t);
        }

        [Test]
        public void HairlineIsBrightAndThin()
        {
            GlassTokens t = NewTokens();
            Assert.Greater(t.hairlineColor.grayscale, 0.9f);
            Assert.Less(t.hairlineWidth, 0.002f);
            Object.DestroyImmediate(t);
        }

        // ------------------------------------------------------------------
        // Radii
        // ------------------------------------------------------------------

        [Test]
        public void APillIsAlwaysFullyRound()
        {
            Assert.AreEqual(0.014f, GlassShape.RadiusFor(GlassStyle.Pill, 0f, new Vector2(0.06f, 0.014f)), 1e-6f);
            Assert.AreEqual(0.02f, GlassShape.RadiusFor(GlassStyle.Panel, 0.02f, new Vector2(0.2f, 0.1f)), 1e-6f);
            Assert.AreEqual(0.05f, GlassShape.RadiusFor(GlassStyle.Row, 0.2f, new Vector2(0.2f, 0.05f)), 1e-6f);
        }

        [Test]
        public void ConcentricRadiusIsParentMinusInset()
        {
            Assert.AreEqual(0.016f, GlassShape.Concentric(0.028f, 0.012f), 1e-6f);
            Assert.AreEqual(0.004f, GlassShape.Concentric(0.010f, 0.020f), 1e-6f, "never below the minimum");
        }

        [Test]
        public void HexParsesAndFlagsTypos()
        {
            Assert.AreEqual("3DDC97", ColorUtility.ToHtmlStringRGB(GlassTokens.Hex("#3DDC97")));
            Assert.AreEqual(Color.magenta, GlassTokens.Hex("#nothex"));
        }
    }
}
