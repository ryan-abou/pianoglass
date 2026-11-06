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

        [Tooltip("Silent countdown before the first note is due. SongTime starts " +
                 "at -leadInSeconds, not 0 -- without it the first note is already " +
                 "at the key line the instant play is pressed, because the highway " +
                 "only shows notes within leadTimeSeconds of due and nothing pushes " +
                 "the clock back before that window opens.")]
        [SerializeField] private float leadInSeconds = 3f;

        [Tooltip("Start playing as soon as the scene loads. Off by default -- " +
                 "SongTransport / OvrSongTransport are what start it now, so the " +
                 "first notes sit frozen at their lead positions as a preview " +
                 "until something presses play.")]
        [SerializeField] private bool playOnStart = false;

        /// <summary>Seconds elapsed in the song. This is the ONLY thing note
        /// positions should ever be computed from. Starts negative -- see
        /// leadInSeconds -- so the first note still has to fall.</summary>
        public double SongTime { get; private set; }

        public bool IsPlaying { get; private set; }

        /// <summary>Wait mode (F22): the clock goes no further than this song
        /// time, however long that takes. +inf otherwise. Set each frame by
        /// ScoreKeeper to the next note not yet played.</summary>
        public double HoldAt { get; set; } = double.PositiveInfinity;

        /// <summary>Playing, and stopped at the hold.</summary>
        public bool IsHolding => IsPlaying && SongTime >= HoldAt;

        /// <summary>The practice loop running, if any. While set, Advance
        /// wraps the clock at its end. Restart() clears it: restarting from
        /// the top is how a loop is left.</summary>
        public LoopRange? Loop { get; private set; }

        /// <summary>How many times a loop has wrapped since launch. Anything
        /// that treats a backwards jump as a restart reads this to tell a
        /// wrap apart.</summary>
        public int Wraps { get; private set; }

        /// <summary>Which time through the loop this is, from 1; 0 with no loop.</summary>
        public int LoopPass => Loop.HasValue ? Wraps - _wrapsAtLoopStart + 1 : 0;

        /// <summary>Memory fade (F36) wanted.</summary>
        public bool MemoryFadeOn { get; set; }

        /// <summary>The song's own ladder, across play-throughs. PracticeLoop
        /// loads and saves it.</summary>
        public MemoryFade SongFade { get; set; }

        private MemoryFade _loopFade;

        /// <summary>The ladder in use: this loop's, which a new loop starts at
        /// 100%, or with no loop the song's.</summary>
        public MemoryFade Fade => Loop.HasValue ? _loopFade : SongFade;

        /// <summary>Whether notes can fade right now.</summary>
        public bool IsFading => MemoryFadeOn && Fade != null;

        /// <summary>The ceiling for what does not fall with the note: its due
        /// key, the panel naming it. 1 unless fading.</summary>
        public float NoteOpacity => IsFading ? Fade.SteadyOpacity : 1f;

        /// <summary>How visible this note's slab and name are now, 1 unless
        /// fading. A note counts as appearing at the top of the highway, or at
        /// the start of the pass for one already part way down by then, so that
        /// the first notes get their glimpse too.</summary>
        public float NoteOpacityAt(double noteStartTime, double leadTimeSeconds)
        {
            if (!IsFading) return 1f;
            double passStart = Loop.HasValue ? Loop.Value.WrapTo : -leadInSeconds;
            double appeared = System.Math.Max(noteStartTime - leadTimeSeconds, passStart);
            double seconds = tempoMultiplier > 0f ? (DisplaySongTime - appeared) / tempoMultiplier : 0.0;
            return System.Math.Max(Fade.Opacity(seconds), Fade.FlashOpacity(noteStartTime, Time.unscaledTimeAsDouble));
        }

        private int _wrapsAtLoopStart;

        /// <summary>No real headset is this slow; anything larger is a bad
        /// reading, and drawing that far ahead would be worse than none.</summary>
        public const double MaxDisplayLatencySeconds = 0.1;

        private double _displayLatency;

        /// <summary>Real seconds from this frame's Update to the moment the
        /// frame is actually on the display. Set every frame by the headset
        /// adapter (OvrDisplayLatency); 0 in the editor and tests. Clamped
        /// to [0, MaxDisplayLatencySeconds].</summary>
        public double DisplayLatencySeconds
        {
            get => _displayLatency;
            set => _displayLatency = System.Math.Min(System.Math.Max(value, 0.0), MaxDisplayLatencySeconds);
        }

        /// <summary>The song time this frame will show when the player sees
        /// it -- what anything DRAWN from the song must use, so a note
        /// reaches the key line on the display exactly when it is due.
        /// Judging must keep using SongTime: a press is real, not drawn.
        /// Equal to SongTime while paused, because a paused song does not
        /// move between render and display.</summary>
        public double DisplaySongTime =>
            IsPlaying
                ? System.Math.Max(SongTime, System.Math.Min(SongTime + _displayLatency * tempoMultiplier, HoldAt))
                : SongTime;

        public float TempoMultiplier
        {
            get => tempoMultiplier;
            set => tempoMultiplier = Mathf.Max(0f, value);
        }

        public float LeadInSeconds
        {
            get => leadInSeconds;
            set => leadInSeconds = Mathf.Max(0f, value);
        }

        private void Awake()
        {
            // Set here, not just in Restart(): the very first play after the
            // scene loads never calls Restart(), so if this only happened
            // there, the opening bars would still have no lead-in.
            SongTime = -leadInSeconds;
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
            double before = SongTime;
            SongTime += deltaSeconds * tempoMultiplier;

            // Stops on the hold, and never jumps back to one behind it.
            if (SongTime > HoldAt) SongTime = System.Math.Max(before, HoldAt);

            // Subtracting the pass length, not setting WrapTo, keeps whatever
            // the frame overshot by, so a loop drifts no more than the clock.
            if (Loop is LoopRange loop && loop.PassLength > 0.0)
            {
                while (SongTime >= loop.NotesUntil)
                {
                    SongTime -= loop.PassLength;
                    Wraps++;
                }
            }
        }

        /// <summary>Start looping and go to the count-in before the range.
        /// Play state is left as it was.</summary>
        public void StartLoop(LoopRange loop)
        {
            Loop = loop;
            _loopFade = new MemoryFade();
            _wrapsAtLoopStart = Wraps;
            SongTime = loop.WrapTo;
        }

        public void Play() => IsPlaying = true;
        public void Pause() => IsPlaying = false;

        /// <summary>Jump to an absolute song time, e.g. restarting from a bar.</summary>
        public void Seek(double seconds) => SongTime = seconds;

        /// <summary>Back to the top -- which is -leadInSeconds, not 0, so a
        /// restart gets the same silent countdown the first play did. Ends
        /// any practice loop. Play state is left as it was.</summary>
        public void Restart()
        {
            Loop = null;
            _loopFade = null;
            SongFade?.ClearHelp();
            SongTime = -leadInSeconds;
        }
    }
}
