namespace PianoGlass.Piano
{
    /// <summary>
    /// When to ask the headset for a recenter at launch (#17).
    ///
    /// On the Quest 3 a saved anchor loads at launch in a different frame from
    /// the head: both Unity and Meta's runtime put the keys about a piano width
    /// off (1.09 m and 93 deg on 2026-11-14), and the first recenter moves the
    /// head but not the anchor, after which they agree and the keys sit on the
    /// piano -- five launches out of five. So once the saved anchor has
    /// localized, ask for that recenter instead of waiting for the player.
    ///
    /// Only after a launch load: an anchor created by a fresh calibration is
    /// already in the head's frame. Only once per launch. Pure, so it is tested
    /// without a headset; LaunchResync does the asking.
    /// </summary>
    public sealed class LaunchResyncPolicy
    {
        /// <summary>Seconds after the load before asking: the anchor's pose
        /// settles over the first few frames.</summary>
        public const double DelaySeconds = 0.5;

        private double _dueAt = double.NaN;
        private bool _done;

        /// <summary>A recenter has been asked for this launch.</summary>
        public bool Done => _done;

        public void OnFlowEvent(FlowEvent e, double now)
        {
            if (_done || !double.IsNaN(_dueAt)) return;
            if (e == FlowEvent.LocalizeSucceeded) _dueAt = now + DelaySeconds;
        }

        /// <summary>True exactly once: the moment to ask.</summary>
        public bool Due(double now)
        {
            if (_done || double.IsNaN(_dueAt) || now < _dueAt) return false;
            _done = true;
            return true;
        }
    }
}
