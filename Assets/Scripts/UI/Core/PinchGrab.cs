namespace PianoGlass.UI
{
    /// <summary>
    /// When one hand's pinch picks a panel up (or presses a button from afar)
    /// and puts it down (#5). A pinch counts only when the fingers had been
    /// open for <see cref="OpenSeconds"/> and then stay closed for
    /// <see cref="CloseSeconds"/> while the hand is on something: hand
    /// tracking flickers between pinched and open for a frame or two, which
    /// pressed Songs twice and picked the checklist up while its rows were
    /// being poked (2026-11-14). A hand that arrives already pinched (for a
    /// key, or the Meta menu) never counts. A hold ends once the fingers have
    /// been open for <see cref="ReleaseSeconds"/> (the same flicker dropped
    /// panels mid-move, 2026-11-14), or tracking has been lost for
    /// <see cref="LostSeconds"/>. Pure, so it is tested without a headset.
    /// </summary>
    public sealed class PinchGrab
    {
        public const float OpenSeconds = 0.1f;
        public const float CloseSeconds = 0.05f;
        public const float ReleaseSeconds = 0.2f;
        public const float LostSeconds = 0.5f;

        public enum Step { None, Start, Hold, End }

        public bool Holding { get; private set; }

        // Starts as if pinched: a hand first seen pinched has to open before it counts.
        private bool _wasPinching = true;
        private bool _armed;
        private float _openFor, _closedFor, _lostFor;

        /// <param name="tracked">The hand is tracked this frame.</param>
        /// <param name="pinching">Thumb and index are together.</param>
        /// <param name="onTarget">The hand is on something it could grab or press.</param>
        /// <param name="dt">Seconds since the last call.</param>
        public Step Update(bool tracked, bool pinching, bool onTarget, float dt)
        {
            if (!tracked)
            {
                _wasPinching = true;
                _armed = false;
                _openFor = 0f;
                if (!Holding) return Step.None;
                _lostFor += dt;
                if (_lostFor < LostSeconds) return Step.Hold;
                Holding = false;
                return Step.End;
            }
            _lostFor = 0f;

            if (!pinching)
            {
                if (_wasPinching) _openFor = 0f;
                _openFor += dt;
                _wasPinching = false;
                _armed = false;
                if (!Holding) return Step.None;
                if (_openFor < ReleaseSeconds) return Step.Hold;
                Holding = false;
                return Step.End;
            }

            if (Holding)
            {
                // Closed again: the next open starts its count from zero.
                _wasPinching = true;
                return Step.Hold;
            }

            if (!_wasPinching)
            {
                // The fingers just closed: this pinch counts only after a real open.
                _armed = _openFor >= OpenSeconds;
                _closedFor = 0f;
            }
            else _closedFor += dt;
            _wasPinching = true;

            if (!_armed || _closedFor < CloseSeconds) return Step.None;
            _armed = false; // decided: on something now, or not at all
            if (!onTarget) return Step.None;
            Holding = true;
            return Step.Start;
        }

        /// <summary>Drop the hold without an End: the pinch pressed a button
        /// instead. The fingers still have to open before the next one.</summary>
        public void Release()
        {
            Holding = false;
            _armed = false;
        }
    }
}
