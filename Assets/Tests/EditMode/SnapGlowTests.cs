using NUnit.Framework;
using PianoGlass.UI;
using UnityEngine;

namespace PianoGlass.Tests.EditMode
{
    public class SnapGlowTests
    {
        private static readonly Rect Panel = Rect.MinMaxRect(-150f, -250f, 150f, 250f);

        [Test]
        public void ASnapFlashesThenSettles()
        {
            Assert.AreEqual(SnapGlow.Flash, SnapGlow.Step(0f, true, 0f, 0.014f), 1e-5f);
            float half = SnapGlow.Step(0f, true, SnapGlow.FlashSeconds * 0.5f, 0.014f);
            Assert.Less(half, SnapGlow.Flash);
            Assert.Greater(half, SnapGlow.Settled);
            Assert.AreEqual(SnapGlow.Settled, SnapGlow.Step(half, true, 2f, 0.014f), 1e-5f);
        }

        [Test]
        public void LeavingTheSnapFadesOutAndStaysOut()
        {
            float level = SnapGlow.Settled;
            for (int i = 0; i < 2; i++) level = SnapGlow.Step(level, false, 0f, 0.014f);
            Assert.Less(level, SnapGlow.Settled);
            Assert.Greater(level, 0f, "a fade, not a blink");
            for (int i = 0; i < 20; i++) level = SnapGlow.Step(level, false, 0f, 0.014f);
            Assert.AreEqual(0f, level);
        }

        [Test]
        public void UprightLightsTheSidesAndSquareTheTopAndBottom()
        {
            Assert.AreEqual(new Vector4(1f, 1f, 0f, 0f), SnapGlow.Sides(Panel, Panel, true, false));
            Assert.AreEqual(new Vector4(0f, 0f, 1f, 1f), SnapGlow.Sides(Panel, Panel, false, true));
            Assert.AreEqual(Vector4.zero, SnapGlow.Sides(Panel, Panel, false, false));
        }

        [Test]
        public void EdgesInsideATwoPartPanelStayDark()
        {
            // A results card with its details open beside it: one outline, two glasses.
            Rect card = Rect.MinMaxRect(-150f, -250f, 150f, 250f);
            Rect details = Rect.MinMaxRect(160f, -200f, 400f, 250f);
            Rect outline = Rect.MinMaxRect(-150f, -250f, 400f, 250f);
            Assert.AreEqual(new Vector4(1f, 0f, 1f, 1f), SnapGlow.Sides(card, outline, true, true));
            Assert.AreEqual(new Vector4(0f, 1f, 0f, 1f), SnapGlow.Sides(details, outline, true, true));
        }
    }
}
