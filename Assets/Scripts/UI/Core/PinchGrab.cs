namespace PianoGlass.UI
{
    /// <summary>
    /// When one hand's pinch picks a panel up and puts it down (#5). A grab
    /// starts only as the fingers close while the hand is on a panel, never
    /// when a hand arrives already pinched (it may have pinched for a key, or
    /// for the Meta menu). It ends when the fingers open, or once tracking has
    /// been lost for <see cref="LostSeconds"/> (a short drop-out keeps hold).
    /// Pure, so it is tested without a headset.
    /// </summary>
    public sealed class PinchGrab
    {
        public const float LostSeconds = 0.3f;

        public enum Step { None, Start, Hold, End }

        public bool Holding { get; private set; }

        // Starts true: a hand first seen pinched has to open before it can grab.
        private bool _wasPinching = true;
        private float _lostFor;

        /// <param name="tracked">The hand is tracked this frame.</param>
        /// <param name="pinching">Thumb and index are together.</param>
        /// <param name="onTarget">The hand is on something it could grab.</param>
        /// <param name="dt">Seconds since the last call.</param>
        public Step Update(bool tracked, bool pinching, bool onTarget, float dt)
        {
            if (!tracked)
            {
                _wasPinching = true;
                if (!Holding) return Step.None;
                _lostFor += dt;
                if (_lostFor < LostSeconds) return Step.Hold;
                Holding = false;
                return Step.End;
            }
            _lostFor = 0f;

            bool closing = pinching && !_wasPinching;
            _wasPinching = pinching;

            if (Holding)
            {
                if (pinching) return Step.Hold;
                Holding = false;
                return Step.End;
            }
            if (closing && onTarget)
            {
                Holding = true;
                return Step.Start;
            }
            return Step.None;
        }

        /// <summary>Drop the hold without an End: the pinch pressed a button
        /// instead. The fingers still have to open before the next grab.</summary>
        public void Release() => Holding = false;
    }
}
