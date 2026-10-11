using UnityEngine;

namespace PianoGlass.Piano
{
    /// <summary>
    /// The far-edge-bar maths for PianoKeyHighlighter's EdgeMarkers display
    /// mode, pulled out for the same reason SlabGeometry was pulled out of
    /// NoteHighway: a pure function any test can call with plain numbers.
    ///
    /// A key's Center sits at its mid-depth, with Forward (toward the player)
    /// and -Forward (the far edge, away from the player) either side of it --
    /// see PianoKeyLayout. The bar is sized to depthFraction of the key's own
    /// depth and placed so its far edge lands exactly on the key's far edge,
    /// which is what keeps it clear of the front portion a resting finger
    /// actually touches.
    /// </summary>
    public static class EdgeMarkerGeometry
    {
        /// <summary>
        /// barDepth: the bar's own front-to-back size.
        /// centerOffsetAlongForward: how far to shift the key's own Center,
        /// along the layout's Forward axis, to get the bar's center. Negative
        /// moves toward the far edge (away from the player), matching how
        /// NoteHighway steps back from Center to find that same far edge.
        /// </summary>
        public static (float barDepth, float centerOffsetAlongForward) FarEdgeBar(float keyDepth, float depthFraction)
        {
            float barDepth = keyDepth * Mathf.Clamp01(depthFraction);
            float centerOffsetAlongForward = -(keyDepth * 0.5f - barDepth * 0.5f);
            return (barDepth, centerOffsetAlongForward);
        }
    }
}
