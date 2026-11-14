namespace PianoGlass.Piano
{
    /// <summary>
    /// Whether to ask the player to recenter after launch (#17).
    ///
    /// On the Quest 3 a saved anchor loads at launch in a different frame from
    /// the head: both Unity and Meta's runtime put the keys about a piano width
    /// off (1.09 m and 93 deg on 2026-11-14). The first recenter re-bases the
    /// head but not the anchor, and then the keys sit on the piano, every
    /// launch so far. An app cannot do that recenter itself: OVRDisplay
    /// .RecenterPose fires the recentered events but moves nothing (round 3,
    /// 2026-11-14). So the player is asked to hold the Meta button.
    ///
    /// A recenter puts the head at the origin, facing yaw 0. So the prompt is
    /// only needed if the head is clearly away from that when the anchor loads,
    /// and a real recenter shows as the head jumping in one frame by more than
    /// a head can move. Pure and tested; LaunchResync feeds it.
    /// </summary>
    public sealed class LaunchRecenterPrompt
    {
        /// <summary>Head this far from the origin (horizontally) or turned this
        /// far from yaw 0 at the launch load: a recenter would change things.</summary>
        public const float AwayMetres = 0.10f, AwayDegrees = 15f;

        /// <summary>A one-frame head move bigger than this is a re-base, not
        /// the player: 8 cm in a 14 ms frame is over 5 m/s.</summary>
        public const float JumpMetres = 0.08f, JumpDegrees = 15f;

        private bool _loaded;

        /// <summary>Show "hold the Meta button" now.</summary>
        public bool Waiting { get; private set; }

        /// <summary>The saved anchor localized at launch, with the head at
        /// (<paramref name="headX"/>, <paramref name="headZ"/>) facing
        /// <paramref name="headYawDegrees"/> in tracking space.</summary>
        public void OnLaunchLoad(float headX, float headZ, float headYawDegrees)
        {
            if (_loaded) return;
            _loaded = true;
            float away = (float)System.Math.Sqrt(headX * headX + headZ * headZ);
            Waiting = away > AwayMetres || System.Math.Abs(Wrap(headYawDegrees)) > AwayDegrees;
        }

        /// <summary>The head moved this much in one frame. True if it was the
        /// recenter the prompt was waiting for.</summary>
        public bool OnHeadStep(float movedMetres, float turnedDegrees)
        {
            if (!Waiting) return false;
            if (movedMetres <= JumpMetres && System.Math.Abs(Wrap(turnedDegrees)) <= JumpDegrees) return false;
            Waiting = false;
            return true;
        }

        /// <summary>Degrees into -180..180.</summary>
        private static float Wrap(float degrees)
        {
            degrees %= 360f;
            if (degrees > 180f) degrees -= 360f;
            if (degrees < -180f) degrees += 360f;
            return degrees;
        }
    }
}
