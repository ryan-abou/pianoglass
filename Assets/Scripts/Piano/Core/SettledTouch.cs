using System.Collections.Generic;
using UnityEngine;

namespace PianoGlass.Piano
{
    /// <summary>
    /// Where a fingertip rests on a pressed key, from the frames while it is
    /// held down (#5). The moment MIDI says a key went down, the finger is
    /// still moving, and hand tracking runs a little behind it, so that one
    /// frame lands higher the faster the press; it also jitters by several mm
    /// from frame to frame. Two hand calibrations in a row needed nudges of
    /// 6 to 17 mm (2026-11-15). Once the key is down the finger stops on it:
    /// the middle value of those frames, axis by axis (the median), is steady,
    /// and one bad frame can't pull it the way it would pull an average.
    /// </summary>
    public static class SettledTouch
    {
        /// <summary>Seconds after the key goes down: frames from
        /// <see cref="From"/> (the finger reaching the bottom) until
        /// <see cref="Until"/>, or until the key comes up.</summary>
        public const float From = 0.06f, Until = 0.3f;

        /// <summary>Fewer frames than this: too short a press, use the moment
        /// the key went down instead.</summary>
        public const int MinFrames = 4;

        /// <summary>The median of each axis on its own.</summary>
        public static Vector3 Median(IReadOnlyList<Vector3> points)
        {
            int n = points.Count;
            if (n == 0) return Vector3.zero;
            var xs = new float[n];
            var ys = new float[n];
            var zs = new float[n];
            for (int i = 0; i < n; i++)
            {
                xs[i] = points[i].x;
                ys[i] = points[i].y;
                zs[i] = points[i].z;
            }
            return new Vector3(Middle(xs), Middle(ys), Middle(zs));
        }

        /// <summary>Metres: the farthest any frame was from <paramref name="centre"/>.</summary>
        public static float Spread(IReadOnlyList<Vector3> points, Vector3 centre)
        {
            float worst = 0f;
            for (int i = 0; i < points.Count; i++) worst = Mathf.Max(worst, Vector3.Distance(points[i], centre));
            return worst;
        }

        private static float Middle(float[] v)
        {
            System.Array.Sort(v);
            int m = v.Length / 2;
            return v.Length % 2 == 1 ? v[m] : 0.5f * (v[m - 1] + v[m]);
        }
    }
}
