using PianoGlass.Song;
using UnityEngine;

namespace PianoGlass.UI
{
    /// <summary>
    /// The shared look of every menu and panel (Liquid Glass, decided
    /// 2026-10-30): glass tint, rim, corners and sheen, and the two text
    /// colours. One asset (Assets/UI/GlassTokens.asset, made by SceneSetup)
    /// that every glass surface reads, so a tuning round edits one file.
    ///
    /// The outcome colours are not copied here: they come straight from
    /// OutcomeTint, which the highway and key overlay already use, so a
    /// results card can never show a different mint from the slabs.
    /// </summary>
    [CreateAssetMenu(menuName = "PianoGlass/Glass Tokens", fileName = "GlassTokens")]
    public sealed class GlassTokens : ScriptableObject
    {
        public static readonly Color DefaultTextPrimary = Hex("#E9EEF2");
        public static readonly Color DefaultTextSecondary = Hex("#8A96A3");

        [Header("Glass")]
        [Tooltip("Body colour; alpha is how much of the room shows through. Dark " +
                 "enough that light text reads over a bright window.")]
        public Color tint = new Color(0.07f, 0.09f, 0.12f, 0.62f);
        [Tooltip("Extra brightness added at the top edge, fading to none at the bottom.")]
        [Range(0f, 0.3f)] public float topGlow = 0.06f;
        [Tooltip("Corner radius, metres.")]
        public float cornerRadius = 0.022f;

        [Header("Rim")]
        public Color rimColor = new Color(1f, 1f, 1f, 0.85f);
        [Tooltip("Rim band width, metres, measured inward from the edge.")]
        public float rimWidth = 0.0022f;
        [Tooltip("Rim strength at the bottom edge as a fraction of the top: the " +
                 "rim is brightest along the top, as if lit from above.")]
        [Range(0f, 1f)] public float rimBottom = 0.25f;

        [Header("Sheen")]
        public Color sheenColor = Color.white;
        [Tooltip("Fresnel sheen at grazing angles. Small: it is a hint of glass, " +
                 "not a reflection.")]
        [Range(0f, 1f)] public float sheenStrength = 0.16f;
        [Range(0.5f, 8f)] public float sheenPower = 3f;

        [Header("Text")]
        public Color textPrimary = DefaultTextPrimary;
        public Color textSecondary = DefaultTextSecondary;

        public Color Hit => OutcomeTint.Hit;
        public Color Missed => OutcomeTint.Missed;
        public Color Wrong => OutcomeTint.Wrong;

        /// <summary>"#RRGGBB" or "#RRGGBBAA"; magenta for anything else, so a
        /// typo is obvious on the headset rather than silently black.</summary>
        public static Color Hex(string hex) =>
            ColorUtility.TryParseHtmlString(hex, out Color c) ? c : Color.magenta;
    }
}
