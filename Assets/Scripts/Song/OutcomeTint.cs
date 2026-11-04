using UnityEngine;

namespace PianoGlass.Song
{
    /// <summary>What has happened to one note so far, as the highway draws it.</summary>
    public enum NoteOutcome { Pending, Hit, Missed }

    /// <summary>
    /// Outcome colours (F19/F20) and how they arrive, kept pure so the timing
    /// is tested headlessly. The colours are reserved: hand colours (F13) may
    /// never use them, or every note would look "hit".
    ///
    ///   * Hit: the slab tints mint over FadeSeconds (~120 ms) -- quick
    ///     enough to read as the press's result, soft enough not to flash.
    ///   * Missed: the slab greys to slate over the same fade.
    ///   * Wrong key: the pressed key pulses coral once, decaying over
    ///     WrongPulseSeconds. Nothing glows at rest.
    /// </summary>
    public static class OutcomeTint
    {
        public static readonly Color Hit = FromHex(0x3D, 0xDC, 0x97);    // mint  #3DDC97
        public static readonly Color Missed = FromHex(0x5A, 0x63, 0x6E); // slate #5A636E
        public static readonly Color Wrong = FromHex(0xFF, 0x6B, 0x5B);  // coral #FF6B5B

        public const float FadeSeconds = 0.12f;
        public const float WrongPulseSeconds = 0.35f;

        /// <summary>A slab's colour <paramref name="secondsSinceJudged"/> after
        /// its note was judged. Pending notes keep the base colour.</summary>
        public static Color Slab(Color baseColor, NoteOutcome outcome, float secondsSinceJudged,
                                 Color hit, Color missed)
        {
            if (outcome == NoteOutcome.Pending) return baseColor;
            Color target = outcome == NoteOutcome.Hit ? hit : missed;
            return Color.Lerp(baseColor, target, Fade(secondsSinceJudged));
        }

        /// <summary>0 at the moment of judging, 1 once FadeSeconds have
        /// passed, eased out so most of the change lands early.</summary>
        public static float Fade(float seconds)
        {
            if (seconds <= 0f) return 0f;
            if (seconds >= FadeSeconds) return 1f;
            float t = seconds / FadeSeconds;
            return 1f - (1f - t) * (1f - t);
        }

        /// <summary>How much coral to mix into a wrong key: full at the press,
        /// none after WrongPulseSeconds, and none before the press.</summary>
        public static float WrongPulse(float secondsSincePress)
        {
            if (secondsSincePress < 0f || secondsSincePress >= WrongPulseSeconds) return 0f;
            float t = 1f - secondsSincePress / WrongPulseSeconds;
            return t * t;
        }

        /// <summary>True while a wrong-key pulse still has something to show.</summary>
        public static bool IsPulsing(float secondsSincePress) =>
            secondsSincePress >= 0f && secondsSincePress < WrongPulseSeconds;

        /// <summary>Brightness of the steady coral a wrong key keeps while held.</summary>
        public const float WrongHeldBrightness = 0.65f;

        /// <summary>
        /// A wrong key's colour <paramref name="secondsSincePress"/> after the
        /// press: bright coral, settling to a dimmer steady coral. Always
        /// coral -- never the pressed-key green (which says "correct") nor
        /// the greenish idle colour; going back to either read as the press
        /// having been fine (Ryan, 2026-11-03).
        /// </summary>
        public static Color WrongKeyHue(float secondsSincePress) =>
            Color.Lerp(WrongHeld, Wrong, WrongPulse(secondsSincePress));

        public const float WrongReleaseFadeSeconds = 0.3f;

        /// <summary>Opacity of a wrong key after release: 1 at the release,
        /// 0 after WrongReleaseFadeSeconds. It fades out as coral rather
        /// than turning another colour.</summary>
        public static float WrongReleaseFade(float secondsSinceRelease)
        {
            if (secondsSinceRelease <= 0f) return 1f;
            if (secondsSinceRelease >= WrongReleaseFadeSeconds) return 0f;
            return 1f - secondsSinceRelease / WrongReleaseFadeSeconds;
        }

        /// <summary>The steady coral of a wrong key still held down.</summary>
        public static Color WrongHeld =>
            new Color(Wrong.r * WrongHeldBrightness, Wrong.g * WrongHeldBrightness, Wrong.b * WrongHeldBrightness, 1f);

        private static Color FromHex(byte r, byte g, byte b) => new Color32(r, g, b, 255);
    }
}
