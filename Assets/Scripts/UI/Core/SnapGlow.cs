using UnityEngine;

namespace PianoGlass.UI
{
    /// <summary>
    /// Which edges of a held panel's glass light up when it snaps, and how
    /// brightly. Standing straight up lights its two upright edges (left,
    /// right); square to the keys lights its top and bottom. Round 8
    /// (2026-11-21): bars drawn beside the panel were easy to see but didn't
    /// look good, so the glass's own edge glows instead, like light catching
    /// it. A snap flashes bright, then settles; letting go or leaving the
    /// snap fades it out. Pure, so it is tested without a headset.
    /// </summary>
    public static class SnapGlow
    {
        /// <summary>How bright a snapped edge stays, and its flash on snapping.
        /// Round 9 (2026-11-21): 0.45 and 1 were "barely visible".</summary>
        public const float Settled = 1f, Flash = 1.8f;
        /// <summary>Seconds for the flash to settle, and to fade out.</summary>
        public const float FlashSeconds = 0.35f, FadeOutSeconds = 0.15f;
        /// <summary>Panel units (mm): a glass edge this close to the panel's
        /// outline is an outside edge. Edges inside a two-part panel stay dark.</summary>
        public const float OutsideToleranceMm = 2f;

        /// <summary>
        /// Next brightness of one edge. <paramref name="sinceOn"/> is how long
        /// it has been snapped (ignored when off).
        /// </summary>
        public static float Step(float current, bool on, float sinceOn, float deltaTime)
        {
            if (on)
            {
                float flash = Mathf.Clamp01(1f - sinceOn / FlashSeconds);
                return Mathf.Lerp(Settled, Flash, flash * flash);
            }
            return Mathf.MoveTowards(current, 0f, deltaTime / FadeOutSeconds * Flash);
        }

        /// <summary>
        /// Per edge of one glass surface (x left, y right, z bottom, w top):
        /// 1 where that edge is on the panel's outline and its snap is on.
        /// </summary>
        public static Vector4 Sides(Rect glass, Rect outline, bool upright, bool square)
        {
            float t = OutsideToleranceMm;
            return new Vector4(
                upright && Mathf.Abs(glass.xMin - outline.xMin) <= t ? 1f : 0f,
                upright && Mathf.Abs(glass.xMax - outline.xMax) <= t ? 1f : 0f,
                square && Mathf.Abs(glass.yMin - outline.yMin) <= t ? 1f : 0f,
                square && Mathf.Abs(glass.yMax - outline.yMax) <= t ? 1f : 0f);
        }
    }
}
