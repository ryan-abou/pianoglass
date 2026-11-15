using UnityEngine;

namespace PianoGlass.Piano
{
    /// <summary>
    /// Which of the two tracked index fingertips pressed a key (#5, calibrating
    /// by hand). The lower of the two was used first, and was wrong: the other
    /// hand resting in the lap is lower than the keys, and it became the touch,
    /// 20 cm low and "too short" (2026-11-14).
    ///
    /// The finger that pressed a key has just come down onto it: a key travels
    /// about 10 mm. So a fingertip that dropped at least <see cref="MinDropM"/>
    /// in the moment before the note, clearly more than the other, is the one.
    /// When neither (or both) did, the one nearest where that key was last
    /// calibrated, if there is a last time. Pure, so it is tested without a headset.
    /// </summary>
    public static class PressingFinger
    {
        /// <summary>Metres a fingertip has to have come down to count as pressing.</summary>
        public const float MinDropM = 0.004f;
        /// <summary>The dropping finger has to have dropped this many times as far as the other.</summary>
        public const float ClearRatio = 2f;
        /// <summary>Metres: a fingertip further than this from the key's last spot is not near it.</summary>
        public const float NearKeyM = 0.25f;

        public struct Tip
        {
            public bool Tracked;
            public Vector3 Position;
            /// <summary>How far it came down just before the note, metres.</summary>
            public float Drop;
        }

        /// <summary>The index of the pressing fingertip, or -1 when there is
        /// none or it can't be told. <paramref name="lastKey"/>: where that key
        /// was in the last calibration, if any.</summary>
        public static int Choose(Tip[] tips, Vector3? lastKey)
        {
            int a = -1, b = -1;
            for (int i = 0; i < tips.Length; i++)
            {
                if (!tips[i].Tracked) continue;
                if (a < 0) a = i;
                else if (b < 0) b = i;
            }
            if (a < 0) return -1;
            if (b < 0) return a;

            float da = tips[a].Drop, db = tips[b].Drop;
            if (da >= MinDropM && da >= db * ClearRatio) return a;
            if (db >= MinDropM && db >= da * ClearRatio) return b;

            if (lastKey.HasValue)
            {
                float ka = Vector3.Distance(tips[a].Position, lastKey.Value);
                float kb = Vector3.Distance(tips[b].Position, lastKey.Value);
                int nearer = ka <= kb ? a : b;
                if (Mathf.Min(ka, kb) <= NearKeyM) return nearer;
            }

            // Both came down about the same and there is nothing else to go on.
            return -1;
        }
    }
}
