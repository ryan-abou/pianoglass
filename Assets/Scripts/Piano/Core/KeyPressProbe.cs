using System;
using System.Collections.Generic;
using UnityEngine;

namespace PianoGlass.Piano
{
    /// <summary>One key press against the nearest tracked fingertip, in
    /// millimetres along (right, up, toward player) from the key's centre.</summary>
    public readonly struct PressReading
    {
        public readonly bool Found;
        public readonly float Dx, Dy, Dz;
        /// <summary>The next fingertip's sideways offset: how clear the choice
        /// was. NaN with only one finger in range.</summary>
        public readonly float NextDx;

        public PressReading(float dx, float dy, float dz, float nextDx)
        {
            Found = true; Dx = dx; Dy = dy; Dz = dz; NextDx = nextDx;
        }
    }

    /// <summary>
    /// "Learn from key presses" (CLAUDE.md, Calibration ideas): MIDI says which
    /// key went down, and hand tracking says where the fingers were. This only
    /// measures whether that is good enough to correct drift during play; it
    /// moves nothing. The nearest finger is assumed to be the one pressing, so
    /// a shift of more than half a key would be read as a smaller one.
    /// </summary>
    public static class KeyPressProbe
    {
        /// <summary>Two white keys. A fingertip further off did not press it.</summary>
        public const float MaxSidewaysMm = 47f;

        public static PressReading Measure(PianoKeyLayout layout, int note, IReadOnlyList<Vector3> tips)
        {
            if (layout == null || tips == null || !layout.TryGetKey(note, out PianoKey key)) return default;

            bool found = false;
            float bestDx = float.PositiveInfinity, bestDy = 0f, bestDz = 0f, nextDx = float.NaN;
            foreach (Vector3 tip in tips)
            {
                Vector3 offset = (tip - key.Center) * 1000f;
                float dx = Vector3.Dot(offset, layout.Right);
                if (Mathf.Abs(dx) > MaxSidewaysMm) continue;
                if (Mathf.Abs(dx) < Mathf.Abs(bestDx))
                {
                    if (found) nextDx = bestDx;
                    bestDx = dx;
                    bestDy = Vector3.Dot(offset, layout.Up);
                    bestDz = Vector3.Dot(offset, layout.Forward);
                    found = true;
                }
                else if (float.IsNaN(nextDx) || Mathf.Abs(dx) < Mathf.Abs(nextDx)) nextDx = dx;
            }
            return found ? new PressReading(bestDx, bestDy, bestDz, nextDx) : default;
        }
    }

    /// <summary>Medians over many presses: a steady offset shows in the median,
    /// and the spread says how far one press can be trusted.</summary>
    public sealed class PressStats
    {
        private readonly List<float> _dx = new List<float>(), _dy = new List<float>(), _dz = new List<float>();

        public int Count => _dx.Count;

        public void Add(PressReading r)
        {
            if (!r.Found) return;
            _dx.Add(r.Dx);
            _dy.Add(r.Dy);
            _dz.Add(r.Dz);
        }

        public float MedianDx => Median(_dx);
        public float MedianDy => Median(_dy);
        public float MedianDz => Median(_dz);

        /// <summary>Median distance from the median sideways.</summary>
        public float SpreadDx
        {
            get
            {
                float middle = MedianDx;
                var distance = new List<float>(_dx.Count);
                foreach (float d in _dx) distance.Add(Math.Abs(d - middle));
                return Median(distance);
            }
        }

        private static float Median(List<float> values)
        {
            if (values.Count == 0) return 0f;
            var sorted = new List<float>(values);
            sorted.Sort();
            int mid = sorted.Count / 2;
            return sorted.Count % 2 == 1 ? sorted[mid] : (sorted[mid - 1] + sorted[mid]) * 0.5f;
        }
    }
}
