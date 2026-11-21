using System.Collections.Generic;
using UnityEngine;

namespace PianoGlass.UI
{
    /// <summary>
    /// Where a held panel was over the last half second, so letting go can
    /// put it back where it was the moment the fingers started to open
    /// (PinchHold.ClosedAt). Pure, so it is tested without a headset.
    /// </summary>
    public sealed class PoseTrail
    {
        /// <summary>Seconds kept.</summary>
        public const double Keep = 0.5;
        /// <summary>Seconds: letting go never goes further back than this.</summary>
        public const double RewindAtMost = 0.3;

        private readonly List<(double time, Pose pose)> _poses = new List<(double, Pose)>();

        public int Count => _poses.Count;

        public void Clear() => _poses.Clear();

        public void Add(double time, Pose pose)
        {
            _poses.Add((time, pose));
            int old = 0;
            while (old < _poses.Count - 1 && time - _poses[old].time > Keep) old++;
            if (old > 0) _poses.RemoveRange(0, old);
        }

        /// <summary>The last pose recorded at or before <paramref name="time"/>,
        /// but no earlier than <see cref="RewindAtMost"/> before
        /// <paramref name="now"/>. False when nothing that old is kept.</summary>
        public bool TryAt(double time, double now, out Pose pose)
        {
            pose = default;
            double t = System.Math.Max(time, now - RewindAtMost);
            for (int i = _poses.Count - 1; i >= 0; i--)
            {
                if (_poses[i].time > t) continue;
                pose = _poses[i].pose;
                return true;
            }
            return false;
        }
    }
}
