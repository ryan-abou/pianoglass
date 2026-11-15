using UnityEngine;

namespace PianoGlass.Piano
{
    /// <summary>
    /// Keeps the key width a hand calibration measures honest (#5). Hand
    /// tracking measured the lowest-to-highest span about 2% short on the
    /// Quest 3 (white keys 23.0 mm, against 23.5 mm every controller
    /// calibration measures; 2026-11-14), which puts the far end a whole key
    /// width off. The keyboard didn't change size, so when a hand
    /// calibration's width is within <see cref="Tolerance"/> of the last
    /// calibration's, the last width is kept: both touched points move apart
    /// (or together) evenly about their middle, keeping the hand's direction
    /// and the middle where the hand put it. Further off than that, the hand
    /// is believed (a different keyboard, or a wrong key). Pure, so it is
    /// tested without a headset.
    /// </summary>
    public static class HandSpan
    {
        /// <summary>How far a hand's width may be from the last one to be corrected, as a fraction.</summary>
        public const float Tolerance = 0.06f;

        /// <summary>True when <paramref name="low"/> and <paramref name="high"/>
        /// were moved so their white key width is <paramref name="knownSpacing"/>.</summary>
        public static bool KeepSpacing(ref Vector3 low, ref Vector3 high, float measuredSpacing, float knownSpacing)
        {
            if (measuredSpacing <= 0f || knownSpacing <= 0f) return false;
            float ratio = knownSpacing / measuredSpacing;
            if (Mathf.Abs(ratio - 1f) > Tolerance) return false;
            Vector3 middle = (low + high) * 0.5f;
            low = middle + (low - middle) * ratio;
            high = middle + (high - middle) * ratio;
            return true;
        }
    }
}
