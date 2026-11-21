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
    /// is believed (a different keyboard, or a wrong key). It also levels the
    /// two ends (KeepLevel). Pure, so it is tested without a headset.
    /// </summary>
    public static class HandSpan
    {
        /// <summary>How far a hand's width may be from the last one to be corrected, as a fraction.</summary>
        public const float Tolerance = 0.06f;
        /// <summary>Degrees: a hand calibration turned less than this from the
        /// last one keeps the last one's turn.</summary>
        public const float TurnToleranceDegrees = 2.5f;

        /// <summary>
        /// The same for the keyboard's turn (2026-11-15): a fingertip pressing
        /// one key near its front and the other further back turns the whole
        /// keyboard, about 1 degree for 2 cm, and the Middle C fix can't take a
        /// turn out. The piano didn't turn, so a turn within
        /// <see cref="TurnToleranceDegrees"/> of the last calibration's is
        /// taken off, about the middle: the depth becomes the two presses'
        /// average. True when the points were moved. <paramref name="lastRight"/>
        /// is the last layout's direction from the lowest key to the highest.
        /// </summary>
        public static bool KeepTurn(ref Vector3 low, ref Vector3 high, Vector3 lastRight, Vector3 up)
        {
            Vector3 now = Vector3.ProjectOnPlane(high - low, up);
            Vector3 last = Vector3.ProjectOnPlane(lastRight, up);
            if (now.sqrMagnitude < 1e-8f || last.sqrMagnitude < 1e-8f) return false;
            float angle = Vector3.SignedAngle(now, last, up);
            if (Mathf.Abs(angle) > TurnToleranceDegrees) return false;
            Quaternion turn = Quaternion.AngleAxis(angle, up);
            Vector3 middle = (low + high) * 0.5f;
            low = middle + turn * (low - middle);
            high = middle + turn * (high - middle);
            return true;
        }

        /// <summary>Metres: ends closer in height than this are left alone.</summary>
        public const float LevelToleranceMetres = 0.0005f;

        /// <summary>
        /// The keyboard's height (round 8, 2026-11-21): a fingertip pressed A0
        /// 14 mm higher than C8, and the piano came out tilted by that much. A
        /// real piano is level (both controller calibrations put the ends at
        /// the same height), and one hand press is only good to about 1 cm up
        /// and down, either way round. So both ends go to their average height,
        /// keeping the middle where the hand put it. True when the points were
        /// moved.
        /// </summary>
        public static bool KeepLevel(ref Vector3 low, ref Vector3 high, Vector3 up)
        {
            up = up.normalized;
            float lowHeight = Vector3.Dot(low, up), highHeight = Vector3.Dot(high, up);
            if (Mathf.Abs(highHeight - lowHeight) < LevelToleranceMetres) return false;
            float middle = (lowHeight + highHeight) * 0.5f;
            low += up * (middle - lowHeight);
            high += up * (middle - highHeight);
            return true;
        }

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
