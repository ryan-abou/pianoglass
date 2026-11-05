using UnityEngine;

namespace PianoGlass.UI
{
    /// <summary>
    /// The geometry a glass panel needs before its shader runs: how big the
    /// rounded rectangle is, how big a quad must be to hold it with room for
    /// the anti-aliased edge, and a radius that can never exceed the panel.
    /// Pure, so GlassPanel and the tests share it.
    /// </summary>
    public static class GlassShape
    {
        /// <summary>Metres of quad beyond the rectangle on each side, so the
        /// edge's one-pixel fade is never clipped by the quad itself.</summary>
        public const float EdgeMargin = 0.002f;

        /// <summary>Half size of the glass, metres: a rect (in canvas units)
        /// times the canvas scale, plus padding on every side. Never negative.</summary>
        public static Vector2 HalfSizeMetres(Vector2 rectSizeUnits, float metresPerUnit, Vector2 paddingMetres)
        {
            float x = rectSizeUnits.x * 0.5f * metresPerUnit + paddingMetres.x;
            float y = rectSizeUnits.y * 0.5f * metresPerUnit + paddingMetres.y;
            return new Vector2(Mathf.Max(0f, x), Mathf.Max(0f, y));
        }

        /// <summary>Half size of the quad that draws it: the glass plus EdgeMargin.</summary>
        public static Vector2 QuadHalfMetres(Vector2 glassHalf) =>
            new Vector2(glassHalf.x + EdgeMargin, glassHalf.y + EdgeMargin);

        /// <summary>A corner radius that fits: 0 to the smaller half size.
        /// A radius bigger than that would turn the panel into a pill with
        /// a flat side missing.</summary>
        public static float ClampRadius(float radius, Vector2 glassHalf) =>
            Mathf.Clamp(radius, 0f, Mathf.Min(glassHalf.x, glassHalf.y));

        /// <summary>The radius a surface of <paramref name="style"/> is drawn
        /// with: a pill is always fully round at its short side; panels and
        /// rows use their token radius, clamped to fit.</summary>
        public static float RadiusFor(GlassStyle style, float tokenRadius, Vector2 glassHalf) =>
            style == GlassStyle.Pill
                ? Mathf.Min(glassHalf.x, glassHalf.y)
                : ClampRadius(tokenRadius, glassHalf);

        /// <summary>A nested surface's radius for concentric curves: the
        /// parent's radius minus the inset between them, never below
        /// <paramref name="minimum"/>.</summary>
        public static float Concentric(float parentRadius, float inset, float minimum = 0.004f) =>
            Mathf.Max(minimum, parentRadius - Mathf.Max(0f, inset));
    }
}
