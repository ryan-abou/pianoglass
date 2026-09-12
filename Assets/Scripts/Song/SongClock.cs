using UnityEngine;

namespace PianoGlass.Song
{
    /// <summary>
    /// The single clock the whole falling-note slice reads from. A double of
    /// elapsed seconds, scaled by a tempo multiplier for practice-speed.
    ///
    /// Every note's on-screen position must come from (note.StartTime -
    /// clock.SongTime), computed fresh every frame -- never from a frame
    /// count or an accumulated offset. Frame counts drift the moment the
    /// frame rate does, and a dropped frame on a Quest 2 is not rare; a
    /// double of seconds, advanced by real elapsed time, does not drift.
    /// </summary>
    public class SongClock : MonoBehaviour
    {
        [Tooltip("1 = real time. 0.5 = half speed for practice.")]
        [SerializeField] private float tempoMultiplier = 1f;

        [Tooltip("Start playing as soon as the scene loads. Off by default -- " +
                 "SongTransport / OvrSongTransport are what start it now, so the " +
                 "first notes sit frozen at their lead positions as a preview " +
                 "until something presses play.")]
        [SerializeField] private bool playOnStart = false;

        /// <summary>Seconds elapsed in the song. This is the ONLY thing note
        /// positions should ever be computed from.</summary>
        public double SongTime { get; private set; }

        public bool IsPlaying { get; private set; }

        public float TempoMultiplier
        {
            get => tempoMultiplier;
            set => tempoMultiplier = Mathf.Max(0f, value);
        }

        private void Start()
        {
            if (playOnStart) Play();
        }

        private void Update()
        {
            if (IsPlaying) Advance(Time.deltaTime);
        }

        /// <summary>
        /// Move the clock forward by a real-time delta, scaled by tempo.
        /// Public and independent of Update() so EditMode tests can drive it
        /// deterministically without a running player loop, and so anything
        /// else that ticks time on its own has one correct place to do it.
        /// Applies regardless of IsPlaying -- Update() is what decides
        /// whether to call this every frame; this method itself just does
        /// the arithmetic.
        /// </summary>
        public void Advance(double deltaSeconds)
        {
            SongTime += deltaSeconds * tempoMultiplier;
        }

        public void Play() => IsPlaying = true;
        public void Pause() => IsPlaying = false;

        /// <summary>Jump to an absolute song time, e.g. restarting from a bar.</summary>
        public void Seek(double seconds) => SongTime = seconds;

        /// <summary>Back to the top. Play state is left as it was.</summary>
        public void Restart() => SongTime = 0.0;
    }
}
