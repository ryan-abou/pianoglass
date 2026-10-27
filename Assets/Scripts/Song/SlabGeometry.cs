using UnityEngine;

namespace PianoGlass.Song
{
    /// <summary>
    /// The vertical-extent maths for one note's slab on the highway, pulled
    /// out of NoteHighway.Update so there is exactly one implementation of it
    /// -- callable from EditMode tests with no MonoBehaviour, no scene, and
    /// no running player loop, and callable from NoteHighway itself, so the
    /// two can never drift apart.
    /// </summary>
    public static class SlabGeometry
    {
        /// <summary>The most of a note's height the gap may take.</summary>
        public const float MaxGapFraction = 1f / 3f;

        /// <summary>
        /// The slab's bottom offset and height, in metres above the key line,
        /// for a note running from noteStartTime to noteEndTime, at the given
        /// songTime. speed converts seconds of lead time into metres of wall
        /// height (typically highwayHeight / leadTimeSeconds).
        ///
        /// bottom is clamped to the key line (never negative), so a note
        /// being played shrinks into the line instead of sliding beneath it.
        /// The top is clamped to highwayHeight, so a note longer than the
        /// whole lead window is clipped instead of spawning off the top of
        /// the wall. height &lt;= 0 means the slab should be hidden: not yet
        /// due, or fully past -- true for any songTime, including a negative
        /// one during the pre-song lead-in, since bottom and top are both
        /// just affine functions of songTime and the same two clamps apply
        /// regardless of its sign.
        ///
        /// gap, in metres, is trimmed off the top so back-to-back notes --
        /// two quarter-note C4s in a row -- draw as two slabs rather than
        /// one long bar. Off the top, never the bottom: the bottom reaching
        /// the key line is the "play now" cue and stays exactly on time. The
        /// note's own times are untouched; this is drawing only. Capped at
        /// MaxGapFraction of the note's full height so a short note never
        /// disappears.
        /// </summary>
        public static (float bottom, float height) VerticalExtent(
            double noteStartTime, double noteEndTime, double songTime,
            float speed, float highwayHeight, float gap = 0f)
        {
            float fullHeight = (float)(noteEndTime - noteStartTime) * speed;
            float trim = Mathf.Min(Mathf.Max(gap, 0f), fullHeight * MaxGapFraction);
            float top = (float)(noteEndTime - songTime) * speed - trim;
            float bottom = (float)(noteStartTime - songTime) * speed;
            float b = Mathf.Max(bottom, 0f);
            float t = Mathf.Min(top, highwayHeight);
            return (b, t - b);
        }
    }
}
