using System;
using PianoGlass.Song;
using UnityEngine;

namespace PianoGlass.UI
{
    /// <summary>Which layer of glass a surface is: the panel itself, a row
    /// nested inside it (iOS grouped style), or a pill (buttons, chips,
    /// heat cells).</summary>
    public enum GlassStyle { Panel, Row, Pill }

    /// <summary>The per-layer look. Radii in metres; a pill's radius is
    /// always half its height and ignores <see cref="cornerRadius"/>.</summary>
    [Serializable]
    public struct GlassLayer
    {
        [Tooltip("Body colour; alpha is how much of the room it covers. Dark enough " +
                 "that light text reads over a bright window.")]
        public Color tint;
        [Tooltip("Corner radius, metres. Nested layers use a smaller one so the " +
                 "curves stay concentric.")]
        public float cornerRadius;
        [Tooltip("Width of the thick-glass edge, metres: where the bevel catches light.")]
        public float bevelWidth;
        [Range(0f, 2f)] public float bevelLight;
        [Range(0f, 0.3f)] public float topGlow;
    }

    /// <summary>
    /// The shared look of every menu and panel (Liquid Glass, decided
    /// 2026-10-30; v2 on 2026-11-03 after Ryan's reference images: Apple's
    /// WWDC25 glass, the iOS 26 grouped settings, ElementsKit's hairline
    /// card). One asset, Assets/UI/GlassTokens.asset, made by SceneSetup,
    /// that every glass surface reads -- a tuning round edits one file.
    ///
    /// The outcome colours are not copied here: they come straight from
    /// OutcomeTint, so a results card can never show a different mint from
    /// the slabs.
    /// </summary>
    [CreateAssetMenu(menuName = "PianoGlass/Glass Tokens", fileName = "GlassTokens")]
    public sealed class GlassTokens : ScriptableObject
    {
        public static readonly Color DefaultTextPrimary = Hex("#E9EEF2");
        /// <summary>Was #8A96A3; lifted on 2026-11-03 because small grey
        /// labels vanished under the sheen over a bright wall in the preview.</summary>
        public static readonly Color DefaultTextSecondary = Hex("#A3AEBA");

        [Header("Layers")]
        public GlassLayer panel = new GlassLayer
        {
            tint = new Color(0.10f, 0.12f, 0.15f, 0.56f),
            cornerRadius = 0.028f, bevelWidth = 0.009f, bevelLight = 1.0f, topGlow = 0.07f,
        };
        public GlassLayer row = new GlassLayer
        {
            tint = new Color(0.55f, 0.60f, 0.66f, 0.16f),
            cornerRadius = 0.016f, bevelWidth = 0.004f, bevelLight = 0.6f, topGlow = 0.04f,
        };
        public GlassLayer pill = new GlassLayer
        {
            tint = new Color(0.60f, 0.65f, 0.70f, 0.22f),
            cornerRadius = 0f, bevelWidth = 0.003f, bevelLight = 0.8f, topGlow = 0.05f,
        };

        [Header("Light")]
        [Tooltip("Where the light comes from, in the panel's plane: x right, y up.")]
        public Vector2 lightDirection = new Vector2(-0.45f, 1f);
        [Tooltip("How far the highlights slide as the head moves.")]
        [Range(0f, 2f)] public float parallax = 0.8f;
        [Tooltip("The opposite edge's inner reflection, as a fraction of the lit edge.")]
        [Range(0f, 1f)] public float backLight = 0.35f;

        [Header("Hairline")]
        public Color hairlineColor = new Color(1f, 1f, 1f, 0.55f);
        [Tooltip("Metres. Kept above a pixel by the shader so it never shimmers.")]
        public float hairlineWidth = 0.0006f;

        [Header("Sheen")]
        [Tooltip("The soft diagonal highlight across the body.")]
        [Range(0f, 0.5f)] public float sheenStrength = 0.07f;
        [Tooltip("Fresnel at grazing angles.")]
        [Range(0f, 1f)] public float fresnelStrength = 0.14f;

        [Header("Edge colour")]
        [Tooltip("The faint rainbow fringe along the bottom edge (WWDC). 0 turns it off.")]
        [Range(0f, 1f)] public float chromaStrength = 0.35f;

        [Header("Text")]
        public Color textPrimary = DefaultTextPrimary;
        public Color textSecondary = DefaultTextSecondary;

        public Color Hit => OutcomeTint.Hit;
        public Color Missed => OutcomeTint.Missed;
        public Color Wrong => OutcomeTint.Wrong;

        public GlassLayer Layer(GlassStyle style) =>
            style == GlassStyle.Row ? row : style == GlassStyle.Pill ? pill : panel;

        /// <summary>"#RRGGBB" or "#RRGGBBAA"; magenta for anything else, so a
        /// typo is obvious on the headset rather than silently black.</summary>
        public static Color Hex(string hex) =>
            ColorUtility.TryParseHtmlString(hex, out Color c) ? c : Color.magenta;
    }
}
