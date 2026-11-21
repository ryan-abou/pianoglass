using UnityEngine;

namespace PianoGlass.UI
{
    /// <summary>
    /// Whether a pinch that picked a panel up still holds it (#5), from how
    /// far apart the thumb and index tips are, not the Quest's own pinched
    /// yes/no. That yes/no blinks off while the fingers are still together,
    /// which dropped panels mid-move or froze them (2026-11-15, 2026-11-20).
    /// The gap is measured against the tightest it has been during this hold,
    /// so it works for any hand: the panel is let go once the fingers are
    /// clearly apart (<see cref="OpenBy"/> wider than that, and at least
    /// <see cref="OpenAtLeast"/>) for <see cref="OpenSeconds"/>. While the
    /// hand isn't tracked (or is making the Meta button gesture) the panel
    /// is held still, and let go after <see cref="LostSeconds"/>.
    ///
    /// <see cref="ClosedAt"/> is the last moment the fingers were still
    /// together: letting go puts the panel back where it was then, so opening
    /// the fingers never drags it. Pure, so it is tested without a headset.
    /// </summary>
    public sealed class PinchHold
    {
        /// <summary>Metres wider than the tightest the pinch has been.</summary>
        public const float OpenBy = 0.02f;
        /// <summary>Metres: never let go with the tips closer than this.</summary>
        public const float OpenAtLeast = 0.03f;
        public const float OpenSeconds = 0.05f;
        /// <summary>Metres: within this of the tightest, the fingers are still together.</summary>
        public const float TogetherWithin = 0.008f;
        public const float LostSeconds = 1f;

        public enum Step { Hold, Lost, End }

        private float _closest = float.PositiveInfinity;
        private float _openFor, _lostFor;

        /// <summary>The tightest gap so far, metres.</summary>
        public float Closest => _closest;
        /// <summary>The last time (seconds, the caller's clock) the fingers were together.</summary>
        public double ClosedAt { get; private set; }
        /// <summary>Metres: the gap past which the fingers count as open.</summary>
        public float OpenGap => Mathf.Max(OpenAtLeast, _closest + OpenBy);
        /// <summary>The hold ended because the hand was lost, not opened.</summary>
        public bool EndedLost { get; private set; }

        public void Begin(float gap, double now)
        {
            _closest = gap;
            _openFor = _lostFor = 0f;
            ClosedAt = now;
            EndedLost = false;
        }

        /// <param name="tracked">The hand is tracked (and not making the Meta gesture).</param>
        /// <param name="gap">Thumb tip to index tip, metres.</param>
        /// <param name="now">Seconds, any clock that only goes forward.</param>
        /// <param name="dt">Seconds since the last call.</param>
        public Step Update(bool tracked, float gap, double now, float dt)
        {
            if (!tracked)
            {
                _openFor = 0f;
                _lostFor += dt;
                if (_lostFor < LostSeconds) return Step.Lost;
                EndedLost = true;
                return Step.End;
            }
            _lostFor = 0f;
            _closest = Mathf.Min(_closest, gap);
            if (gap <= _closest + TogetherWithin) ClosedAt = now;

            if (gap > OpenGap)
            {
                _openFor += dt;
                if (_openFor >= OpenSeconds) return Step.End;
            }
            else _openFor = 0f;
            return Step.Hold;
        }
    }
}
