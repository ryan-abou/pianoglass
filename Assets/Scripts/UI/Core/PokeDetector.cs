namespace PianoGlass.UI
{
    /// <summary>
    /// One fingertip against one button, for pressing Liquid Glass buttons with
    /// a tracked hand (#5). Fed every frame with how far the fingertip is in
    /// front of the button's face and whether it is over the button.
    ///
    /// Hover while over it and within <see cref="HoverMm"/> in front. Press
    /// once as the fingertip pushes through the face from the front; it re-arms
    /// only after backing off past <see cref="ReleaseMm"/>, so resting on a
    /// button never repeats, and a finger arriving from behind never presses.
    /// Pure: HandPokePointer does the hand tracking.
    /// </summary>
    public sealed class PokeDetector
    {
        /// <summary>In front of the face, mm: closer than this shows hover.</summary>
        public const float HoverMm = 40f;
        /// <summary>A press is the fingertip reaching this far in front of the
        /// face (a fingertip bone sits a few mm inside the finger pad).</summary>
        public const float PressMm = 4f;
        /// <summary>Back off this far in front before it can press again.</summary>
        public const float ReleaseMm = 15f;
        /// <summary>Further behind the face than this is not near the button at all.</summary>
        public const float BehindMm = 40f;

        private bool _armed = true;
        private bool _hasLast;
        private float _lastDepth;

        public bool Hovered { get; private set; }

        /// <summary>One frame. <paramref name="depthMm"/>: the fingertip's
        /// distance in front of the face (negative: behind it).
        /// <paramref name="over"/>: within the button's outline. True once per press.</summary>
        public bool Update(float depthMm, bool over)
        {
            bool near = over && depthMm > -BehindMm;
            Hovered = near && depthMm <= HoverMm && depthMm > PressMm;

            if (depthMm > ReleaseMm || !over) _armed = true;

            bool pressed = near && _armed && _hasLast && _lastDepth > PressMm && depthMm <= PressMm;
            if (pressed) _armed = false;

            _lastDepth = depthMm;
            _hasLast = true;
            return pressed;
        }

        /// <summary>The fingertip is gone (hand lost): start fresh.</summary>
        public void Reset()
        {
            _armed = true;
            _hasLast = false;
            Hovered = false;
        }
    }
}
