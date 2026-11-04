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
            Assert.AreEqual("8A96A3", ColorUtility.ToHtmlStringRGB(t.textSecondary));
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
        public void GlassIsTranslucentWithABrighterRim()
        {
            GlassTokens t = NewTokens();
            Assert.Greater(t.tint.a, 0.3f, "light text must stay readable over a bright window");
            Assert.Less(t.tint.a, 0.9f, "it is glass: the room shows through");
            Assert.Greater(t.rimColor.a, t.tint.a);
            Assert.Less(t.rimBottom, 1f, "rim brightest along the top");
            Object.DestroyImmediate(t);
        }

        [Test]
        public void HexParsesAndFlagsTypos()
        {
            Assert.AreEqual("3DDC97", ColorUtility.ToHtmlStringRGB(GlassTokens.Hex("#3DDC97")));
            Assert.AreEqual(Color.magenta, GlassTokens.Hex("#nothex"));
        }
    }
}
